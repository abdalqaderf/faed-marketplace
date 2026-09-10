# Phase Plan — `faed-core`

Eleven phases. **One phase per session.** Every phase ends with a green build and green tests
before the next begins.

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

## Expected result

| | Before | After |
|---|---|---|
| Entities | 30 | 17 |
| C# lines | ~25,600 | ~19,500 |
| Migrations | 11 | 1 |
| Tests | 0 | 5 |
| Fields to publish | ~20 across 2 pages | 8 on 1 page |
| Shop filters | 10 | 4 |

---

## Phases 11–13 — design and demo readiness

This file stops at Phase 10 (the functional rebuild). Phases 11–13 — the visual redesign and
demo readiness — are specified in `docs/DESIGN-BRIEF.md` §9, not here, because they are
design work, not a `Do` / `Acceptance` / `Verify` engineering spec.

**Current status entering Phase 11:** all five public screens are fully specified in
`docs/DESIGN-BRIEF.md` §5 (Home, Listing detail, Reserve, Shop grid, Merchant storefront), and
tokens are locked. Phase 11 is now a mechanical task — apply the tokens and layouts exactly as
written. There are no remaining design decisions to make in this phase.
