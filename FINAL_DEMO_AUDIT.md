# TASK-017 — Final Demo & Runtime Audit

**Date:** 2026-09-05
**Result:** `PASS WITH NOTES`
**Agent:** Claude Code, performing TASK-017 in place of Codex at the user's explicit
direction. TASK-017 is assigned to Codex in `tasks/FINALIZATION_PROGRESS.md` and
`tasks/TASK-017-CODEX-DEMO-RUNTIME-REVIEW.md`; the user was asked and chose to have
Claude Code execute it directly this session rather than wait for Codex. This is
recorded here as a deviation from the normal agent assignment, not as a redefinition
of it — Decision #1/#2 in the tracker still hold for future tasks.
**Input:** `DEMO_DATA_REPORT.md`, current repository state (uncommitted TASK-016
changes still in the working tree).
**Mode:** REVIEW ONLY — no application/source code was modified. One environmental
action (dropping and recreating the local `Faed` LocalDB, then reseeding it) was taken
mid-review, with explicit user approval, to diagnose a login failure; see Finding 1.

## Overall Result

**PASS WITH NOTES.** The application, its demo data, and its role-based authorization
all work correctly once the local database reflects a seed produced with the
currently-configured demo password. One finding (non-blocking, environmental — not a
code defect) is recorded below with full root-cause analysis. No P0/P1 code issues
were found. All build, migration, idempotency, authorization, navigation, and workflow
checks passed after the database was refreshed.

## A. Build & Test Baseline

| Check | Command | Result |
|---|---|---|
| Release build | `dotnet build Faed.slnx -c Release` | PASS — 0 warnings, 0 errors |
| Full test suite | `dotnet test Faed.slnx -c Release --no-build` | PASS — 464/464 (270 unit + 194 integration), 0 failed, 0 skipped |

## B. Migrations

| Check | Command | Result |
|---|---|---|
| Migration list | `dotnet ef migrations list` (Development) | PASS — all 10 migrations present, applied, in order |
| Model drift | `dotnet ef migrations has-pending-model-changes` | PASS — "No changes have been made to the model since the last migration." |
| Clean apply | `dotnet ef database drop --force` + `dotnet ef database update` (Development) | PASS — dropped and recreated `Faed`; all 10 migrations applied with no errors |

## C. Demo Seeding

| Check | Evidence | Result |
|---|---|---|
| First run (fresh DB) | Startup log: role/catalog seeding, "Seeded development Admin admin@faed.local", **"Demo data set seeded."** | PASS |
| Second run (idempotency) | App restarted against the same DB; log shows "Development Admin admin@faed.local already present" and **"Demo data already present; skipping demo seed."** | PASS |
| Row counts stable across restart | `AspNetUsers`=7, `Listings`=12 before and after restart | PASS |
| Final data shape matches `DEMO_DATA_REPORT.md` | `SELECT COUNT(*)` across tables: 7 users, 3 merchant profiles, 12 listings, 4 orders, 3 B2B negotiations, 1 B2B deal, 1 review, 1 dispute, 2 brands | PASS — exact match to the report |

## D. Finding 1 — Stale local demo credentials after a database refresh (non-blocking, environmental)

**Severity:** P2 (operational/documentation, not a code defect). **Status:** diagnosed
and resolved for this session's database; no code change required.

**Observation:** On first starting the app against the `Faed` database exactly as left
by TASK-016, none of the 6 documented demo accounts (`demo-admin@faed.local`,
`merchant-a@faed.local`, `merchant-b@faed.local`, `pending-merchant@faed.local`,
`buyer-a@faed.local`, `buyer-b@faed.local`) could log in with the password currently
stored in the `Faed:DemoSeed:Password` user secret — every attempt returned "Invalid
login attempt." The separate `admin@faed.local` development-admin account (a different
seeder, `IdentityDataSeeder`, using `Faed:AdminSeed:Password`) logged in successfully
with the same request mechanism, proving the login pipeline itself (antiforgery,
cookie auth, `PasswordSignInAsync`) was not broken.

