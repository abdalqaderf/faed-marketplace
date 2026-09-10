# Phase Plan — `faed-core`

Phases 0–10 below are the scope reduction. **Phases 11–13** (design system · public-surface
rebuild · demo readiness) were planned in the design brief and are complete; they are summarised
before Phase 14 and back-filled into this document by Phase 14 §8. **Phase 14** closes the
branch.

**One phase per session.** Every phase ends with a green build and green tests before the next
begins.

`main` is never touched. All work happens on `faed-core`.

The database is **dropped and recreated** — production data is not preserved (decision 18 in
`BUSINESS-MODEL.md`). This is what makes the schema work simple: old migrations are deleted
and replaced by one clean `InitialCreate` in Phase 7.

**Global rule for every phase:** `dotnet build Faed.slnx` and `dotnet test` must both pass
before the phase is reported complete.

---

## Phase 0 — Branch and documents

**Goal:** the branch exists and carries the rules, before any code moves.

**Do**
- `git checkout -b faed-core`
- Add `CLAUDE.md` at the repo root
- Add `docs/CORE.md`, `docs/BUSINESS-MODEL.md`, `docs/PHASE-PLAN.md`
- Replace `README.md`
- Move the current B2B design into `docs/B2B-DESIGN.md`: the negotiation state machine, why
  offer revisions are immutable, and how stock is held on acceptance — written from the
  existing `Services/B2B/` code **before it is deleted**

**Acceptance**
- `git branch --show-current` prints `faed-core`
- `main` has no new commits
- `docs/B2B-DESIGN.md` describes the module well enough to rebuild it later
- No `.cs` or `.cshtml` file changed

**Verify:** `dotnet build Faed.slnx`

---

## Phase 1 — Smoke tests

**Goal:** a safety net before deleting 7,200 lines.

**Do**
- Create `tests/Faed.Web.Tests` (xUnit) and add it to `Faed.slnx`
- Packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
  `Microsoft.EntityFrameworkCore.InMemory`, `FluentAssertions`
- Write **four** tests against current behaviour:

| # | Test | Asserts |
|---|---|---|
| 1 | Listing status transitions | Draft → PendingReview → Live is allowed; illegal transitions throw `DomainException` |
| 2 | Submission blockers | A listing missing a product photo, a discount reason, or an active variant cannot be submitted, and each blocker is reported |
| 3 | Role authorization | A buyer cannot reach merchant endpoints; an unapproved merchant cannot publish |
| 4 | Reservation expiry | An unconfirmed order past its window is cancelled and its stock released |

**Do not** attempt a real `rowversion` concurrency test on the InMemory provider — it does not
support concurrency tokens. Test that `OrderService` handles `DbUpdateConcurrencyException`
correctly instead, and note the limitation in the test file.

**Acceptance**
- `dotnet test` is green and all four themes above are covered. The count is not the criterion —
  splitting a theme into several focused `[Fact]`s is better than one blob, because the failure
  message then names the actual broken rule
- Tests exercise services and entities, not controllers-over-HTTP

**Verify:** `dotnet test`

---

## Phase 2 — Delete the B2B module

**Goal:** −4,640 lines, −5 entities.

**Do**
- Delete `Services/B2B/` (all files), `Areas/Merchant/Controllers/{OffersController,DealsController}.cs`,
  `Areas/Merchant/ViewModels/{B2BOfferModels,B2BDealModels}.cs`,
  `Areas/Merchant/Views/{Offers,Deals}/`, `Areas/Admin/Views/Transactions/{Deals,DealDetails}.cshtml`,
  `Rendering/{B2BDealStatusDisplay,B2BNegotiationStatusDisplay}.cs`
- Delete entities `B2BNegotiation`, `B2BOfferRevision`, `B2BOfferLine`, `B2BDeal`, `B2BDealLine`
  and their `Data/Configurations/`
