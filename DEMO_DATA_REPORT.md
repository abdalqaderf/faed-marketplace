# TASK-016 — Demo Data & Media Report

**Date:** 2026-09-04
**Result:** `COMPLETED`
**Input:** `FINAL_RUNTIME_FIX_REPORT.md`, `tasks/TASK-016-CLAUDE-REALISTIC-DEMO-DATA.md`

## Outcome

The Development/demo database was rebuilt from a clean state and repopulated through the
existing `DemoDataSeeder`, which drives every scenario through the real application services
(no direct aggregate writes, no bypassed moderation/authorization/stock rules). The seeder now
builds 12 listings across the two approved merchants (11 public/Live, 1 sold out), two
admin-controlled brands, four B2C order scenarios, three B2B negotiation/deal scenarios, one
dispute, one review, and one manual inventory adjustment. The previous 1×1-pixel image
fixtures were replaced with 19 real, locally generated PNG product images. Build is Release
with 0 warnings/0 errors; the full test suite (464/464 — 270 unit + 194 integration) passes,
including an updated `DemoDataSeederTests` that verifies the new data set, its idempotency, and
its recovery-after-interruption behaviour against real SQL Server.

## A. Database Reset

- **Database:** local SQL Server LocalDB, instance `(localdb)\MSSQLLocalDB`, database `Faed` —
  exactly the connection string committed in `appsettings.Development.json`
  (`ConnectionStrings:DefaultConnection`). `ASPNETCORE_ENVIRONMENT=Development` was confirmed
  before any destructive step.
- **Pre-reset inspection:** the existing `Faed` database held only synthetic Development data —
  the old 4-listing demo set (4 listings/3 orders/3 negotiations/1 deal/1 review/1 dispute) plus
  ~655 stray `AspNetUsers` rows and ~352 `MerchantProfiles` rows accumulated from earlier ad hoc
  local runs (registration/HTTP tests, prior manual exploration). All rows used `@faed.local` /
  clearly synthetic identities; there was no evidence of production or shared data.
- **Backup:** not required — the data was disposable Development/demo content only.
- **Reset mechanism:** `dotnet ef database drop --force` followed by `dotnet ef database
  update`, both run with `ASPNETCORE_ENVIRONMENT=Development` against the Release build (an
  existing, project-native EF Core mechanism — no ad hoc `DELETE` scripts). This produced a
  genuinely clean rebuild rather than a partial cleanup layered under old data.
- **Migration result:** all 10 existing migrations applied to the fresh database with no drift
  and no new migration created. Schema was not changed by this task.

## B. Demo Accounts

All accounts are created by `DemoDataSeeder` and share one Development-only password.

| Role | Email | Purpose |
|---|---|---|
| Admin | `demo-admin@faed.local` | Merchant verification, listing moderation, disputes, catalog, order/B2B monitoring, reviews, audit logs |
| Approved Merchant | `merchant-a@faed.local` | "Amman Threads" — clothing & bags/accessories store |
| Approved Merchant | `merchant-b@faed.local` | "Petra Footwear" — shoes & bags/accessories store |
| Pending Merchant | `pending-merchant@faed.local` | "Rainbow Kids Wear" — awaiting admin verification |
| Buyer | `buyer-a@faed.local` | Active order, clearance/sold-out order |
| Buyer | `buyer-b@faed.local` | Completed order, review, delivery order |

The shared password is never committed. Set it via user secrets
(`dotnet user-secrets set "Faed:DemoSeed:Password" "<your-choice>"`) or the
`Faed__DemoSeed__Password` environment variable, and enable the seed with
`Faed:DemoSeed:Enabled=true` (or `Faed__DemoSeed__Enabled=true`) — both are read only in
Development (`docs/12-SEED-DATA.md`, `DemoDataOptions`).

## C. Merchants

- **Approved — Amman Threads** (`merchant-a@faed.local`): pickup location (Abdali) + delivery
  zone (Amman — inside the ring road, JOD 2.500 fee, JOD 10.000 minimum, 1–3 working days). 6
  listings.
- **Approved — Petra Footwear** (`merchant-b@faed.local`): pickup location (Sweifieh) + the same
  delivery-zone shape. 6 listings.
- **Pending — Rainbow Kids Wear** (`pending-merchant@faed.local`): submitted for verification,
  left in `PendingReview` so the Admin verification queue has a live case to demonstrate.

## D. Listings