**Root cause:** `DemoDataSeeder` only sets each demo account's password once, at the
moment the account row is first created (`_users.CreateAsync(user, _password)` in
`DemoDataSeeder.cs`), using whatever `Faed:DemoSeed:Password` value was in effect for
*that specific process*. The accounts on disk were created at that earlier moment;
their password hash reflects the value used then, not necessarily the value later left
in `secrets.json`. Because `DemoDataSeeder.SeedAsync` skips entirely once demo data is
present, it never re-applies the password on subsequent runs even if the configured
secret has since changed. The two values had drifted apart on this machine.

**Verification of root cause (not a code bug):** with explicit user approval, the
local `Faed` database was dropped (`dotnet ef database drop --force`) and recreated
(`dotnet ef database update`), and the app was restarted with demo seeding enabled.
This produced a genuine fresh seed ("Demo data set seeded."), and all 6 demo accounts
then logged in successfully on the first attempt with the exact password currently in
`Faed:DemoSeed:Password`. A second restart against this same database reproduced the
idempotent skip and login continued to work, confirming the seeding/auth mechanism is
sound — the original failure was purely stale local state, not a defect in
`DemoDataSeeder`, `ApplicationUser`, or the Identity configuration.

**Recommendation:** no code change is required. For future sessions/graders: if demo
login ever fails after opening this repository on a machine where `Faed:DemoSeed:Password`
was set independently of a previous seeding run, the fix is to reseed
(`dotnet ef database drop --force && dotnet ef database update`, then run the app once
with `Faed:DemoSeed:Enabled=true`) rather than to suspect the seeder logic. This is
worth one sentence in the final README/setup docs (candidate for TASK-020) so a grader
hitting this doesn't mistake it for a broken feature.

## E. Role & Route Verification (post-reseed, all against the working demo password)

All checks below were performed by authenticating with real HTTP form logins (cookie
sessions), not by inspecting code — i.e., genuine black-box verification of
server-side authorization.

### Anonymous
| Route | Result |
|---|---|
| `/`, `/Shop`, `/Identity/Account/Register`, `/Identity/Account/Login` | 200 |
| `/Merchant/Listings`, `/Merchant/Orders/Index`, `/Admin`, `/Buyer/Orders` | 302 → `/Identity/Account/Login?ReturnUrl=...` |
| `/store/amman-threads`, `/store/petra-footwear` | 200 |
| `/Shop?category=shoes` (3), `/Shop?category=bags-accessories` (6) | 200, counts match catalog |
| `/Shop?Q=jacket`, `/Shop?Q=sneakers` | 200, meaningful matches |
| Live listing detail (`/listing/classic-indigo-denim-jacket-past-season`) | 200, 3 images rendered |
| SoldOut listing detail (`/listing/merino-half-zip-final-units`) | **404 — correct by design.** `ListingController` documents that anything not `Live` (Draft/Hidden/SoldOut) is a 404, never a partial render, per `docs/06-ARCHITECTURE.md` §12 and `docs/11-ACCEPTANCE-CRITERIA.md` ("Public sees only Live listings"). |
| Listing image (`/listing-images/{id}`) | 200, genuine `image/png`, 60 KB |

### Buyer (`buyer-a@faed.local`, `buyer-b@faed.local`)
| Route | Result |
|---|---|
| `/Buyer/Orders` | 200 |
| `/Buyer/Disputes` | 200 |
| `/Buyer/Checkout?slug={live-listing}` | 200 (bare `/Buyer/Checkout` is a 404 because the action requires a `slug` — expected routing, not a bug) |
| `/Merchant/Listings`, `/Admin` | 302 → `AccessDenied` — correctly blocked |

### Pending Merchant (`pending-merchant@faed.local`, "Rainbow Kids Wear")
| Route | Result |
|---|---|
| `/Merchant/Verification` | 200 — pending status visible |
| `/Merchant/Listings`, `/Merchant/Orders/Index` | 302 → `AccessDenied` — correctly blocked until approved |