- Delete enums `B2BDealStatus`, `B2BNegotiationStatus`, `B2BFulfillmentType`, `TrustTransactionType`
- Remove B2B `DbSet`s from `ApplicationDbContext` and `IApplicationDbContext`
- Remove B2B registrations from `DependencyInjection.cs` (including the two expiry services)
- `Review`: drop `B2BDealId`, make `OrderId` required, remove the "exactly one" constraint
- `Dispute`: drop `B2BDealId` for now (the entity itself goes in Phase 3)
- `Listing`: drop the sales-channel and wholesale fields — `AllowB2C`, `AllowB2B`,
  `AllowMixedVariantB2B`, `WholesaleIndicativeUnitPrice`, `WholesaleMinQuantity` — and every
  reference to them in `ListingFormModel`, `Workspace.cshtml`, `MerchantListingService`,
  `ListingOptions`, `PublicMarketplaceService`, the moderation views and the listing cards.
  These are B2B fields, not sector fields; they belong to this deletion, not to Phase 5
- `DescribeSubmissionBlockers`: remove the two channel branches. **A retail price is now
  always required** — that is the rule the `AllowB2C` check used to express
- Clean `AdminOperationsService`, `MerchantAnalyticsService`, `ReviewService`, `DisputeService`,
  admin transaction views and `DemoDataSeeder` of every B2B reference
- Remove B2B links from `_MerchantSubnav.cshtml` and `_AdminSubnav.cshtml`

**Do not** create a migration yet.

**Acceptance**
- `grep -ri "b2b" src/ --include=*.cs --include=*.cshtml` returns nothing
- Build and tests green

**Verify:** `dotnet build Faed.slnx && dotnet test`

---

## Phase 3 — Remove disputes and secondary entities

**Do**
- Delete `Dispute`, `DisputeEvidence`, `AdminActionLog`, `InventoryAdjustment`,
  `MerchantDeliveryZone`, `Brand` and their configurations
- Delete enums `DisputeStatus`, `DisputeReasonCode`, `AdminActionType`,
  `InventoryAdjustmentType`
- Delete `Services/Trust/Dispute*`, `Controllers/DisputeEvidenceController.cs`, all
  `Areas/*/Controllers/DisputesController.cs`, all `Areas/*/Views/Disputes/`,
  `Areas/Admin/Controllers/AuditLogController.cs` and its view,
  `Rendering/{DisputeStatusDisplay,AdminActivityDisplay}.cs`
- Keep `Services/Trust/Review*` — reviews stay
- `Listing`: remove `BrandId` and its navigation
- `Order`: remove `DeliveryZoneId` and `DeliveryFeeSnapshot`; `OrderFulfillmentType` keeps
  `Pickup` and `MerchantDelivery`, but the fee is no longer modelled. **Every Phase-1 order is
  created as `Pickup`** — the delivery choice is not offered at checkout, because the platform
  models no zone, no fee and no address. Delivery is arranged between the two parties on
  WhatsApp after confirmation, at the merchant's own responsibility
- Delete the merchant Inventory feature entirely: `InventoryController`, `IInventoryService`,
  `InventoryService`, `InventoryPageModel`, the Inventory views and the merchant subnav link.
  It is a merchant-facing screen only — it holds no reservation or release logic, so nothing
  in ordering depends on it. Stock editing moves to the quantity field on the listing form
- **Carry this rule forward:** `IInventoryService` documented that a quantity is not a claim
  about the product, so adjusting stock never sent a published listing back to moderation.
  That rule survives the deletion — see Phase 8
- Strip audit-log calls out of every admin service

**Acceptance**
- Those type names appear nowhere in `src/`
- Build and tests green

**Verify:** `dotnet build Faed.slnx && dotnet test`

---

## Phase 4 — Simplify the upload validator

**Goal:** 2,705 lines → about 100.

**Do**
- Rewrite `Services/Merchants/VerificationDocumentValidator.cs` to check only:
  declared content type is one of `application/pdf`, `image/jpeg`, `image/png`; the extension
  agrees with it; size is within `MerchantVerificationOptions`; and the first bytes match the
  magic number for that type (`%PDF-`, `FF D8 FF`, `89 50 4E 47 0D 0A 1A 0A`)