**Total: 12** (11 `Live`, 1 `SoldOut`). Categories: Clothing (3), Shoes (3), Bags & Accessories
(6). All four condition grades (A–D) and six of the eight discount reasons (Overstock, Past
Season, Customer Return, Display Item, Packaging Damage, Cosmetic Defect) are represented.

| Listing | Merchant | Category | Grade | Reason(s) | Variants | B2C/B2B | Notes |
|---|---|---|---|---|---|---|---|
| Everyday Cotton Crew Tee | Amman Threads | Clothing | A | Overstock | 2 (Size×Colour) | B2C+B2B (MOQ 10) | Brand: none |
| Structured Leather Tote | Amman Threads | Bags & Accessories | D | Display Item | 1 | B2C | Defect photo |
| Classic Indigo Denim Jacket | Amman Threads | Clothing | B | Past Season | 3 (Size) | B2C | Brand: Nova Basics; 3 photos incl. packaging |
| Charcoal Wool-Blend Scarf | Amman Threads | Bags & Accessories | A | Overstock | 1 | B2C | **Low stock (3 units)** |
| Genuine Leather Belt | Amman Threads | Bags & Accessories | C | Customer Return | 1 | B2C | |
| Heavyweight Canvas Backpack | Amman Threads | Bags & Accessories | B | Packaging Damage | 2 (Colour) | B2C | Packaging photo |
| Court Low Sneakers | Petra Footwear | Shoes | B | Past Season + Packaging Damage | 3 (Size) | B2C+B2B (MOQ 10) | Used for both B2B scenarios |
| Merino Half-Zip | Petra Footwear | Clothing | C | Customer Return | 1 | B2C | **Sold out** (demo order clears stock) |
| TrailHead Runner | Petra Footwear | Shoes | A | Overstock | 3 (Size) | B2C | Brand: TrailHead |
| Leather Sandals | Petra Footwear | Shoes | D | Display Item | 1 | B2C | Defect photo |
| Sports Socks 3-Pack | Petra Footwear | Bags & Accessories | A | Overstock | 1 | B2C | |
| Travel Shoe Bag Set (3-Pack) | Petra Footwear | Bags & Accessories | C | Cosmetic Defect | 1 | B2C | Defect photo |

Brands: **Nova Basics** and **TrailHead**, both created via `IAdminCatalogService` (the real
admin-only brand-creation path) and looked up by name before creation on any rebuild, so a
purge-and-rebuild cycle cannot duplicate them.

## E. Images

- **Location:** `src/Faed.Web/Data/Seed/Assets/Images/*.png` — 19 files, ~60 KB each (~1 MB
  total), well under the 8 MB per-image limit.
- **Source:** locally generated, original flat-illustration product cards (solid/gradient
  background, a simple silhouette icon for the item type, title/category footer, and a red
  "flaw" marker + label on defect/packaging photos), produced by
  `tools/demo-images/generate-demo-images.ps1` using .NET's built-in System.Drawing/GDI+. No
  file was downloaded or hotlinked, so there is no licensing concern. This was the best
  available option in this session's order of preference (§14 of the task): no user-supplied
  images were provided, and no image-generation or web-fetch tool was available to source real
  photography.
- **Build wiring:** a `Content` item in `Faed.Web.csproj` copies the folder next to the built
  app (`CopyToOutputDirectory=PreserveNewest`, `CopyToPublishDirectory=Never` — Development-only,
  never shipped in a publish artifact). `DemoDataSeeder.DemoAssets.LoadImage` reads each file
  from `AppContext.BaseDirectory` at seed time, so it works identically under `dotnet run` and a
  built `bin` output; a missing file fails the seed with a clear message naming the file instead
  of silently substituting something else.
- **Rendering verified:** fetched a listing image directly over HTTP — genuine `image/png`,
  900×900, 200 OK. All three business-rule-mandated defect/packaging photos (condition Grade B
  or D, or a `PackagingDamage`/`CosmeticDefect` reason, must have visual evidence — enforced by
  `Listing.DescribeSubmissionBlockers`) are present; the seed would otherwise fail submission,
  which is exactly what happened once during development and was fixed by adding the missing
  photos rather than relaxing the rule.

## F. Transaction Scenarios

**B2C:**
- **Active order** — Buyer A buys 2 Tee variants from Amman Threads; merchant confirms
  (`Confirmed`, pickup).