### Approved Merchant (`merchant-a@faed.local`, `merchant-b@faed.local`)
| Route | Result |
|---|---|
| `/Merchant/Listings`, `/Merchant/Orders/Index`, `/Merchant/Reviews`, `/Merchant/Deals`, `/Merchant/Analytics`, `/Merchant/Inventory`, `/Merchant/StoreSettings` | 200 |
| `/Merchant/Offers` (B2B negotiation list) | 200 |
| `/Merchant/Offers/Details/{id}` for both an Open negotiation and the counter-offer-chain negotiation | 200 |
| `/Admin` | 302 → `AccessDenied` — correctly blocked |

### Admin (`demo-admin@faed.local`)
| Route | Result |
|---|---|
| `/Admin`, `/Admin/MerchantVerification`, `/Admin/ListingModeration`, `/Admin/Disputes`, `/Admin/Catalog`, `/Admin/Reviews`, `/Admin/AuditLog` | 200 |
| `/Admin/Transactions/Orders`, `/Admin/Transactions/Deals` | 200 (bare `/Admin/Transactions` 404s — no index action exists on that controller; expected routing, not a bug) |
| `/Admin/Disputes/Details/{id}` for the `UnderReview` dispute | 200 |
| Pending merchant "Rainbow Kids Wear" visible in `/Admin/MerchantVerification` | Confirmed present |
| `/Merchant/Listings` | 302 → `AccessDenied` — correctly blocked (Admin has no Merchant-role access, matching the separation of concerns) |

## F. Workflow Coverage Represented in Demo Data

Confirmed present and reachable through the UI, matching `DEMO_DATA_REPORT.md` §F:

- B2C: active order, completed+reviewed order, sold-out-clearing order, merchant-delivery
  dispatched order, one manual `StockFound` inventory adjustment.
- B2B: one open negotiation, one counter-offer chain (2 revisions), one completed deal
  (accepted → fulfilled → completed).
- Dispute: one `MissingItems` dispute in `UnderReview`, visible and openable in the
  Admin queue.
- Moderation: one pending merchant verification case ("Rainbow Kids Wear") live in the
  Admin queue.
- Review: one 5-star review tied to the completed order.

## G. Errors, Exceptions, Broken Assets

- No unhandled exceptions, `fail:`, or `error:` log lines appeared across the full
  session (fresh seed, idempotent restart, anonymous browsing, and all 6
  authenticated role sessions), other than the expected, harmless
  `Failed to determine the https port for redirect` warning (HTTP-only local run, not
  a defect).
- No broken image URLs encountered; the one direct image fetch performed returned a
  genuine 900-pixel-class PNG (see §E).
- No empty/broken key pages encountered across all role/route combinations tested.

## H. Manual/Remaining Checks

- **Visual/browser verification was not available in this session** (no browser
  automation tool was present); all checks were performed via real HTTP requests with
  authenticated cookie sessions and direct SQL Server reads for data verification, not
  static code review. A manual pass in an actual browser (visual layout, responsive
  behavior, console errors, image alt text rendering) is still recommended before
  final submission, consistent with the same caveat recorded in TASK-014/015/016.
- Product photography remains original generated flat-illustration artwork rather than
  real photography (carried over from TASK-016, §H of `DEMO_DATA_REPORT.md`) —
  non-blocking for a student project, already documented as a known limitation.
- The local `Faed` database was dropped and recreated during this review (with user
  approval) and now contains a fresh seed rather than the exact rows TASK-016 produced.
  The data *shape* (row counts, scenarios, accounts) is identical — only the underlying
  GUIDs/timestamps differ. This is disposable Development-only data and requires no
  follow-up.

## Handoff

TASK-017 is complete with no blocking findings. Finding 1 (§D) is fully diagnosed,
non-blocking, and requires no code fix — only an optional documentation note,
which is deferred to TASK-020 (README & final docs) rather than treated as a blocker
for repository cleanup.