- Delete the deflate/PDF-stream inspection entirely
- Keep the public API (`ValidateMetadata`, `ValidateContent`, `SignatureProbeBytes`) so callers
  do not change

**Acceptance**
- The file is under 150 lines
- A `.exe` renamed to `.pdf` is still rejected
- Build and tests green

**Verify:** `dotnet build Faed.slnx && dotnet test`

---

## Phase 5 — Sector repivot

**Do**
- `CatalogDataSeeder.RootCategorySlug` → `open-box-ex-display`
- Launch categories → `small-kitchen-appliances` (Small Kitchen Appliances),
  `home-cleaning` (Home & Cleaning Appliances), `power-tools` (Power Tools & Workshop)
- Discount reason `PastSeason` → `SupersededModel` ("Superseded Model")
- Condition grade names and descriptions rewritten for appliances (see the four cards in
  `CORE.md` §3.3); the A–D codes do not change
- `Listing`: remove `WarrantyText` (the sales-channel and wholesale fields were already
  removed in Phase 2)
- `Listing`: add `WarrantyType` enum (`None`, `ManufacturerWarranty`, `ShopWarranty`) and
  `WarrantyMonths` (nullable int, 1–120)
- `ReferencePriceEvidenceType`: reduce to `Photo` and `Link`
- `Order`: add `Reference` — 6 chars from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`, unique index,
  generated on creation
- Replace the category images in `wwwroot/images/categories/` with placeholders named for the
  new slugs

**Acceptance**
- No fashion vocabulary anywhere in `src/`
- Every new order gets a unique 6-character reference
- Build and tests green

**Verify:** `dotnet build Faed.slnx && dotnet test`

---

## Phase 6 — Subscriptions

**Do**
- Entity `SubscriptionPlan`: `Code`, `Name`, `MonthlyPriceJod` `decimal(18,3)`,
  `ActiveListingQuota`, `HasFeaturedPlacement`, `SortOrder`, `IsActive`
- Entity `MerchantSubscription`: `MerchantProfileId`, `SubscriptionPlanId`, `Status`,
  `StartsAtUtc`, `ExpiresAtUtc`, `PaymentReference`, `ActivatedByAdminId`, `RowVersion`
- Enum `SubscriptionStatus`: `PendingActivation`, `Active`, `Expired`, `Cancelled`
- Seed the three plans in `CatalogDataSeeder`: Basic 35/15, Standard 50/40, Pro 80/120
- `ISubscriptionBilling` + a manual implementation that records an admin-entered payment
- `SubscriptionService`: choose a plan, activate a month, extend, cancel, report quota usage
- **Publish gate:** `verification approved` **AND** `subscription active` **AND**
  `live listings < quota` — three independent checks with three distinct messages
- **Downgrade rule:** on moving to a smaller quota, keep the N most recently published live and
  pause the rest, then notify the merchant
- `SubscriptionExpiryService` (hosted): move `Active` past `ExpiresAtUtc` to `Expired` and hide
  that merchant's listings — hidden, never archived
- **Renewal restores automatically.** Add a `HiddenBySubscriptionLapse` flag to `Listing`,
  mirroring the existing `HiddenByAdmin` pattern, so a lapse-hide is distinguishable from a
  merchant's own Pause. On activation, un-hide those listings newest-first up to the plan
  quota and leave the rest paused. `BUSINESS-MODEL.md` §6.5 promises renewal is one click; a
  merchant with 40 listings must not click Restore 40 times. The column is free while Phase 7
  still rebuilds the migration from scratch
- Merchant page: current plan, quota usage, how to pay, plan chooser
- Admin page: activate a month, extend, cancel, with a payment reference
- **Reorder onboarding:** verification is submitted and approved *before* a plan is chosen.
  Drafts are creatable from registration
- Add a fifth test: a merchant at quota cannot publish, and can after pausing one

**Acceptance**
- Renewing after a lapse brings listings back without the merchant restoring them one by one
- A merchant who is approved but unsubscribed sees the plan chooser, not an error page
- A merchant who is subscribed but unverified cannot publish
- Downgrading from Pro to Basic with 120 live listings leaves exactly 15 live
- `dotnet test` is green and the quota rule is covered

**Verify:** `dotnet build Faed.slnx && dotnet test`

---

## Phase 7 — One clean migration

> **Before this phase — schema freeze check.** This is the last moment a new column is free.
> Confirm `Listing` carries `HiddenBySubscriptionLapse` (Phase 6). If it does not, finish that
> first: after `InitialCreate` it costs a migration of its own.
>
> **Also before this phase:** back-fill the four Phase 1 tests. They were never written, so Phases
> 2–5 ran without them. They cannot protect deletions that already happened, but tests 2 and 4
> are the regression guards for Phases 8 and 9, which are the riskiest work left. Rewrite
> test 3 against current behaviour — an unapproved merchant now *can* build drafts; the publish
> gate is three checks.

**Goal:** replace eleven migrations of history with a single truthful schema.

**Do**
- `dotnet ef database drop -f --project src/Faed.Web`
- Delete every file in `src/Faed.Web/Data/Migrations/`
- `dotnet ef migrations add InitialCreate --project src/Faed.Web`
- `dotnet ef database update --project src/Faed.Web`
- Run the app and confirm roles and reference data seed cleanly

**Acceptance**
- Exactly one migration plus the model snapshot
- A fresh database is created from empty in one command
- The three subscription plans, four condition grades, eight discount reasons and three
  categories are present after first run

**Verify:** `dotnet ef database drop -f --project src/Faed.Web && dotnet ef database update --project src/Faed.Web && dotnet run --project src/Faed.Web`

---

## Phase 8 — Merchant listing form

**Goal:** two pages and five sections → one page and eight fields.

**Do**
- `ListingFormModel`: 17 properties → 9 (see `CORE.md` §3.2) — eight decision fields plus an
  optional `Description`, placed last. Make `Listing.Description` nullable in the entity and its
  configuration, and regenerate `InitialCreate` in place; nothing is released
- Merge `Views/Listings/Create.cshtml` and `Workspace.cshtml` into one page under 250 lines
- Build the four condition cards; one click sets `ConditionGradeId` **and**
  `DiscountReasonIds`. Put the mapping in one small class, `ConditionPresets`
- Selecting card 2 or 4 reveals the defect-photo upload inline
- Hide options and variants entirely: the quantity field creates one variant with a generated
  SKU
- **A quantity-only change must not send a published listing back to moderation.** Stock is not
  a claim about the product; the title, condition, photos and price are. This rule came from
  the deleted `IInventoryService` and must be enforced here instead — comment it where it lives
- One photo box plus the contextual defect box — not three typed buckets
- Original price is optional; when set, one evidence field appears (photo or link)
- Rename the merchant's actions to **Pause** and **Delete** only
- Convert `DescribeSubmissionBlockers` into inline validation shown next to each field
- One **Publish** button; afterwards the listing shows "Under review"

**Acceptance**
- Publishing an item takes one page; only eight inputs ask for a decision
- The words "variant", "SKU", "condition grade", "discount reason" appear nowhere in the
  merchant UI
- Choosing "Ex-display" without a defect photo blocks publishing, inline
- Build and tests green

**Verify:** `dotnet build Faed.slnx && dotnet test`, then publish an item by hand

---

## Phase 9 — Buyer journey and the handoff

**Do**
- Rename checkout to **Reserve** everywhere; `Checkout/Index.cshtml` 362 lines → about 80
- Remove all cart-and-payment language from views and copy, including any delivery choice —
  every Phase-1 order is `Pickup`
- `ShopFilterModel`: 10 filters → 4 (category, condition, price range, text). Delete the
  advanced-filter drawer. **Sort stays** — Newest · Price low–high · Price high–low. It is not a
  filter, and without it the response rate has nothing to act on. Default order is newest first
  with response rate as the tie-breaker among same-day listings
- **Contact reveal:** before `Confirmed` the buyer sees the shop's area only; after, the full
  address, map link, pickup hours and phone
- **WhatsApp button** on the order page for both sides — a `wa.me` link with a pre-filled
  message including the order reference. No API, no package
- Extend the background services: 12 h `Pending` → `Cancelled`; 48 h `Confirmed` → `NoShow`;
  72 h `ReadyForPickup`/`OutForDelivery` with no movement → **`NoShow`**, not `Completed`.
  The platform never asserts a sale it did not witness: a wrong `Completed` marks live stock as
  sold and lets a buyer review a purchase that never happened, permanently polluting the
  reputation layer, while a wrong `NoShow` merely returns stock to sale — visible and
  recoverable. Each sweep carries its own message to the buyer
- **Show each deadline to the person it binds.** The reserve page already states the shop's
  12 h confirmation window; the confirmed-order page must state the buyer's 48 h collection
  deadline as an absolute date and time ("Collect by Thursday, 6 PM"), not a duration. Before
  this, the buyer was never told their own deadline and met it only as a `NoShow` notice
- Add a merchant action **"Actually collected"** on a `NoShow` order, moving it to `Completed`.
  The sale usually did happen and the merchant simply never opened the dashboard; this repairs
  the record and unlocks the review without the platform inventing a transaction
- **Response rate:** rolling 30 days, confirmed-before-deadline ÷ received, plus a bucketed
  median response time. Below 5 orders show a "New seller" badge instead. Display on the
  storefront and under each listing, and factor it into catalogue ordering
- Add a visible order status timeline for both sides

**Acceptance**
- The word "checkout" appears nowhere
- An unconfirmed order is cancelled after 12 hours and its stock released
- A merchant with fewer than 5 orders shows "New seller", never a percentage
- The WhatsApp link opens with the correct pre-filled text
- Build and tests green

**Verify:** `dotnet build Faed.slnx && dotnet test`

---

## Phase 10 — Demo data and final verification

**Do**
- Rewrite `DemoDataSeeder` for the new world: an admin; two approved and subscribed merchants
  (one Standard, one Basic); one merchant pending verification; one approved but unsubscribed;
  two buyers; a catalogue across the three categories using every condition card; and one order
  in each state — pending, confirmed, ready, completed, cancelled, no-show; plus one review
- The seeder must call the same application services a real user would — no direct
  `DbContext` writes that bypass moderation, authorization, quota or concurrency
- Walk all three roles by hand
- Update `README.md` with anything that drifted

**Acceptance**
- A clean database plus demo seed produces a browsable site with no empty screens
- Every role can complete its journey end to end
- `dotnet build` and `dotnet test` green

**Verify:** drop, update, run, and walk the three roles

---

## Phases 11–13 — Visual rebuild (complete)

Planned in the design brief, not here. Summarised so Phase 14 has the context:

| Phase | Work |
|---|---|
| **11** | Design system (tokens) + mockups of the five public screens. No app code |
| **12** | Public surface rebuilt to the mockups · workspaces token pass · an empty state for every screen · one responsive check |
| **13** | Demo readiness: real images in `DemoDataSeeder` · a written demo script for the defence · full walkthrough of all three roles |

Phase 14 §8 back-fills these three into this document from what the code actually does.

---

## Phase 14 — Final audit and defence readiness

**Goal:** a repository that tells the truth, contains nothing it does not need, and a demo that
cannot break. **No new features.**

This project is being finished for a **university graduation defence**, not a launch. The demo
is the product: a panel watches a screen, it does not read the code. Phase 14 removes what is
dead, proves what is claimed, and rehearses the walkthrough.

### Working rules for this phase

1. **Audit first, fix second.** Work Steps 1–9 in order, writing every finding into
   `docs/AUDIT-PHASE-14.md` as it is found — severity (Blocker / High / Medium / Low), file,
   what is wrong, proposed fix. **Change no code until the full report is reviewed and
   approved.** The only exceptions are Step 1 (secrets) and Step 2 (the build must compile) —
   fix those immediately and record what was done.
2. Prefer deleting over flagging. Delete code, never comment it out. No `TODO` comments.
3. **Do not touch `wwwroot/css/faed.css`** beyond deleting rules that can be *proven*
   unreferenced. It is 8,000+ lines, it works, and it is invisible to the panel. Refactoring it
   is out of scope.
4. No new NuGet packages, no frontend framework, no build step, no new features.
5. If a deletion is ambiguous — a file that cannot be proven unreferenced, or a comment that may
   carry a business rule the docs do not — **stop and ask.** A rule that lives only as a comment
   on a deleted file disappears with it; this has already gone wrong twice on this project.
6. Numbers in the docs are markers of intent, not the intent. When a number collides with the
   goal, the goal wins — report the collision rather than working around the number.

### Step 1 — Secrets and public-repo safety *(fix immediately)*

The repo is public. Grep the tree for connection strings, passwords, API keys, tokens, real
e-mail addresses and phone numbers across `appsettings*.json`, `*.cs`, `*.cshtml`, `*.md`,
`.vscode/`, `Properties/launchSettings.json`. Confirm `.gitignore` covers
`appsettings.Development.json`, `bin/`, `obj/`, `*.user` and uploaded media. `git ls-files` must
list no uploaded verification document and no local media dump. Seeded demo credentials are
expected — but they must be obviously fake and documented in `README.md`. If a secret is already
in git history, report it; do not rewrite history.

### Step 2 — Clean build *(fix immediately)*

`dotnet clean && dotnet build Faed.slnx -warnaserror`. Nullable reference types are on and
`CLAUDE.md` forbids suppressing warnings to make code compile. Fix real warnings properly; if
one can only be silenced by a suppression, leave it and report it.

### Step 3 — Migration and schema state

`Data/Migrations/` holds exactly one migration plus the model snapshot, and
`dotnet ef migrations list` agrees. Prove model and snapshot are in sync:
`dotnet ef migrations add __probe` must produce an **empty** migration — then delete `__probe`.
The app must not migrate on startup, and seeding must be idempotent (run it twice).

### Step 4 — Dead vocabulary and dead code

Re-run the earlier acceptance greps across the **whole** tree — `.cs`, `.cshtml`, `.js`, `.css`,
`.json`, `docs/`. Phases 11–13 rebuilt the public surface, so earlier greens may have regressed:

```bash
grep -rin "b2b\|negotiation\|wholesale\|dispute\|escrow\|checkout\|cart\b" src/
grep -rin "brand\|inventory\|deliveryzone\|auditlog\|adminactionlog" src/
grep -rin "fashion\|apparel\|garment\|pastseason" src/
grep -rn  "TODO\|FIXME\|HACK" src/ tests/
```

Some hits are legitimate (`Brand` inside a third-party string, `size` as a file size) — judge
each and list the judgements. Then confirm the merchant-facing UI contains none of `variant`,
`SKU`, `condition grade`, `discount reason`, `archive`, `hide`; the merchant sees two lifecycle
words only, **Pause** and **Delete**. Finally, find genuinely dead code: unreferenced
controllers, actions, services, interfaces, view models, enums, partials, `Rendering/` helpers,
and DI registrations pointing at nothing.

### Step 5 — Files that should not exist

**The repository must contain nothing it does not need.** Walk the entire tree, not only `src/`,
and propose for deletion every file that no longer earns its place. `git ls-files` is the list
that matters — an untracked local file is not the panel's problem, a tracked one is.

**Markdown and documents — this is the keep-list. Everything else in `.md` goes.**

| Keep | Why |
|---|---|
| `README.md` | repo root — the entry point |
| `CLAUDE.md` | repo root — the working rules |
| `docs/CORE.md` | the scope |
| `docs/BUSINESS-MODEL.md` | the reasoning |
| `docs/PHASE-PLAN.md` | this file |
| `docs/B2B-DESIGN.md` | the archived module, deliberately preserved |
| `docs/04-DOMAIN-MODEL.md` | regenerated in Step 8 |
| `docs/DEPLOYMENT.md` | keep **only if** it still matches reality — otherwise fix it or delete it |
| `docs/COMMITS-AND-COMMENTS.md` | referenced by `CLAUDE.md` |
| `docs/AUDIT-PHASE-14.md` | this phase's own report |

Anything else — a stale `plan.md`, numbered design documents from the pre-`faed-core` era
(`docs/01-*`, `docs/02-*`, `docs/03-*`, `05-*` …), `NOTES.md`, `CHANGELOG.md`, `TASKS.md`,
scratch notes, a second copy of the README, **the Arabic working documents and the Claude Code
prompt files** (the file-placement table says those live outside the repo) — is proposed for
deletion. A `.md` that is stale is worse than absent: it is a document that contradicts the code
in front of the panel. If a stale document contains a decision the keep-list files do not,
**move that paragraph into the right doc first, then delete the file** — do not delete a
decision.

**Everything else that should not be there.** Report each with its evidence:

- Editor and OS droppings: `.DS_Store`, `Thumbs.db`, `desktop.ini`, `*.swp`, `.idea/`,
  `*.user`, `*.suo`
- Tracked build output: `bin/`, `obj/`, `TestResults/`, `*.log` — and the `.gitignore` lines
  that should have stopped them
- Backups and duplicates: `*.bak`, `*.orig`, `*.rej`, `*~`, `*.old.*`, `* copy.*`,
  `Copy of *`, `*-v2.cshtml`, `Index2.cshtml`, `Class1.cs`, `NewFile1.cs`
- Framework template leftovers never used by this app: `WeatherForecast*`, unused scaffolding,
  unused `Areas/Identity` pages that were never wired
- Design-phase artefacts: mockup `.html` files, exported PNG comps, Figma dumps, tokens
  sketches from Phase 11 — the design shipped as CSS; the mockups are not the app
- Unused `wwwroot/lib/` packages, second icon sets, alternate Bootstrap themes, unreferenced
  fonts, `libman.json` entries pointing at what is no longer used
- Archives and media dumps: `*.zip`, `*.rar`, `*.7z`, screenshots, sample uploads, a committed
  `.bak` database
- Empty directories, and directories left holding a single unreferenced file

**Assets in use** — the ordinary sweep: every file in `wwwroot/images/` is referenced or gone;
category images match the three current slugs and no fashion-era placeholder survives; no orphan
script in `wwwroot/js/`, and none bound to an element that no longer exists; no Razor view
without an action behind it (Phases 8, 9 and 12 merged several pages). Obsolete fashion-palette
tokens in `faed.css` (`#f7f3eb`, `#0d6957`) are **reported, not deleted**, per rule 3.