- **Completed order** — Buyer B buys the Handbag from Amman Threads; full lifecycle through
  `ConfirmReceipt` (`Completed`), then reviewed.
- **Sold-out order** — Buyer A clears the Merino Half-Zip's last 4 units from Petra Footwear
  (`Completed`); the listing transitions to `SoldOut`, exercising the public sold-out path.
- **Delivery order** — Buyer B orders a TrailHead Runner from Petra Footwear via
  `MerchantDelivery`; merchant confirms and dispatches (`OutForDelivery`), left short of
  completion.
- **Inventory adjustment** — Amman Threads records a `StockFound` correction on a Tee variant
  (an extra carton found in a stockroom count), exercising the audited manual-adjustment path.

**B2B (all on the Sneakers/Tee listings, same as the prior demo set):**
- **Open negotiation** — Petra Footwear enquires about a wholesale lot of Amman Threads' Tees.
- **Counter-offer chain** — Amman Threads opens on Petra's Sneakers; Petra counters (2 offer
  revisions on one negotiation).
- **Completed deal** — a separate Sneakers negotiation is accepted, marked ready, delivered and
  completed end to end.

**Dispute:** Amman Threads (the buying merchant on the completed deal) files a `MissingItems`
dispute; the Admin starts review (`UnderReview`) — a full audited example still visible in the
queue.

**Review:** Buyer B leaves a 5-star review on the completed Handbag order.

## G. Verification Results

| Check | Command/evidence | Result |
|---|---|---|
| Release build | `dotnet build Faed.slnx -c Release` | PASS — 0 warnings, 0 errors |
| Full test suite | `dotnet test Faed.slnx -c Release --no-build` | PASS — 464/464 (270 unit + 194 integration), 0 failed, 0 skipped |
| Demo seeder test | `DemoDataSeederTests` (updated for the new catalog/order counts) | PASS — first run, idempotent second run, and purge-and-rebuild recovery all verified against real SQL Server |
| Database reset | `dotnet ef database drop --force` + `dotnet ef database update` (Development) | PASS — dropped and recreated `Faed`; all 10 migrations applied, no drift |
| App startup (1st run) | Release build, `ASPNETCORE_ENVIRONMENT=Development`, `Faed__DemoSeed__Enabled=true` | PASS — "Demo data set seeded."; final counts: 7 users, 3 merchant profiles, 12 listings, 4 orders, 3 negotiations, 1 deal, 1 review, 1 dispute, 2 brands |
| App restart (2nd run, idempotency) | Same database, app restarted | PASS — "Demo data already present; skipping demo seed."; row counts unchanged |
| HTTP smoke | `/`, `/Shop`, `/Identity/Account/Register`, `/Merchant/Reviews` | PASS — 200/200/200/302 (anonymous sign-in redirect) as expected |
| Listing detail pages | e.g. `/listing/classic-indigo-denim-jacket-past-season` | PASS — 200, 3 images rendered (2 product + 1 packaging) |
| Merchant storefronts | `/store/amman-threads`, `/store/petra-footwear` | PASS — 200, populated |
| Category filter | `/Shop?category=shoes` → 3; `/Shop?category=bags-accessories` → 6 | PASS — matches the catalog exactly |
| Search | `/Shop?Q=jacket` → 1; `/Shop?Q=sneakers` → 1; `/Shop?Q=scarf` → 1 | PASS — meaningful, exact matches |
| Image rendering | Direct fetch of a `/listing-images/{id}` URL | PASS — genuine 900×900 PNG, `image/png`, 200 OK |

## H. Remaining Notes

- Product photography is original, locally generated flat-illustration artwork, not real
  product photos — no user-supplied images, image-generation tool, or web-fetch capability was
  available in this session (see §E). This is non-blocking for a student-project demo but is
  the one respect in which "realistic" media was not literally photographic.
- The optional "Submitted/Pending listing" moderation scenario mentioned in the task (§15,
  explicitly optional — "if it helps") was not added; the pending-merchant-verification queue
  and the open dispute already exercise the Admin review workflow, and a 13th listing was judged
  unnecessary scope for this task.
- In-app browser screenshot verification was not available in this session; verification used
  live HTTP smoke checks, rendered-HTML inspection, and a direct image fetch instead.
- No blockers. No schema change or migration was made.

## Handoff

TASK-016 is complete. The next task is `TASK-017` for Codex, using the current repository and
this report.