**How to prove a file is dead** before proposing it: `grep -rn "<filename-without-extension>"`
across `src/`, `tests/`, `docs/`, `*.csproj`, `Faed.slnx` and `libman.json`; for a view, find the
action or the `Partial`/`Component` call that renders it; for an image, the view, CSS or seeder
that names it. **A file you cannot prove is dead is not proposed — it is asked about**, per
rule 5. After deleting, `dotnet build Faed.slnx -warnaserror && dotnet test` must still pass,
and Step 9's rehearsal must still run — a missing partial or image fails at runtime, not at
build time.

### Step 6 — Tests

`dotnet test` green. Then confirm the five tests still assert what they were written for against
current behaviour: listing status transitions · submission blockers · the three-check publish
gate · reservation expiry · the quota rule. **A test that now passes vacuously is worse than a
missing one** — report it. Then list, without writing them, the gaps that matter for the
defence: the 12 h / 48 h / 72 h sweeps, "Actually collected" on a `NoShow`, and the response
rate below and above the 5-order threshold. Confirm the InMemory `rowversion` limitation is
still commented in the test file.

### Step 7 — Invariants review

Check the eight invariants in `CLAUDE.md` one by one and state, for each, whether it holds and
**which file enforces it**: `rowversion` on stock · order snapshots · no physical deletes of
transactional history · append-only moderation · business rules in the entity not the service ·
nothing waits on a human forever · publishing needs two independent conditions · money never
touches the platform.

Two rules were carried across deletions and can only be enforced where they now live — verify
both explicitly:

- a **quantity-only** edit must not send a published listing back to moderation;
- `HiddenBySubscriptionLapse` must stay distinguishable from a merchant's own Pause, so renewal
  restores listings automatically.

Confirm nothing from the `CLAUDE.md` "Do not add" list crept in during Phases 11–13.

### Step 8 — Documentation drift

- Back-fill **Phases 11, 12, 13** into this document as completed sections in the same
  Do / Acceptance / Verify format, written from what the code actually does — not from what was
  planned — and record Phase 14's own outcome.
- Regenerate `docs/04-DOMAIN-MODEL.md` from the current entities: every entity, its key fields,
  its relationships, and an entity count. It was due in Phase 7 and never done.
- `README.md`: setup in the right order, demo accounts, the three roles, the current categories
  and plans. Someone who has never seen the repo must get it running from it alone.
- Flag any statement in `CORE.md` or `BUSINESS-MODEL.md` the code now contradicts. **List
  contradictions; never silently rewrite a business decision.**
- Confirm `docs/B2B-DESIGN.md` survived and is still detailed enough to rebuild the module.

### Step 9 — Defence rehearsal from zero

Simulate the machine the demo runs on: fresh clone to a temp folder, `faed-core`, build,
`dotnet ef database drop -f`, `database update`, `dotnet run`. Then walk all three roles end to
end and **time each step**: merchant publishes · admin approves · buyer reserves · merchant
confirms · contact reveal · pickup completes · buyer reviews. Report every empty screen, broken
image, 404, unhandled exception, console error and slow page — that is what the panel will see.
Confirm the Phase 13 demo script still matches the actual screens.

**Run this step again after the Step 5 deletions land.** A deleted partial, image or script
fails at runtime, not at build time, and this rehearsal is the only thing that catches it.

### Deliverable

`docs/AUDIT-PHASE-14.md` — findings ordered by severity, each with file, evidence and proposed
fix; a **deletion list** as its own section, each entry with the proof that the file is dead;
then the rehearsal log with timings; then a "state of the repo" summary: tracked file count,
entity count, C# line count, migration count, test count, build warnings. Present the report and
**stop**. Fix only what is approved, in severity order, in separate commits, running
`dotnet build Faed.slnx && dotnet test` after each — deletions in one commit of their own, so a
single `git revert` undoes them.

**Acceptance**
- No secret, credential or uploaded document tracked in git
- `dotnet build Faed.slnx -warnaserror` passes
- Exactly one migration plus the snapshot; a probe migration comes out empty
- The Step 4 greps return only hits justified in writing
- **`git ls-files` lists no file that cannot be justified**: no `.md` outside the keep-list, no
  build output, no backup or duplicate, no editor dropping, no design-phase artefact, no
  unreferenced view, script, image or library
- No decision lost with a deleted document — anything worth keeping was moved first
- `dotnet test` green, with no vacuously-passing test
- All eight invariants confirmed, each with the file that enforces it
- `PHASE-PLAN.md`, `04-DOMAIN-MODEL.md` and `README.md` match the code
- A clone-to-running walkthrough of all three roles with no empty screen and no error, **run
  after the deletions**

**Verify:** `dotnet build Faed.slnx -warnaserror && dotnet test`, then the Step 9 rehearsal

---

## Expected result

| | Before | After |
|---|---|---|
| Entities | 30 | 17 |
| C# lines | ~25,600 | ~19,500 |
| Migrations | 11 | 1 |
| Tests | 0 | 5 |
| Fields to publish | ~20 across 2 pages | 8 on 1 page |
| Shop filters | 10 | 4 |