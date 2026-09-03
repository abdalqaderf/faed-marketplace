# Project Status

## Task status

| Task | Phase | Status |
|---|---|---|
| TASK-001 — Foundation | 0 | Completed |
| TASK-002 — Merchant Verification | 1 | Completed |
| TASK-003 — Catalog Foundations | 2 | Completed |
| TASK-004 — Listings, Variants, Inventory and Moderation | 3 | Completed |
| TASK-005 — Public Marketplace | 4 | Completed |
| TASK-006 — B2C Orders | 5 | Completed |
| TASK-007 — B2B Negotiation | 6 | Completed |
| TASK-008 — B2B Deal and Fulfillment | 7 | Completed |
| TASK-009 — Disputes and Reviews | 8 | Completed |
| TASK-010 — Merchant Analytics and Admin Completion | 9–10 | Completed |

Execute tasks in queue order (`docs/00-SPEC-MAP.md`). Do not start TASK-011 until
explicitly requested.

## Current state

**Phases 9–10 — Merchant Analytics and Admin Completion complete (TASK-010).**

Merchant recovery analytics and the consolidated admin operational screens sit entirely on
top of the finished TASK-001–009 aggregates. No schema change: `dotnet ef migrations
has-pending-model-changes` reports no drift, and no migration was added.

`IMerchantAnalyticsService` (`Services/Analytics/`) recomputes every figure on `Areas/Merchant`'s
new **Analytics** page from the merchant's own authoritative order / deal / listing rows on
each request — nothing is stored and nothing is read from the request
(docs/03-BUSINESS-RULES.md §15, docs/08-SECURITY-AND-PRIVACY.md §6). Recovered value is the
sum of `OrderItem.LineTotalSnapshot` over the merchant's `Completed` orders (B2C) and
`B2BDealLine.LineTotalSnapshot` over its `Completed` deals where it is the seller (B2B); the
delivery-fee snapshot is deliberately excluded (docs/15-GLOSSARY.md "Recovered Value" — value
recovered *from inventory*). Units listed follows the stock-accounting invariant exactly:
`ListingVariant.InitialQuantity + positive InventoryAdjustment.QuantityDelta`; negative
adjustments remain the separate removed-stock side of the invariant. Units sold is the
completed-transaction line quantity (equal to `ListingVariant.SoldQuantity`), and sell-through
is sold units ÷ introduced supply. Average time-to-sale is the sold-unit-weighted duration from
the listing's `PublishedAtUtc` to the completed order/deal's `CompletedAtUtc`; order/deal
creation-to-completion is fulfillment duration and is not used. Cancellation count is
`Cancelled` (and separately `NoShow`) B2C orders plus `Cancelled` B2B deals. Active B2B
negotiations require both `Open` status and a current revision whose `OfferExpiresAtUtc` is
strictly later than now, so an unswept expired offer is not counted. Stale listings are the
merchant's `Live` listings published strictly longer than the exact positive configured
`Analytics:StaleListingThreshold` (default 30 days) ago that have never sold a unit. Invalid,
zero, and negative durations fail options validation at startup; the UI displays the complete
configured duration without rounding it to whole days. The page uses the Faed design-system
stat tiles and tables (no decorative charts,
`.claude/skills/faed-dashboard-ux`) with an explicit empty state for a merchant with no
activity yet (docs/07-UI-UX-SPEC.md §12).

The admin area is consolidated behind a shared `_AdminSubnav` partial (Overview, Merchant
verification, Listing moderation, Orders, B2B deals, Disputes, Catalog, Reviews, Audit log —
docs/07-UI-UX-SPEC.md §7); the three existing admin index/detail views were switched to the
partial. New screens, all behind `FaedPolicies.AdminOnly`:

- **`Areas/Admin/Home`** — the overview: live pending counts across every MVP queue.
- **`Areas/Admin/Transactions`** — read-only B2C order and B2B deal monitoring
  (`IAdminOperationsService`, `Services/Admin/`). An administrator sees the full transaction —
  parties, timeline, line snapshots, money, linked disputes — for support, but the B2C / B2B
  state machines stay with their participants (docs/16-PERMISSIONS-MATRIX.md "monitoring/support").
  No admin mutation of an order or a deal was added. Orders and deals use stable 50-row database
  pages with total counts and filter-preserving Previous/Next navigation; no history is silently
  cut off.
- **`Areas/Admin/Catalog`** — management of the taxonomy, condition grades, discount reasons
  and controlled brands (`IAdminCatalogService`, `Services/Catalog/`). New domain mutators
  (`Category.UpdateDetails`/`SetActive`, `ConditionGrade.UpdateDetails`/`SetActive`,
  `DiscountReason.UpdateDetails`/`SetActive`, `Brand.Rename`/`SetActive`) only touch display
  fields and availability — the stable natural keys (`Code`, `Slug`) the seeder and existing
  listings depend on are never changed, and reference rows are deactivated, never deleted
  (docs/04-DOMAIN-MODEL.md §12). Grade set stays A–D (no add). Every write re-checks the
  `Admin` Identity role in the service and writes an `AdminActionLog` row
  (`CatalogItemCreated` / `CatalogItemUpdated` / `CatalogItemAvailabilityChanged`) in the same
  transaction (docs/08-SECURITY-AND-PRIVACY.md §2, §13), mirroring `ListingModerationService`.
  Category management and merchant listing eligibility are both restricted to descendants of
  the `fashion-overstock` launch root; new sector roots and parents outside that tree are rejected.
  SQL Server unique slug/code races are translated from errors 2601/2627 to controlled `Conflict`
  results, with the failed catalog change and its audit row rolled back together.
- **`Areas/Admin/Reviews`** — read-only monitoring of every verified review. A verified
  review is immutable (TASK-009); no spec defines a review takedown and TASK-009's accepted
  behaviour is that reviews cannot be removed, so this screen is oversight only — an
  administrator follows a problem review up through the merchant's disputes or account status.
  Review history is database-paged rather than truncated.
- **`Areas/Admin/AuditLog`** — a filterable viewer over the append-only `AdminActionLog`
  (docs/04-DOMAIN-MODEL.md §10). `IApplicationDbContext` gained a read-mostly `Users` set so
  the viewer can show which administrator performed each action. The append-only history is
  database-paged and remains reachable beyond the former 200-row boundary.

`AdminActionType` gained `CatalogItemCreated` (13), `CatalogItemUpdated` (14),
`CatalogItemAvailabilityChanged` (15) — the column is `nvarchar(48)` with a value converter,
so new members are not a schema change. `Analytics:StaleListingThreshold` was added to
`appsettings.json`. The merchant sub-navigation gained an **Analytics** tab; the top-nav
"Admin" link now points at the admin **Overview**.

See "TASK-010 — Merchant Analytics and Admin Completion" below.

**Phase 8 — Disputes and Reviews complete (TASK-009).**

Post-transaction trust controls sit on top of the completed B2C order and B2B deal
aggregates with no change to either state machine or its stock handling. A `Dispute`
references exactly one transaction — a B2C `Order` or a B2B `B2BDeal`, enforced by the
`CK_Disputes_ExactlyOneTransaction` check constraint — and carries its own lifecycle
(`Open → UnderReview → Resolved | Rejected`, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10)
that never mutates the order/deal status or its reservation. Only a participant can open one
(`DisputeService` resolves the transaction and rejects a non-participant with a plain "not
found", docs/08-SECURITY-AND-PRIVACY.md §9); administrators cannot file disputes
(docs/16-PERMISSIONS-MATRIX.md). Orders can be disputed only once the merchant has confirmed
them (docs/05 §4 excludes `Pending`); deals only while not `Cancelled`. At most one active
(`Open`/`UnderReview`) dispute exists per transaction at a time — enforced by the database,
not just an application read: `Dispute.ActiveTransactionKey` holds a per-transaction token
while the dispute is active and is `null` once it closes, and `IX_Disputes_ActiveTransactionKey_Unique`
(filtered `WHERE [ActiveTransactionKey] IS NOT NULL`) rejects a second concurrent filing. The
admin lifecycle is strict: an `Open` dispute must be `StartReview`-ed before it can be
`Resolve`-d or `Reject`-ed — the aggregate refuses to close directly from `Open`, and the
admin UI only shows the outcome forms once the dispute is `UnderReview`. `DisputeEvidence`
stores only a protected object key and metadata; the bytes stream from `/dispute-evidence/{id}`
behind `[Authorize]` and are served only to the dispute's participants and to administrators.
A non-participant gets exactly the response a non-existent id gets — `NotFound` — so guessing
ids never confirms which evidence exists; an administrator's access is written to the audit
log (`DisputeEvidenceAccessed`). Every admin decision (`StartReview`, `Resolve`, `Reject`)
re-checks the `Admin` role in the service and writes an `AdminActionLog` row inside the same
transaction as the status change (mirrors `MerchantVerificationService.DecideAsync`); the
`AdminActionLog.Notes` column is `nvarchar(max)` so a full-length resolution
(`Dispute.MaxResolutionLength` = 4000) is recorded complete, never truncated.

A `Review` references exactly one **completed** transaction (`CK_Reviews_ExactlyOneTransaction`,
`CK_Reviews_RatingRange` 1–5). Eligibility — the transaction is `Completed`, the reviewer took
part (the B2C buyer, or the B2B buying merchant's user), and has not already reviewed it — is
enforced in `ReviewService`, and the "one review per transaction" rule is also a filtered
unique index on each transaction FK (`IX_Reviews_OrderId_Unique`,
`IX_Reviews_B2BDealId_Unique`), so a race that passes the pre-check still loses at the
database. Reviews are surfaced on the buyer order detail, the merchant deal detail, a new
merchant "Reviews received" page, and the public merchant storefront (rating average + recent
reviews). The merchant-side dispute flow is symmetric: an eligible **selling merchant** can
open a dispute for a B2C order it sells, through `Merchant/DisputesController` and a "Raise a
dispute" affordance on the merchant order page, subject to the same `DisputeService`
participant/eligibility checks. Transaction detail pages treat only `Open`/`UnderReview`
disputes as "active" (which suppresses a new filing); a closed dispute stays visible as
history and does not block another dispute when the rules allow one.

Migrations `20260903152034_AddDisputesAndReviews` (adds `Disputes` — `rowversion`,
string-valued `Status`/`ReasonCode`, `(Status, CreatedAtUtc)` queue index, `Restrict` FKs to
`Orders`, `B2BDeals` and `AspNetUsers`; `DisputeEvidence` — `Cascade` from its dispute;
`Reviews` — `Restrict` FKs to `MerchantProfiles`, `Orders`, `B2BDeals`, `AspNetUsers`, the two
filtered unique indexes, `(ReviewedMerchantProfileId, CreatedAtUtc)`) and
`20260903162224_HardenDisputeInvariants` (adds `Disputes.ActiveTransactionKey` +
`IX_Disputes_ActiveTransactionKey_Unique`, backfills the key for any already-active dispute,
widens `AdminActionLogs.Notes` to `nvarchar(max)`). `dotnet ef migrations
has-pending-model-changes` reports no drift. See "TASK-009 — Disputes and Reviews" below.

**Phase 7 — B2B Deal and Fulfillment complete (TASK-008).**

Accepting a B2B offer revision now atomically reserves every requested variant and creates a
`B2BDeal` fulfillment record in one transaction (AGENTS.md Rule C, docs/adr/0004). The
accept use case moved from `IB2BNegotiationService` to the new `IB2BDealService`
(`B2BDealService.AcceptOfferAsync`); the negotiation aggregate's own `Accept` transition is
driven from there. `B2BDealService` re-loads the negotiation and listing tracked inside a
transaction, moves the negotiation to `Accepted`, then reserves each `B2BOfferLine`'s variant
(`ListingVariant.Reserve`, protected by its `rowversion`); if any line cannot be reserved the
`DomainException` rolls the whole transaction back, so no variant is reserved, no deal is
created, and the negotiation stays `Open` in the database
(docs/05-USER-FLOWS-AND-STATE-MACHINES.md §6). Acceptance also re-checks that **both**
merchants are still `Approved` and non-admin — a suspension after the negotiation opened
blocks the deal. The listing row is forced into the write set (`Listing.RegisterStockReservation`
on acceptance, `Listing.RegisterStockRelease` on a cancellation/expiry) so a B2B acceptance
or release and a competing B2C order — or a second acceptance — serialize on it. The deal
snapshots the agreed terms only: `AcceptedUnitPriceSnapshot` and `SubtotalSnapshot` (the
accepted revision's server-derived `ProposedTotal`), and `TotalSnapshot` is derived from
`SubtotalSnapshot + (ShippingCostSnapshot ?? 0)` — a caller cannot pass a standalone total.
No shipping charge is agreed during negotiation, so acceptance adds none: `AcceptOfferInput`
is just the `FulfillmentType`.

The `B2BDeal` aggregate owns the fulfillment state machine
(`AwaitingFulfillment → ReadyForPickup|Shipped → Delivered → Completed`, plus `Cancelled`;
`Disputed` is deferred to TASK-009 like the B2C `Order` enum). `B2BFulfillmentType` is
`Pickup` or `SellerArrangedShipping`; the seller records an optional `ShipmentReference`
later, through its own fulfilment steps — a `Pickup` deal may carry neither a reference nor a
shipping cost (Faed neither books nor prices shipping, docs/03-BUSINESS-RULES.md §12). The
deal carries its own `ReservationExpiresAtUtc`, distinct from a revision's `OfferExpiresAtUtc`
(docs/adr/0004): it lapses only while the deal is `AwaitingFulfillment`, is cleared once the
seller starts fulfilling, and `MarkReadyForPickup` / `MarkShipped` refuse synchronously once
it has passed (the deal stays `AwaitingFulfillment` for the sweep to release). The
`B2BDealExpiryService` background worker releases lapsed reservations and cancels the deal
idempotently. Cancellation before delivery releases `Reserved → Available`; completion
(either participant, once `Delivered`) moves `Reserved → Sold`. Seller-only steps
(ready-for-pickup, shipped, shipment reference) are enforced in the service; `Admin` role
holders are excluded at the `CanNegotiateB2B` policy and re-checked in `B2BDealService`.
Migration `20260903141524_AddB2BDeal` adds `B2BDeals` (`rowversion`, string-valued
`Status`/`FulfillmentType`, `decimal(18,3)` money, `CK_B2BDeals_NonNegativeMoney`, unique
`B2BNegotiationId` so an accepted negotiation backs at most one deal, seller/buyer status
indexes, `(Status, ReservationExpiresAtUtc)` sweep index, `Restrict` FKs to `B2BNegotiations`,
`B2BOfferRevisions` and `MerchantProfiles` ×2) and `B2BDealLines` (unique
`(B2BDealId, ListingVariantId)`, `CK_B2BDealLines_PositiveQuantityAndMoney`, `Cascade` from
its deal, `Restrict` to `ListingVariants`). See "TASK-008 — B2B Deal and Fulfillment" below.

**Phase 6 — B2B Negotiation complete (TASK-007).**

> TASK-008 note: acceptance is no longer stock-neutral. `IB2BNegotiationService.AcceptAsync`
> was removed and replaced by `IB2BDealService.AcceptOfferAsync`, which reserves stock and
> creates the `B2BDeal` (see the Phase 7 summary above). The negotiation-only test that
> asserted acceptance reserved no stock was updated accordingly.

Structured merchant-to-merchant offer and counter-offer history sits on top of the
TASK-004 listing aggregate with no change to inventory. A verified buying merchant opens a
`B2BNegotiation` from a Live, B2B-enabled listing with its first `B2BOfferRevision`
(revision 1); the selling merchant and the buyer then strictly alternate — each side may
accept, reject or counter the offer currently on the table, and every counter is a new
immutable revision appended to the history (AGENTS.md Rule C, docs/adr/0004). MOQ is
enforced against `Listing.WholesaleMinQuantity` (per variant, or summed across variants when
`AllowMixedVariantB2B` is set). Each revision carries its own `OfferExpiresAtUtc`, distinct
from a deal's reservation expiry; every participant action synchronously expires a lapsed
current revision before it can be accepted, countered, rejected, or cancelled, while the
`B2BOfferExpiryService` background worker remains the idle-negotiation backstop. Accepting a
revision moves the negotiation to `Accepted` and records
which revision both sides agreed on — **it reserves no stock and creates no fulfillment
record**; the atomic reservation and the `B2BDeal` are TASK-008
(tasks/TASK-007 "No stock is permanently consumed by negotiation alone"). Migration
`20260903121629_AddB2BNegotiation` adds `B2BNegotiations` (`rowversion`, seller/buyer status
indexes), `B2BOfferRevisions` (unique `(negotiation, revision number)`, `decimal(18,3)`
money, `CK_B2BOfferRevisions_NonNegativeMoney`) and `B2BOfferLines` (unique
`(revision, variant)`, `CK_B2BOfferLines_PositiveQuantity`, `Restrict` FK to
`ListingVariants` so negotiation history is never cascade-deleted). See "TASK-007 — B2B
Negotiation" below.

Post-review hardening also excludes `Admin` role holders from B2B participation at both the
MVC policy and service layers, rejects offer unit prices beyond JOD's three-decimal precision
before an immutable revision is created, and prevents removal of variants referenced by B2B
offer history (the merchant receives controlled guidance to deactivate the variant instead).

**Phase 5 — B2C Orders complete (TASK-006).**

Single-merchant consumer ordering with variant-level reservation is implemented on top of
the TASK-004 listing/inventory aggregate and the TASK-005 public read surface. Buyers build
an order from one listing's variants, pick pickup or merchant delivery, and place it;
`OrderService.PlaceOrderAsync` re-loads price and stock server-side inside a transaction,
moves `Available → Reserved` on each `ListingVariant` (protected by its existing
`rowversion`), and creates the `Order` + `OrderItem` snapshots atomically. Cancellation and
the reservation-expiry sweep release `Reserved → Available`; completion — by the merchant, or
by the buyer confirming receipt — moves `Reserved → Sold`. A `ReservationExpiryService`
background worker runs the sweep on a configurable interval; a merchant cannot confirm an
order whose window has already lapsed. Administrators cannot place B2C orders
(`FaedPolicies.CanPlaceB2COrder` + a service recheck). Migration
`20260903113500_AddB2COrders` adds `Orders` (FK to the Identity user on `BuyerUserId`,
`OnDelete Restrict`), `OrderItems`, `MerchantLocations` and `MerchantDeliveryZones`. See
"TASK-006 — B2C Orders" and its "Post-review fixes (Codex review — TASK-006)" below.

**Phase 4 — Public Marketplace complete (TASK-005).**

The anonymous-safe discovery experience (`IPublicMarketplaceService`) sits on top of the
TASK-003/004 catalog and listing aggregate with no schema changes: Home, Shop (filters +
paging), listing detail, and the merchant storefront all read exclusively through queries
scoped to `ListingStatus.Live` and `MerchantVerificationStatus.Approved`. See "TASK-005 —
Public Marketplace" below.

**Phase 3 — Listings, Variants, Inventory and Moderation complete (TASK-004).**

The `Listing` aggregate (options/values, sellable `ListingVariant`s, media, discount
reasons, reference-price evidence, moderation history), variant-level inventory with
`rowversion` concurrency and an `InventoryAdjustment` audit trail, and the merchant
listing workspace / admin moderation queue are implemented on top of the TASK-002/003
foundation. See "TASK-004 — Listings, Variants, Inventory and Moderation" below.

**Architecture restructure complete — single-project organized MVC.**

The former four-project solution (`Faed.Domain` + `Faed.Application` + `Faed.Infrastructure`
+ `Faed.Web`) was consolidated into a single `src/Faed.Web` project organized by folder
(`Models/{Entities,Enums,Identity}`, `Data/{Configurations,Migrations,Seed}`, `Services`,
`Areas`, `ViewModels`, `Authorization`). No behavior, schema, migration IDs or tests
changed — only namespaces and file locations moved (`Faed.Domain.* → Faed.Web.Models.*`,
`Faed.Application.* → Faed.Web.Services.*`, `Faed.Infrastructure.Persistence.* →
Faed.Web.Data.*`). See `docs/adr/0006-SINGLE-PROJECT-MVC.md`. `Faed.Domain`,
`Faed.Application` and `Faed.Infrastructure` were removed from the solution and repository.

**Phase 2 — Catalog Foundations complete (TASK-003).**

DB-driven taxonomy and disclosure reference data: hierarchical `Category`, the
`ConditionGrade` and `DiscountReason` reference tables, an optional admin-controlled
`Brand`, and an idempotent runtime `CatalogDataSeeder` that seeds condition grades A–D,
the eight PRD-approved discount reasons, and the launch taxonomy
(`Fashion Overstock` → Clothing, Shoes, Bags & Accessories). No catalog UI — full admin
catalog management is deferred to TASK-010.

**Phase 1 — Roles and Merchant Verification complete (TASK-002).**

Merchant application, private verification document handling, admin approval/rejection/
suspension, admin audit logging, and the `ApprovedMerchant` / `AdminOnly` authorization
policies are implemented on top of the TASK-001 foundation.

**Phase 0 — Foundation complete (TASK-001).**

The Visual Studio-generated `Faed.Web` baseline was audited and adopted, then the solution
foundation was completed around it.

### Phase 0 baseline audit — result: `PASS`

The Visual Studio baseline (commit `afe6003`, "chore: create MVC Identity baseline") was
correct and adoptable with no blocking issues. The structural changes made afterwards are
the expected TASK-001 Phase 2–3 foundation work, not corrections to a defective baseline.

| Audit item | Finding |
|---|---|
| Project template | ASP.NET Core Web App (Model-View-Controller), `Microsoft.NET.Sdk.Web` |
| Target framework | `net10.0`, nullable + implicit usings enabled |
| Authentication / Identity | Individual Accounts — `AddDefaultIdentity<IdentityUser>` + `Microsoft.AspNetCore.Identity.UI`; `Areas/Identity` present; Register/Login reachable |
| Generated database provider | **SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.11), LocalDB connection string — no SQLite, so no provider migration was needed |
| Solution structure | Repository root = solution root; `src/Faed.Web/`; no nested `Faed/Faed/` |
| Template migration | Legacy `00000000000000_CreateIdentitySchema` in `Faed.Web/Data/Migrations` |
| Build before restructuring | `dotnet build Faed.slnx` — succeeded, 0 warnings, 0 errors |
| Run before restructuring | App started; Home, `/Identity/Account/Register`, `/Identity/Account/Login` all HTTP 200; Identity schema applied to LocalDB |
| Git safety | `bin/`, `obj/`, `.vs/`, `*.user` correctly ignored; no secrets tracked |
| Blocking baseline issues | **None** |

### Post-audit foundation work (TASK-001 Phases 2–5)

> History note: TASK-001/002 used a four-project split; that structure was later
> consolidated into the single `src/Faed.Web` project (see "Current state" and
> `docs/adr/0006-SINGLE-PROJECT-MVC.md`). Paths below reflect the original layout.

EF/Identity moved out of the generated `Faed.Web` root into a persistence layer; user type
extended to `ApplicationUser` (`CreatedAtUtc`, `IsActive`); roles enabled and seeded
idempotently; template migration regenerated as `InitialIdentity`; `IClock` added;
unit + SQL Server integration test projects added. No product/marketplace features were
implemented.

## Active task

None. TASK-010 is closed.

Next: `tasks/TASK-011-HARDENING-AND-DEMO.md` (do not start until explicitly requested).

## TASK-010 — Merchant Analytics and Admin Completion

### Behaviour implemented

- `Services/Analytics/MerchantAnalyticsService` (`IMerchantAnalyticsService`) — one
  `GetForOwnerAsync` that resolves the signed-in user's `MerchantProfile` and returns a
  `MerchantAnalyticsView` computed entirely from `Orders`/`OrderItems`, `B2BDeals`/`B2BDealLines`,
  `Listings`/`ListingVariants` and `B2BNegotiations`. Returns an all-zero view (never null)
  for a user with no merchant profile. Introduced supply is initial quantity plus positive
  inventory adjustments; average time-to-sale is publication-to-completed-sale per sold unit;
  expired current offer revisions are excluded from active negotiations. `AnalyticsOptions`
  holds a validated positive `StaleListingThreshold` (default 30 days), whose exact duration
  drives both strict "older than" query semantics and UI copy.
- `Areas/Merchant/Controllers/AnalyticsController` + `Areas/Merchant/Views/Analytics/Index.cshtml`
  behind `FaedPolicies.ApprovedMerchant` — recovered value (total / B2C / B2B), sell-through
  rate, units sold / listed, retail-vs-wholesale unit split, average days to sale, active
  negotiations, stale listings (with a table), cancelled orders / no-shows / cancelled deals.
  Faed stat tiles + tables, explicit "no analytics yet" empty state. The merchant sub-nav
  gained an **Analytics** tab.
- `Services/Admin/AdminOperationsService` (`IAdminOperationsService`) — read-only projections:
  `GetDashboardAsync` (pending counts), `GetOrdersAsync`/`GetOrderAsync` (B2C monitor + detail),
  `GetDealsAsync`/`GetDealAsync` (B2B monitor + detail), `GetReviewsAsync` (all reviews),
  `GetAuditLogAsync` (filterable audit log). Orders, deals, reviews and audit entries use
  stable 50-row database paging with total counts; records after row 200 remain accessible.
- `Services/Catalog/AdminCatalogService` (`IAdminCatalogService`) — `GetOverviewAsync` plus
  create/update/activate for categories, discount reasons and brands, and update/activate for
  condition grades. Each write: admin-role recheck (`IUserRoleService`), the change and an
  `AdminActionLog` row in one transaction. Category administration and merchant listing
  eligibility share the Fashion Overstock launch-root boundary. Concurrent unique slug/code
  collisions return a controlled `Conflict` and roll back the losing audit row. New entity
  mutators on `Category`, `ConditionGrade`,
  `DiscountReason`, `Brand` (display fields + `IsActive` only; natural keys immutable). New
  `AdminActionType` values `CatalogItemCreated`/`CatalogItemUpdated`/`CatalogItemAvailabilityChanged`.
- `Areas/Admin` controllers (all `FaedPolicies.AdminOnly`): `HomeController` (Overview),
  `TransactionsController` (`Orders`/`OrderDetails`/`Deals`/`DealDetails`), `CatalogController`
  (Index + antiforgery-protected POSTs), `ReviewsController` (Index), `AuditLogController`
  (Index). `Areas/Admin/Views/Shared/_AdminSubnav.cshtml` shared partial; the existing
  MerchantVerification / ListingModeration / Disputes index & detail views were switched to it.
  `Rendering/AdminActivityDisplay` maps `AdminActionType` to plain-English labels.
- `IApplicationDbContext` gained `DbSet<ApplicationUser> Users` (read-mostly) so the audit-log
  viewer and the admin order detail can resolve an actor's / buyer's email.
- `Views/Shared/_Layout.cshtml`: the "Admin" top-nav link now targets `Admin/Home` (Overview).

### Exit-criteria coverage (tasks/TASK-010 "Exit criteria")

| Exit criterion | Covered by |
|---|---|
| Analytics reconcile with known completed transactions | `MerchantAnalyticsServiceTests`: completed-channel reconciliation; replenishment included in introduced supply/sell-through; publication-to-completed-sale timing; expired-current-offer exclusion; exact strict stale threshold boundary |
| Admin can operate all MVP review queues | `Task010HttpTests` (every admin screen: anonymous 401, non-admin 403, admin 200); `AdminOperationsServiceTests` (all monitors surfaced, second-page order/review access, and all 205 audit probes reachable); `AdminCatalogServiceTests` (launch-sector eligibility plus SQL Server slug/code races) |

### Additional coverage

- `Faed.UnitTests.MerchantAnalyticsViewTests` (9) and `AnalyticsOptionsTests` (4) — derived
  roll-ups, exact duration copy, positive-duration validation, and malformed-duration binding.
- `Faed.IntegrationTests.MerchantAnalyticsServiceTests` (7, SQL Server) — reconciliation,
  introduced-supply/sell-through, publication-based weighted time-to-sale, unswept offer
  expiry, no-profile zero view, and stale threshold boundary behavior.
- `Faed.IntegrationTests.AdminOperationsServiceTests` (4, SQL Server) — dashboard/all monitors,
  unknown ids, second-page order/review history, and access beyond the former 200-row audit cap.
- `Faed.IntegrationTests.AdminCatalogServiceTests` (7, SQL Server) — authorization/auditing,
  launch-tree administration and merchant eligibility, and deterministic concurrent brand-slug
  and discount-code collisions returning `Conflict` with exactly one committed row/audit entry.
- `Faed.IntegrationTests.Task010HttpTests` (15 incl. theory rows) — route authorization for
  every admin screen and the merchant Analytics page, antiforgery on an admin POST, and the
  Analytics page rendering for an approved merchant.
- The three existing public-marketplace launch-boundary tests still verify that even a legacy
  or directly persisted out-of-sector `Live` listing is absent from browse/detail HTTP surfaces;
  their fixture now bypasses the newly hardened merchant write path directly.

### Not implemented (correctly deferred / out of scope)

- Any admin mutation of a B2C order or a B2B deal — TASK-010 is "order/deal **monitoring**";
  the state machines stay with their participants (docs/16-PERMISSIONS-MATRIX.md).
- A review takedown / hide — no spec defines one and TASK-009's accepted behaviour is that a
  verified review is immutable; the admin Reviews screen is monitoring only. A safe,
  reversible assumption: if the product owner later wants a takedown it is a small additive
  migration (`Review.RemovedByAdminId` + a filtered public query), not a TASK-010 gap.
- Precomputed / cached analytics aggregates — explicitly deferred by
  docs/03-BUSINESS-RULES.md §15 ("may be introduced later if needed").
- The demo transactional seed scenarios in docs/12-SEED-DATA.md — TASK-011.

### Validation (TASK-010)

- `dotnet build Faed.slnx --no-restore -c Release -p:UseAppHost=false` — succeeds, 0 warnings,
  0 errors. (`UseAppHost=false` avoids replacing the Debug executable held by the developer's
  already-running Faed process.)
- Focused TASK-010 tests — **46 passed** (13 unit + 33 SQL Server integration), 0 failed,
  0 skipped; the three directly affected public launch-boundary regressions also pass.
- `dotnet test Faed.slnx --no-restore -c Release -p:UseAppHost=false` — **428 passed
  (247 unit, 181 SQL Server integration)**, 0 failed, 0 skipped.
- `dotnet ef migrations has-pending-model-changes --project src/Faed.Web --startup-project
  src/Faed.Web --configuration Release --no-build` — no model drift. **No migration was added**
  for these fixes.

## TASK-009 — Disputes and Reviews

### Behaviour implemented

- `Models/Entities/Dispute` (+ `DisputeEvidence`) — the complaint aggregate
  (docs/04-DOMAIN-MODEL.md §9). Constructor requires exactly one of `OrderId` / `B2BDealId`;
  guarded transitions `StartReview` / `Resolve` / `Reject` require an administrator id and
  the right prior status, and `AddEvidence` is refused once the dispute is closed. The
  aggregate holds no transaction state — resolving a dispute is an administrative record, not
  a fulfilment transition, so the B2C `Order` and B2B `B2BDeal` state machines and their
  stock handling are unchanged by this phase.
- `Models/Entities/Review` — rating (1–5, validated in the constructor and by
  `CK_Reviews_RatingRange`), optional comment, exactly one transaction reference.
- `Models/Enums`: `DisputeStatus` (`Open`, `UnderReview`, `Resolved`, `Rejected`),
  `DisputeReasonCode` (`ItemNotAsDescribed`, `UndisclosedDefect`, `MissingItems`,
  `ItemNotReceived`, `WrongItem`, `Other`), `TrustTransactionType` (`B2COrder`, `B2BDeal`).
  `AdminActionType` gained `DisputeReviewStarted`, `DisputeResolved`, `DisputeRejected`,
  `DisputeEvidenceAccessed`.
- `Services/Trust/DisputeService` (`IDisputeService`) — `FileDisputeAsync` resolves the
  transaction, rejects a non-participant with `NotFound` (IDOR), refuses administrators
  (`Forbidden`), refuses a `Pending` order / `Cancelled` deal, refuses a second active
  dispute, validates and stores evidence (buffered and scanned by
  `ListingImageValidator.ValidatePayload` before anything is written; orphaned blobs are
  cleaned up on a persistence failure). `OpenEvidenceAsync` serves the bytes only to a
  participant or an administrator and audits an administrator's access. The admin workflow
  (`StartReviewAsync` / `ResolveAsync` / `RejectAsync`) re-checks the `Admin` role and writes
  an `AdminActionLog` row in the same transaction as the status change.
- `Services/Trust/ReviewService` (`IReviewService`) — `SubmitReviewAsync` enforces
  completed-transaction + participant + not-already-reviewed + not-admin; the filtered unique
  index is the race backstop and a `DbUpdateException` is translated to a friendly conflict.
  `GetEligibilityAsync` drives the "leave a review" UI; `GetMerchantReviewsAsync` /
  `GetReviewsForOwnerAsync` produce the rating summary and recent reviews.
- `Services/Trust/TrustOptions` (`Trust` config section) — `MaxEvidenceFilesPerDispute` (8),
  `MaxEvidenceBytes` (10 MB). `Program.cs` folds `MaxEvidenceBytes` into the multipart body
  limit alongside the verification-document and listing-image caps.
- Controllers / views:
  - `Areas/Buyer/Controllers/DisputesController` (list, create-from-order, detail,
    add-evidence) and a `Review` POST on `Areas/Buyer/Controllers/OrdersController` with the
    review/dispute panels rendered on the buyer order detail.
  - `Areas/Merchant/Controllers/DisputesController` (list, create-from-deal, detail,
    add-evidence), `Areas/Merchant/Controllers/ReviewsController` ("Reviews received"), and a
    `Review` POST on `DealsController` with the panels on the deal detail. The merchant
    sub-navigation gained "Disputes" and "Reviews".
  - `Areas/Admin/Controllers/DisputesController` — the dispute queue (filter tabs, open
    count), review detail with parties / transaction / evidence links, and the
    start-review / resolve / dismiss POSTs. The admin sub-navigation gained "Disputes".
  - `Controllers/DisputeEvidenceController` — `[Authorize]` `/dispute-evidence/{id}`,
    served as an attachment with `no-store`, mirroring the verification-document endpoint.
  - `StoreController` now shows the merchant's rating average and recent reviews on the
    public storefront (docs/07-UI-UX-SPEC.md §4). The top nav gained "My Disputes" for
    signed-in users.
- Migration `20260903152034_AddDisputesAndReviews` — see the Phase 8 summary above.

### Exit-criteria coverage (tasks/TASK-009 "Exit criteria")

| Exit criterion | Covered by |
|---|---|
| Only participants can dispute | `TrustServiceTests.FileDispute_ByTheBuyer_Succeeds_ButByANonParticipant_RevealsNothing`, `…FileDispute_ByAnAdministrator_IsForbidden`; `TrustHttpTests` (anonymous challenged, admin forbidden on `/Buyer/Disputes`) |
| Review requires Completed transaction | `TrustServiceTests.Review_RequiresACompletedTransaction` (a confirmed-but-not-completed order is rejected; the same order after completion is accepted) |
| Duplicate review is blocked | `TrustServiceTests.DuplicateReview_IsBlocked` (second submit → `Conflict`, exactly one row); unit `ReviewTests` for the aggregate |
| Admin resolution is audited | `TrustServiceTests.AdminResolution_MovesTheDisputeToResolved_AndIsWrittenToTheAuditLog` (both `DisputeReviewStarted` and `DisputeResolved` rows), `…DisputeDecisions_ByANonAdministrator_AreForbidden_EvenAtTheServiceLayer` |
| Public/private evidence permissions are correct | `TrustServiceTests.DisputeEvidence_IsPrivate_ToParticipantsAndAdministrators` (stranger `Forbidden`; buyer, selling merchant and admin all succeed; admin access audited); `TrustHttpTests` (anonymous challenged, guessed id not revealed) |

### Additional coverage

- `Faed.UnitTests.DisputeTests` (12) and `Faed.UnitTests.ReviewTests` (9) — the two
  aggregates' construction and transition rules.
- `Faed.IntegrationTests.TrustServiceTests` (11, SQL Server) — the five exit criteria plus
  the pending-order and one-active-dispute rules, the B2B deal review path (buying merchant
  only, seller rejected, rating summary), and non-participant review rejection.
- `Faed.IntegrationTests.TrustHttpTests` (8) — route authorization for the buyer, merchant
  and admin dispute/review surfaces and the evidence endpoint, plus render checks for the
  buyer dispute list and the admin dispute queue.

### Not implemented (correctly deferred)

- A `Disputed` status on `OrderStatus` / `B2BDealStatus` and any freeze of the underlying
  transaction while a dispute is open — TASK-009's deliverables and exit criteria are a
  separate `Dispute` record with its own lifecycle, and adding an order/deal status with
  stock-freeze semantics would change previously accepted behaviour. The `OrderStatus` /
  `B2BDealStatus` doc comments were updated to say the dispute is modelled as its own
  aggregate rather than as a status.
- Admin transaction-monitoring screens, the admin reviews screen and the audit-log viewer —
  TASK-010 (docs/10-IMPLEMENTATION-PLAN.md Phase 10).
- B2B reviews beyond the buying-merchant-reviews-seller direction, and any merchant reply to
  a review — not in scope for the MVP (docs/03-BUSINESS-RULES.md §13).

### Post-review fixes (Codex review — TASK-009)

A review of the initial TASK-009 implementation raised six blocking findings. All are fixed,
scoped to TASK-009; the schema changes are folded into a new
`20260903162224_HardenDisputeInvariants` migration (nothing committed or deployed).

- **The one-active-dispute rule was not concurrency-safe.** `DisputeService.FileDisputeAsync`
  did an `AnyAsync` pre-check, so two simultaneous filings for the same order or deal could
  both pass it and both insert. New `Dispute.ActiveTransactionKey` (`"O:"`/`"D:"` +
  `Guid.ToString("N")`, held while `Open`/`UnderReview`, cleared on close) is backed by the
  filtered unique index `IX_Disputes_ActiveTransactionKey_Unique`; the concurrent loser's
  insert is rejected by the database and translated to a clean `Conflict`. Covered by the
  deterministic `TrustServiceTests.TwoConcurrentFilings_ForTheSameOrder_OnlyOneSucceeds`
  (SQL Server interleave via `GatedApplicationDbContext`: the gated call pauses immediately
  before its INSERT, the competing call commits, the gated call then conflicts on the index).
- **An `Open` dispute could be resolved or rejected in one step.** `Dispute.Close` accepted
  `Open` or `UnderReview`; it now requires `UnderReview`, so an administrator must
  `StartReview` first (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10). `AdminDisputeDetailView.CanClose`
  and the admin detail view were updated to match. Covered by
  `DisputeTests.An_open_dispute_cannot_be_resolved_or_rejected_directly` and
  `TrustServiceTests.AnOpenDispute_CannotBeResolvedOrRejectedDirectly` (both `ResolveAsync`
  and `RejectAsync` on an `Open` dispute return `Conflict`, the status stays `Open`, and no
  `DisputeResolved`/`DisputeRejected` audit row is written; the two-step path then works).
- **Resolution text could exceed the audit column.** `Dispute.MaxResolutionLength` is 4000 but
  `AdminActionLog.Notes` was `nvarchar(2000)`, so a valid long resolution would fail the save
  (breaking the atomic status-change + audit write). `AdminActionLog.Notes` is now
  `nvarchar(max)`; a full-length resolution is stored on the dispute **and** recorded complete
  on the audit log, in one transaction. Covered by
  `TrustServiceTests.AResolutionAtTheDocumentedMaxLength_PersistsWithItsCompleteAuditEntry`.
- **The evidence endpoint leaked id existence.** `OpenEvidenceAsync` returned `Forbidden` for
  a non-participant hitting a real evidence id but `NotFound` for a missing one. It now
  returns `NotFound` for both, so guessing ids never confirms which evidence exists;
  legitimate participant/admin access and the admin audit entry are unchanged. Covered by the
  updated `TrustServiceTests.DisputeEvidence_IsPrivate_ToParticipantsAndAdministrators` (a
  stranger's hit and miss both return `NotFound`) and the existing HTTP test.
- **Transaction detail pages treated closed disputes as active.** The buyer order, merchant
  deal and merchant order detail controllers matched any dispute on the transaction, so a
  resolved dispute was shown as active and wrongly suppressed a new filing. They now split on
  `DisputeSummaryView.IsActive`: an `Open`/`UnderReview` dispute is the active one (and
  suppresses "raise a dispute"), closed disputes render as a history list and never block a
  new filing. Covered by `TrustServiceTests.AfterADisputeIsClosed_AFreshDisputeMayBeFiledForTheSameOrder`
  and `TrustHttpTests.AfterADisputeIsResolved_TheOrderPageOffersANewDispute_AndKeepsTheClosedOneAsHistory`.
- **The merchant-side B2C dispute flow was missing.** `DisputeService` already permitted a
  selling merchant to dispute a B2C order, but there was no HTTP/UI path.
  `Merchant/DisputesController.Create` now takes `type` + `id` and covers both B2C orders and
  B2B deals (verifying participation via `IOrderService.GetMerchantOrderAsync` /
  `IB2BDealService.GetDealAsync`, 404 otherwise); the merchant order detail page carries the
  "Raise a dispute" affordance and the active/past-dispute panel. Covered by
  `TrustServiceTests.FileDispute_ByTheSellingMerchant_Succeeds` and
  `TrustHttpTests.SellingMerchant_CanReachTheB2COrderDisputeForm_ButAnUnrelatedMerchantCannot`.

### Validation (TASK-009, after the fix pass)

- `dotnet build Faed.slnx` (Debug + Release) — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **382 passed (234 unit, 148 integration)**, 0 failed, 0 skipped,
  on a workstation with SQL Server LocalDB reachable. The fix pass added 3 unit + 7 integration
  regression tests and updated a handful of existing dispute tests in place to assert the
  corrected state-machine and evidence-privacy behaviour; no existing test was deleted or
  weakened.
- `20260903152034_AddDisputesAndReviews` and `20260903162224_HardenDisputeInvariants` apply
  incrementally from the existing schema (`dotnet ef database update`), the web integration
  host recreates the database from empty every run, and
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- SQL Server concurrency: the one-active-dispute-per-transaction invariant is proven against
  real SQL Server via a deterministic interleave, not InMemory/SQLite
  (docs/09-TEST-STRATEGY.md §2).
- Targeted verification only, scoped to the six findings and the regressions their fixes could
  cause — no fresh broad review (per the task instruction).

## TASK-008 — B2B Deal and Fulfillment

### Behaviour implemented

- `Models/Entities/B2BDeal` + `B2BDealLine` — the accepted-deal aggregate
  (docs/04-DOMAIN-MODEL.md §8, docs/adr/0004). `B2BDeal` owns the fulfillment state machine as
  guarded transitions (`MarkReadyForPickup` requires `Pickup`; `MarkShipped` requires
  `SellerArrangedShipping`; `MarkDelivered` from `ReadyForPickup`/`Shipped`; `Complete` only
  from `Delivered`; `Cancel` only before delivery), never a status assigned from input. It
  holds no stock: it records the transition and the deal service moves the variant quantities
  in the same transaction. `MarkReadyForPickup` / `MarkShipped` also refuse an already-lapsed
  reservation. Money is snapshotted at creation (`AcceptedUnitPriceSnapshot`, `SubtotalSnapshot`,
  `ShippingCostSnapshot` — nullable, docs/04-DOMAIN-MODEL.md §8, never populated in this phase)
  and never recomputed from the listing; the constructor **derives** `TotalSnapshot` from
  `SubtotalSnapshot + (ShippingCostSnapshot ?? 0)` rather than accepting a standalone total,
  and rejects a shipment reference or shipping cost on a `Pickup` deal. `B2BDealLine` stores
  an immutable unit-price and variant-combination snapshot; `LineTotalSnapshot` is
  server-derived.
- `Models/Enums/B2BDealStatus` (`AwaitingFulfillment`, `ReadyForPickup`, `Shipped`,
  `Delivered`, `Completed`, `Cancelled` — `Disputed` deferred to TASK-009) and
  `B2BFulfillmentType` (`Pickup`, `SellerArrangedShipping`).
- `Services/B2B/B2BDealService` (`IB2BDealService`) — the only path that accepts an offer or
  transitions a deal. `AcceptOfferAsync` opens a transaction, re-loads the negotiation
  (revisions + lines) and listing (variants) tracked, synchronously expires a lapsed offer,
  calls `B2BNegotiation.Accept`, then reserves every `B2BOfferLine`'s variant; a
  `DomainException` (stale `rowversion` or insufficient stock) returns `Conflict` and the
  transaction rolls back — **all lines reserve atomically or none do**, and the negotiation
  stays `Open` in the database (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §6,
  docs/17-DATA-INVARIANTS.md "Inventory for all deal lines reserves atomically or not at
  all"). Acceptance also re-checks that **both** merchants are still `Approved` and non-admin.
  `Listing.RegisterStockReservation` (acceptance) and `Listing.RegisterStockRelease`
  (cancellation/expiry) force the listing row into the write set so a B2B acceptance or
  release serializes against a competing B2C reservation or a second acceptance on a
  different variant of the same listing. Fulfilment transitions pair each status change with
  its stock movement (`Cancel` → release, `Complete` → confirm sale) in one transaction;
  seller-only steps are enforced in the service; every read/action re-resolves the caller's
  approved merchant and re-checks participation, so a guessed deal id reveals nothing
  (docs/08-SECURITY-AND-PRIVACY.md §9).
  `ReleaseExpiredDealReservationsAsync` releases lapsed `AwaitingFulfillment` reservations and
  cancels the deal; idempotent — a second run is a no-op
  (docs/17-DATA-INVARIANTS.md "Reservation release is idempotent").
- `Services/B2B/B2BDealExpiryService` — a hosted `BackgroundService` on the configurable
  `B2BDeal:ExpirySweepInterval`; not hosted under the `Testing` environment
  (docs/09-TEST-STRATEGY.md §1).
- `Services/B2B/B2BDealOptions` (`B2BDeal` config section) — `ReservationWindow` (default
  7 days, the reversible default for docs/13-OPEN-QUESTIONS.md §15) and `ExpirySweepInterval`.
  Durations live in configuration, never as domain constants.
- `IB2BNegotiationService.AcceptAsync` was **removed**; `B2BNegotiationService` keeps
  reject/cancel/counter. `B2BNegotiationDetailView` gained `DealId` so the offer detail page
  links straight to the fulfilment record once accepted.
- Controllers / views: `Areas/Merchant/Controllers/DealsController` (behind
  `FaedPolicies.CanNegotiateB2B`) — the deal queue with filters, per-deal detail, and the
  fulfilment POSTs (ready-for-pickup, shipped, shipment-reference, delivered, complete,
  cancel). `OffersController.Accept` takes only a `B2BFulfillmentType`, calls
  `B2BDealService.AcceptOfferAsync`, and redirects to the deal on success. Views `Merchant/Deals/{Index,Details}` use the Faed design system
  (filter tabs, `scope`-annotated tables in `overflow-x` wrappers, non-colour status badges
  via `Rendering/B2BDealStatusDisplay`, `role="status"`/`role="alert"` messaging,
  `<fieldset>`/`<legend>` groups). The merchant sub-navigation gained a "B2B Deals" tab; the
  offer detail's "Accept" panel gained the fulfilment-type choice.
- Migration `20260903141524_AddB2BDeal` — see the Phase 7 summary above.
  `dotnet ef migrations has-pending-model-changes` reports no drift.

### Mandatory-test coverage (tasks/TASK-008 "Mandatory tests")

| Mandatory test | Covered by |
|---|---|
| All requested variants reserve atomically or none do | `B2BDealServiceTests.AcceptOffer_ReservesEveryLineAtomically_AndCreatesTheDeal` (multi-line reserve) and `AcceptOffer_WhenOneLineCannotReserve_ReservesNothing_AndLeavesTheNegotiationOpen` (SQL Server — one short line → no reservation, no deal, negotiation `Open`) |
| B2C vs B2B competition is safe | `B2BDealServiceTests.AB2COrderAndAB2BAcceptance_CompetingForTheLastUnits_AreSafe` — deterministic interleave via `GatedApplicationDbContext`: the B2C order commits inside the acceptance's pre-write gate; the acceptance loses on the moved `rowversion`, no oversell, no deal |
| Two B2B accept attempts cannot oversell | `B2BDealServiceTests.TwoB2BAcceptances_CompetingForTheSameStock_CannotOversell` — deterministic interleave; exactly one deal, final `Available = 0` / `Reserved = 10` |
| Repeated expiry processing does not double-release | `B2BDealServiceTests.ExpiredDealReservation_IsReleasedByTheSweep_ExactlyOnce` (first sweep 1, second 0; stock released once; deal `Cancelled`) |
| Completion moves Reserved → Sold | `B2BDealServiceTests.Completion_MovesReservedStockToSold` |

### Additional coverage

- `Faed.UnitTests.B2BDealTests` (17) — the aggregate: starts `AwaitingFulfillment` with lines
  and a reservation window; same-merchant deal rejected; duplicate variant line rejected;
  pickup and shipping happy paths through to `Completed`; wrong-type transitions rejected;
  `SetShipmentReference` only for shipping deals and requires a value; `Complete` before
  delivery rejected; `Cancel` allowed before delivery only; cancel clears the reservation
  window and records the reason; the total is derived from the subtotal plus any shipping
  cost (not a caller-supplied total); a `Pickup` deal cannot carry a shipment reference or a
  shipping cost; `MarkReadyForPickup` / `MarkShipped` reject an already-lapsed reservation.
- `Faed.IntegrationTests.B2BDealServiceTests` (15, SQL Server) — the five mandatory tests plus
  cancellation releasing reserved stock, seller-arranged-shipping storing the shipment
  reference (and the buyer being forbidden the seller's steps), a non-participant merchant
  finding the deal invisible and untouchable, and the four post-review regressions (both
  merchants re-checked on acceptance, expired deal cannot advance to fulfilment, B2B release
  vs B2C reservation on different variants stays consistent, acceptance snapshots exactly the
  agreed terms).
- `Faed.IntegrationTests.B2BDealHttpTests` (4) — the deal queue challenges an anonymous
  request, is forbidden to a user without an approved merchant profile and to an approved
  merchant with the `Admin` role, and the detail route renders for both participants while an
  unrelated merchant gets 404.
- `B2BNegotiationServiceTests` was updated for the relocated accept path: acceptance now
  asserts the negotiation moves to `Accepted`, the stock is reserved and a `B2BDeal` exists
  (`AcceptingAnOffer_MovesTheNegotiationToAccepted_AndTheDealReservesTheStock`).

### Not implemented (correctly deferred)

- B2B reviews and disputes, the `Disputed` deal status and the dispute path from a deal — all
  TASK-009 (docs/10-IMPLEMENTATION-PLAN.md Phase 8). No such code was scaffolded.
- B2B analytics / recovered-value from completed deals — TASK-010.
- Admin B2B deal monitoring screens — TASK-010 (docs/07-UI-UX-SPEC.md §7).
- Faed booking or pricing shipping — out of scope by docs/03-BUSINESS-RULES.md §12. The
  `B2BDeal.ShippingCostSnapshot` column exists (docs/04-DOMAIN-MODEL.md §8) but is never
  populated in this phase; only a seller-entered `ShipmentReference` is recorded, via the
  seller's own fulfilment steps. There is no negotiated or acceptance-time shipping charge.

### Post-review fixes (Codex review — TASK-008)

A review of the initial TASK-008 implementation raised four blocking findings. All are fixed,
scoped to TASK-008; no schema change was needed (all four are logic / constructor-signature
changes) so `20260903141524_AddB2BDeal` is unchanged.

- **Acceptance did not re-check that both merchants were still eligible to trade.**
  `B2BDealService.AcceptOfferAsync` verified only the caller. A seller or buyer suspended
  (or made an administrator) after the negotiation opened could still be pulled into a newly
  created, stock-reserving `B2BDeal` (docs/03-BUSINESS-RULES.md §1,
  docs/16-PERMISSIONS-MATRIX.md). New `CounterpartyIneligibleAsync` loads both merchant
  profiles inside the acceptance transaction and refuses the deal unless **both** are
  `Approved` and neither holds the `Admin` role — mirroring `OrderService` re-checking the
  selling merchant at `PlaceOrderAsync`. Covered by
  `B2BDealServiceTests.AcceptOffer_WhenTheSellingMerchantHasBeenSuspended_IsRejected_AndReservesNothing`
  and `…WhenTheBuyingMerchantHasBeenSuspended_IsRejected` (SQL Server — no deal row, negotiation
  stays `Open`, no stock reserved).
- **An expired deal reservation could be advanced to a fulfilment state.** `B2BDeal.MarkReadyForPickup`
  / `MarkShipped` checked only the status, so between a deal's window lapsing and the sweep
  running, the seller could advance it — which clears `ReservationExpiresAtUtc` and holds the
  stock indefinitely on a passed deadline (the same defect `Order.Confirm` was hardened
  against in TASK-006). Both transitions now reject when `ReservationExpiresAtUtc <= nowUtc`;
  the deal stays `AwaitingFulfillment` and `ReleaseExpiredDealReservationsAsync` releases the
  stock exactly once and cancels it. Covered by `B2BDealTests.MarkReadyForPickup_WhenTheReservationHasAlreadyExpired_*`,
  `MarkShipped_WhenTheReservationHasAlreadyExpired_*` and
  `B2BDealServiceTests.AdvancingAnExpiredDealToFulfillment_IsRejected_AndTheSweepThenReleasesTheStockExactlyOnce`
  / `…SellerArrangedShippingDealToShipped_IsAlsoRejected` (SQL Server).
- **A B2B stock release racing a B2C reservation on a different variant could leave the
  listing status wrong.** `B2BDealService.ApplyTransitionAsync` refreshed listing availability
  from its loaded variants but never forced the listing row into the write set, so a deal
  cancellation/expiry releasing variant X and a concurrent B2C order depleting variant Y each
  committed against a stale view — leaving the listing wrongly `SoldOut` (or wrongly `Live`).
  New `Listing.RegisterStockRelease(nowUtc)` (sibling of the TASK-006 `RegisterStockReservation`)
  always advances the listing `RowVersion`; `ApplyTransitionAsync` calls it for every affected
  listing on a `Release` effect, so releases and reservations now serialize on the listing row
  and the loser re-reads. Covered by the deterministic
  `B2BDealServiceTests.AB2BReleaseRacingAB2CReservation_OnDifferentVariants_KeepsTheListingStatusConsistent`
  (real SQL Server interleave via `GatedApplicationDbContext`: the B2C order commits inside the
  release's pre-write gate; the release conflicts on the listing rowversion, retries, and the
  listing ends `Live` and consistent).
- **Acceptance could inject unagreed shipping charges and contradictory fulfilment data.**
  `AcceptOfferInput` carried a `ShipmentReference` and a `ShippingCost` that the accepting
  merchant (who may be the buyer) could set; the charge was added to the deal total and the
  reference was stored even for a `Pickup` deal. Nothing about shipping is agreed during
  negotiation (docs/03-BUSINESS-RULES.md §12, docs/04-DOMAIN-MODEL.md §7). `AcceptOfferInput`
  is now just `FulfillmentType`. The `B2BDeal` constructor derives `TotalSnapshot` from
  `SubtotalSnapshot + (ShippingCostSnapshot ?? 0)` — a caller cannot pass a standalone total —
  uses the accepted revision's server-derived `ProposedTotal` as the subtotal, and rejects a
  shipment reference or shipping cost on a `Pickup` deal outright. The shipment reference is
  recorded later, only by the selling merchant, through the already-seller-only
  `MarkShipped` / `SetShipmentReference` transitions. `B2BDealOptions.MaxShippingCost` (now
  unused) was removed. Covered by `B2BDealTests.Ctor_DerivesTheTotalFromTheSubtotalPlusAnyShippingCost_*`,
  `Ctor_Pickup_WithAShipmentReferenceOrAShippingCost_IsRejected` and
  `B2BDealServiceTests.AcceptOffer_SnapshotsExactlyTheAgreedTerms_AndAddsNoShippingCharge`
  (SQL Server — subtotal = accepted `ProposedTotal`, total = subtotal, shipping and reference
  null); the pre-existing `SellerArrangedShipping_StoresTheShipmentReference` test was updated
  to assert the total is the subtotal alone and still checks the buyer is forbidden the
  seller's steps.

### Validation (TASK-008, after the fix pass)

- `dotnet build Faed.slnx` (Debug + Release) — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **335 passed (212 unit, 123 integration)**, 0 failed, 0 skipped,
  on a workstation with SQL Server LocalDB reachable (net of 4 new unit + 6 new integration
  regression tests; no existing test was deleted or weakened — two were updated in place to
  assert the corrected behaviour).
- `20260903141524_AddB2BDeal` is unchanged (no schema change in the fix pass); it applies
  incrementally from the existing schema (`dotnet ef database update`), the web integration
  host recreates the database from empty every run, and
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- SQL Server concurrency: the atomic-reservation, two-acceptance, B2C-vs-B2B-acceptance and
  the new B2B-release-vs-B2C-reservation races all run deterministic interleaves against real
  SQL Server, not InMemory/SQLite (docs/09-TEST-STRATEGY.md §2).
- Targeted verification only, scoped to the four findings and the regressions their fixes
  could cause — no fresh broad review (per the task instruction).

## TASK-007 — B2B Negotiation

### Behaviour implemented

- `Models/Entities/B2BNegotiation` — the negotiation aggregate (AGENTS.md Rule C,
  docs/04-DOMAIN-MODEL.md §7). Owns an append-only list of `B2BOfferRevision`s through a
  backing field; the constructor requires the buying merchant's first offer and rejects a
  negotiation whose buying and selling merchant are the same
  (docs/17-DATA-INVARIANTS.md "Selling and buying merchants cannot be the same merchant").
  `Counter` / `Accept` / `Reject` are guarded transitions: they require the negotiation to be
  `Open` and require the caller to be the merchant the current offer is addressed to — the
  side that did *not* propose it — so a merchant can neither accept its own offer nor counter
  twice in a row, and the two sides strictly alternate
  (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §5). Every participant transition refuses once
  `CurrentRevision.OfferExpiresAtUtc` has passed and synchronously moves the negotiation to
  `Expired` (docs/17-DATA-INVARIANTS.md "Only the active non-expired revision can be
  accepted"). `Cancel` lets either participant withdraw from an unexpired open negotiation.
  `ExpireIfLapsed` also supports the idempotent background sweep. MOQ
  (docs/03-BUSINESS-RULES.md §11) is enforced in the
  aggregate: the summed line quantity when `AllowMixedVariantB2B` is set, otherwise each
  line independently.
- `Models/Entities/B2BOfferRevision` + `B2BOfferLine` — immutable proposal records
  (docs/17-DATA-INVARIANTS.md "Previous revisions are immutable"). No mutators; created once
  by `B2BNegotiation`. `ProposedTotal` is derived server-side from `ProposedUnitPrice ×`
  summed line quantities — never accepted from input
  (docs/08-SECURITY-AND-PRIVACY.md §7). `RevisionNumber` is unique per negotiation, backed by
  a database unique index.
- `Services/B2B/B2BNegotiationService` (`IB2BNegotiationService`) — the only path that
  creates or transitions a negotiation. `StartNegotiationAsync` resolves the buyer's approved
  merchant, loads the listing by slug, and rejects the offer unless the listing is `Live`,
  `AllowB2B`, owned by a currently-`Approved` merchant, and not the buyer's own
  (docs/16-PERMISSIONS-MATRIX.md "Submit B2B offer — verified merchant only"). `CounterOfferAsync`
  re-loads the negotiation, refuses a caller who is not a participant with a plain "not found"
  (IDOR, docs/08-SECURITY-AND-PRIVACY.md §9), and lets the aggregate enforce the alternation
  and MOQ rules. `Accept`/`Reject`/`Cancel` follow the same shape. `GetNegotiationAsync`
  returns `null` for a non-participant, so a guessed id reveals nothing
  (docs/16-PERMISSIONS-MATRIX.md "View unrelated B2B negotiation — ❌"). All monetary totals
  and the offer-expiry timestamp are computed server-side; nothing here is trusted from the
  request. Variant labels for the history are resolved at read time from the listing options
  (the offer line only stores the variant id, matching docs/04-DOMAIN-MODEL.md §7).
- `Services/B2B/B2BOfferExpiryService` — a hosted `BackgroundService` that runs
  `IB2BNegotiationService.ExpireLapsedNegotiationsAsync` on the configurable
  `B2BNegotiation:ExpirySweepInterval`. Idempotent (a closed negotiation is skipped); not
  hosted under the `Testing` environment — the tests drive expiry through the service and a
  fake clock (docs/09-TEST-STRATEGY.md §1).
- `Services/B2B/B2BNegotiationOptions` (`B2BNegotiation` config section) —
  `DefaultOfferValidity` (3 days, the reversible default for docs/13-OPEN-QUESTIONS.md §14),
  `Min`/`MaxOfferValidity`, `ExpirySweepInterval`, `MaxOfferLineQuantity`, `MaxOfferLines`.
  Durations live in configuration, never as domain constants.
- Controllers / views: `Areas/Merchant/Controllers/OffersController` (behind the
  `FaedPolicies.CanNegotiateB2B` policy — an approved merchant who is **not** an
  administrator; `B2BNegotiationService` re-checks the caller's Identity role on every write
  and private read, so an approved merchant profile with the `Admin` role cannot slip past
  the MVC policy) — the negotiation queue, per-negotiation detail with the full
  revision history, the "make an offer" builder (`Create`), and the counter/accept/reject/
  cancel POSTs. Views `Merchant/Offers/{Index,Details,Create}` use the Faed design system
  (filter tabs, `scope`-annotated tables inside `overflow-x` wrappers, non-colour status
  badges via `Rendering/B2BNegotiationStatusDisplay`, `role="status"`/`role="alert"`
  messaging, `<fieldset>`/`<legend>` quantity groups). The merchant sub-navigation gained a
  "B2B Offers" tab, and the public listing detail's "Make an Offer" button now links to the
  offer builder instead of being a disabled placeholder.
- Migration `20260903121629_AddB2BNegotiation` — `B2BNegotiations` (string-valued `Status`,
  `rowversion`, indexes on `(SellingMerchantProfileId, Status)`, `(BuyingMerchantProfileId,
  Status)` and `ListingId`; `Restrict` FKs to `Listings` and to `MerchantProfiles` ×2 so a
  populated listing or merchant can never take negotiation history with it,
  docs/04-DOMAIN-MODEL.md §12). `B2BOfferRevisions` — unique `(B2BNegotiationId,
  RevisionNumber)`, `decimal(18,3)` money, `CK_B2BOfferRevisions_NonNegativeMoney`, `Cascade`
  from its parent negotiation, `Restrict` to `MerchantProfiles`. `B2BOfferLines` — unique
  `(B2BOfferRevisionId, ListingVariantId)`, `CK_B2BOfferLines_PositiveQuantity`, `Cascade`
  from its parent revision, `Restrict` FK to `ListingVariants`. `dotnet ef migrations
  has-pending-model-changes` reports no drift.

### Must-have-test coverage (docs/09-TEST-STRATEGY.md §3 "B2B negotiation")

| Must-have test | Covered by |
|---|---|
| Counter-offer preserves old revisions | `B2BNegotiationTests.Counter_CreatesANewImmutableRevision_AndLeavesTheEarlierOnesUntouched`; `B2BNegotiationServiceTests.StartNegotiation_ThenACounterOfferChain_PersistsEveryRevisionImmutably` (SQL Server — three revisions reloaded, revision 1's price/message intact) |
| Expired revision cannot be accepted (or countered / rejected / cancelled) | `B2BNegotiationTests.Accept_WhenTheCurrentOfferHasExpired_IsRejected_AndExpiresTheNegotiation` and `B2BNegotiationTests.CounterAndReject_WhenTheCurrentOfferHasExpired_AreRejectedWithoutCreatingARevision` (the aggregate itself moves the negotiation to `Expired` on the lapsed action, with no new revision); `B2BNegotiationServiceTests.AcceptingAnExpiredOffer_IsRejected_AndSynchronouslyExpiresTheNegotiation` and `B2BNegotiationServiceTests.CounteringOrRejectingAnExpiredOffer_IsBlockedAndSynchronouslyExpiresIt` (SQL Server — the participant action itself returns `Conflict` and persists `Expired` in the same call; a subsequent `ExpireLapsedNegotiationsAsync` sweep is a no-op) |
| Seller cannot accept a negotiation it does not own | `B2BNegotiationServiceTests.ANegotiationIsInvisibleAndUntouchableByAMerchantThatIsNotAParticipant` (accept/counter by a stranger → `NotFound`; `GetNegotiationAsync` → `null`); `B2BOfferHttpTests.OfferPages_RenderForAParticipant_ButAnUnrelatedMerchantGets404OnTheDetail` |
| Buyer cannot buy from itself | `B2BNegotiationTests.Ctor_WhenBuyingMerchantIsTheSellingMerchant_IsRejected`; `B2BNegotiationServiceTests.StartNegotiation_OnYourOwnListing_IsRejected` (SQL Server — no negotiation row) |

### Additional coverage

- `Faed.UnitTests.B2BNegotiationTests` (22) — the aggregate: first revision is by the buyer;
  server-calculated `ProposedTotal`; strict alternation and "cannot counter twice"; accept by
  the proposer rejected; reject/cancel rules; commands on a closed negotiation rejected; MOQ
  in both mixed and per-variant modes; past-expiry / duplicate-variant / non-positive-price /
  over-three-decimal-place offers rejected; every participant action on a lapsed offer
  (accept, counter, reject, cancel) rejected while moving the negotiation to `Expired` with no
  new revision; `ExpireIfLapsed` idempotency; strictly increasing revision numbers.
- `Faed.IntegrationTests.B2BNegotiationServiceTests` (11, SQL Server) — the counter-offer
  chain alternation and "seller cannot accept its own counter"; acceptance records the
  agreement but leaves `AvailableQuantity`/`ReservedQuantity`/`SoldQuantity` untouched
  (no stock consumed); MOQ enforced at the service; synchronous expiry on accept / counter /
  reject; the `Admin`-role exclusion and JOD-precision / variant-removal regressions listed
  under "Post-review fixes" below.
- `Faed.IntegrationTests.B2BOfferHttpTests` (4) — the offer queue challenges an anonymous
  request, is forbidden to a user without an approved merchant profile, and the create page /
  detail render for a participant while an unrelated merchant gets 404 on the detail route.

### Post-review fixes (Codex review — TASK-007)

- A lapsed active offer is expired and persisted synchronously before any participant
  transition. Accept, counter, reject, and cancel all return a controlled conflict; counter
  cannot append another revision and the background sweep remains idempotent.
- `FaedPolicies.CanNegotiateB2B` excludes the `Admin` role before the Offers controller is
  entered. `B2BNegotiationService` independently checks the current Identity role for every
  write and private read, so an approved merchant profile cannot bypass the restriction.
- Proposed unit prices with more than three decimal places are rejected before revision
  construction. Accepted prices and server-derived totals therefore persist exactly at
  `decimal(18,3)` and remain mathematically consistent.
- `MerchantListingService.RemoveVariantAsync` checks immutable B2B offer-line history and
  returns validation guidance to deactivate a referenced variant. The existing `Restrict`
  foreign key remains the database backstop, and a named-FK race is translated instead of
  leaking `DbUpdateException`.
- Focused regressions cover aggregate/service expiry, HTTP and service Admin exclusion,
  JOD precision plus persisted total consistency, and referenced-variant preservation with
  successful deactivation.

### Not implemented (correctly deferred)

- The accepted `B2BDeal`, its atomic multi-line stock reservation, its separate
  `ReservationExpiresAt`, pickup / seller-arranged shipping, the shipment reference and the
  fulfillment state machine — all TASK-008 (docs/adr/0004, docs/10-IMPLEMENTATION-PLAN.md
  Phase 7). Acceptance in TASK-007 only sets `B2BNegotiationStatus.Accepted`.
- B2B reviews and disputes (TASK-009), B2B analytics (TASK-010). No such code was scaffolded.
- A hard cap on counter-offer rounds (docs/13-OPEN-QUESTIONS.md §17) — left unbounded, the
  safe reversible default.

### Validation (TASK-007)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **300 passed (196 unit, 104 integration)**, 0 failed, 0 skipped,
  on a workstation with SQL Server LocalDB reachable. Targeted post-review verification:
  `B2BNegotiationTests` 22/22 and the B2B service/HTTP suites 15/15.
- `20260903121629_AddB2BNegotiation` applies from the existing schema
  (`dotnet ef database update`); the web integration host recreates the database from empty
  every run, so all migrations apply from scratch, and
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- No new migration was required for the four post-review fixes; `dotnet ef database update`
  reports the configured development database already current.
- No broad post-implementation review was performed (per the task instruction).

## TASK-006 — B2C Orders

### Behaviour implemented

- `Models/Entities/Order` + `OrderItem` — the B2C order aggregate (AGENTS.md Rule D). One
  buyer, one selling merchant, one or more variant lines from that merchant; a second
  merchant's variant on the same order is rejected. `Order` owns the explicit status state
  machine (`Pending → Confirmed → ReadyForPickup|OutForDelivery → Completed`, plus
  `Cancelled` and `NoShow`); every transition is a guarded aggregate method, never a status
  assigned from controller input (docs/03-BUSINESS-RULES.md §8). `Subtotal`/`Total` are
  recomputed on the aggregate from the line snapshots plus the fulfilment-fee snapshot —
  no price is ever accepted from the request (`PlaceOrderInput` has no price field;
  docs/08-SECURITY-AND-PRIVACY.md §6-7). `OrderItem` stores immutable snapshots of the
  listing title, variant combination, unit price, condition grade and discount reasons
  (docs/17-DATA-INVARIANTS.md "Order price snapshots never change after creation") — proven
  by `OrderServiceTests.PlaceOrder_ComputesTotalsServerSide_AndSnapshotsSurviveAListingRepricing`.
- `Models/Entities/ListingVariant` gains `Reserve` / `ReleaseReservation` / `ConfirmSale` —
  the `Available ↔ Reserved ↔ Sold` movements the order lifecycle needs, each preserving the
  `Initial = Available + Reserved + Sold` accounting invariant (docs/03-BUSINESS-RULES.md §5)
  and each running under the variant's existing SQL Server `rowversion` (AGENTS.md §7). No
  schema change to the variant — the token has been present since the first variant
  migration.
- `Services/Ordering/OrderService` (`IOrderService`) — the only path that creates or
  transitions an order. `PlaceOrderAsync` opens a transaction, re-loads every requested
  variant and its listing, revalidates (listing `Live`, merchant `Approved`, `AllowB2C`,
  retail price present, requested quantity ≤ available, single merchant, delivery-zone
  minimum), reserves each variant, creates the order and items, refreshes each listing's
  publication (`Live → SoldOut` when a listing runs out), and commits — or rolls back with a
  friendly conflict message on a `rowversion` collision (docs/06-ARCHITECTURE.md §9). Buyer
  and merchant transition methods each pair the status change with its stock movement
  (`Cancel`/`NoShow`/expiry → release, `Complete` → confirm sale) in one transaction. Every
  read/action re-resolves the caller's buyer identity or approved-merchant ownership from the
  database, so guessing another order id reveals nothing (docs/08-SECURITY-AND-PRIVACY.md §9).
- `Services/Ordering/ReservationExpiryService` — a hosted `BackgroundService` that runs
  `IOrderService.ReleaseExpiredReservationsAsync` on the configurable
  `Ordering:ExpirySweepInterval`. The sweep only touches `Pending` orders past their
  reservation window, releases their stock and cancels them, and is idempotent — a second
  run (or one racing a merchant confirmation) does nothing
  (docs/09-TEST-STRATEGY.md "repeated expiry job is idempotent"). It is not hosted under the
  `Testing` environment; the integration tests drive expiry deterministically through the
  service and a fake clock.
- `Services/Ordering/OrderingOptions` (`Ordering` config section) — `ReservationWindow`
  (default 1 hour, the reversible default for docs/13-OPEN-QUESTIONS.md §8),
  `ExpirySweepInterval` (default 5 minutes) and `MaxUnitsPerLine`. Durations live in
  configuration, never as domain constants (docs/13-OPEN-QUESTIONS.md "Important").
- `Services/Ordering/MerchantStoreService` (`IMerchantStoreService`) — merchant CRUD for
  `MerchantLocation` (pickup) and `MerchantDeliveryZone` (delivery fee + optional minimum
  order value). Without at least one active option a merchant's listings cannot be ordered.
- Controllers / Areas:
  - `Areas/Buyer` (new area) — `CheckoutController` (`[Authorize]`; the single-listing order
    builder, GET `/Buyer/Checkout?slug=…` and the placing POST) and `OrdersController`
    (order history, detail, buyer cancellation). An anonymous visitor hitting checkout is
    challenged to sign in.
  - `Areas/Merchant/Controllers/OrdersController` — the selling merchant's order queue with
    filters and the fulfilment transition POSTs (confirm, ready-for-pickup, out-for-delivery,
    complete, no-show, cancel), behind the `ApprovedMerchant` policy.
  - `Areas/Merchant/Controllers/StoreSettingsController` — pickup locations and delivery
    zones.
  - `Controllers`-level `Listing/Details` "Order this item" CTA now links to the real
    checkout instead of the disabled TASK-005 placeholder button; the merchant sub-navigation
    gained "B2C Orders" and "Store settings" (shared `_MerchantSubnav` partial), and the
    top nav gained "My Orders" for signed-in users.
- Views (Faed design system, no raw Bootstrap components): `Buyer/Checkout/Index`
  (variant quantity table, pickup/delivery `<fieldset>`/`<legend>` radio groups with a
  progressive-enhancement toggle, contact block, server-authoritative totals note),
  `Buyer/Orders/{Index,Details}`, `Merchant/Orders/{Index,Details}`,
  `Merchant/StoreSettings/Index`. Empty states, `role="status"`/`role="alert"` messaging,
  `scope`-annotated tables inside `overflow-x` wrappers, and non-colour status badges
  (`Rendering/OrderStatusDisplay`) throughout (docs/07-UI-UX-SPEC.md §10-12).
- Migration `20260903113500_AddB2COrders` — `Orders` (string-valued `Status`/`FulfillmentType`,
  `rowversion`, `CK_Orders_NonNegativeMoney`, indexes on `(BuyerUserId, CreatedAtUtc)`,
  `(MerchantProfileId, Status)` and `(Status, ReservationExpiresAtUtc)` for the sweep),
  `OrderItems` (`CK_OrderItems_PositiveQuantityAndMoney`, unique `(OrderId, ListingVariantId)`,
  `Restrict` FKs to `Listings`/`ListingVariants` so order history is never cascade-deleted),
  `MerchantLocations`, `MerchantDeliveryZones` (`decimal(18,3)` money,
  `CK_MerchantDeliveryZones_NonNegativeMoney`). `dotnet ef migrations
  has-pending-model-changes` reports no drift.

### Mandatory-test coverage (tasks/TASK-006)

| Mandatory test | Covered by |
|---|---|
| Forged price rejected / recomputed | `OrderServiceTests.PlaceOrder_ComputesTotalsServerSide_AndSnapshotsSurviveAListingRepricing` (price is not in `PlaceOrderInput`; `Total` is the server calc and the item snapshot is unchanged by a later listing repricing) |
| Multi-merchant order rejected | `OrderServiceTests.PlaceOrder_WithVariantsFromTwoMerchants_IsRejected` (no order row is created) |
| Two buyers compete for last unit: one succeeds | `OrderServiceTests.PlaceOrder_TwoBuyersCompeteForTheLastUnit_TheLoserGetsAConcurrencyConflict` — **deterministic** interleave via `GatedApplicationDbContext`: order A pauses immediately before its write, order B runs to completion and commits against the *same* original stock state, then A's write is released and fails on the moved token (final `Available = 0`, `Reserved = 1`, one order row) |
| Cancellation releases | `OrderServiceTests.CancelOrder_ReleasesReservedStock`; `ExpiredReservation_IsReleasedByTheSweep_ExactlyOnce` (also asserts the second sweep is a no-op) |
| Completion moves Reserved → Sold | `OrderServiceTests.CompleteOrder_MovesReservedStockToSold` |
| Unauthorized order access blocked | `OrderServiceTests.OrderDetail_IsPrivateToItsBuyer_AndItsSellingMerchant`; `OrderHttpTests.BuyerOrderDetails_ForSomeoneElsesOrder_Returns404`; `OrderHttpTests.MerchantOrderPages_RenderForTheOwner_ButAnotherMerchantGets404OnTheDetail` |

### Additional coverage

- `Faed.UnitTests.OrderTests` — the order state machine: totals from lines + fee,
  delivery-without-address rejected, duplicate variant line rejected, `Confirm` clears the
  reservation expiry / is single-shot / is refused once the window lapses, the fulfilment
  snapshot truncates instead of throwing, the pickup and delivery happy paths, invalid
  transitions (`Complete` before fulfilment, `Cancel`/`MarkNoShow` from the wrong state), and
  the buyer-vs-merchant cancellation windows.
- `Faed.UnitTests.ListingVariantReservationTests` (7) — `Reserve`/`ReleaseReservation`/
  `ConfirmSale` guards and the preserved stock-accounting invariant.
- `OrderServiceTests.MerchantDelivery_AddsTheZoneFeeToTheTotal_AndEnforcesTheZoneMinimum`
  (fee snapshot, `Total = Subtotal + fee`, sub-minimum order rejected) and
  `PlaceOrder_AgainstASuspendedMerchant_IsRejected`.
- `OrderHttpTests` — checkout challenges an anonymous request, is forbidden to
  administrators, renders the order builder for a signed-in buyer, and the merchant order
  queue / store settings / order detail pages render for the owner while another merchant
  gets a 404 on the detail route.
- The Codex-review fixes each carry their own regression test — see "Post-review fixes
  (Codex review — TASK-006)" below.

### Not implemented (correctly deferred)

- A cross-listing "cart" — the domain and `OrderService` already accept multi-listing,
  single-merchant orders, but the checkout UI is scoped to one listing at a time
  (docs/13-OPEN-QUESTIONS.md §13 explicitly permits simplifying the UI initially).
- B2B negotiation / offers / deals (TASK-007/008), disputes and reviews (TASK-009), the
  `Disputed` order status and the admin order-monitoring screens (TASK-010). No B2B, dispute
  or review code was scaffolded.

### Post-review fixes (Codex review — TASK-006)

A review of the initial TASK-006 implementation raised seven blocking findings. All are
fixed, scoped to TASK-006; the schema fix regenerated the single `AddB2COrders` migration
in place (nothing was committed or deployed).

- **A merchant could confirm an order whose stock reservation had already expired.**
  `Order.Confirm` only checked the status, so between an order's window lapsing and the
  expiry sweep running, a merchant could confirm it and hold the stock indefinitely on the
  strength of a passed deadline. `Order.Confirm(nowUtc)` now rejects when
  `ReservationExpiresAtUtc <= nowUtc`; the order stays `Pending` and the sweep cancels it and
  releases the stock. Covered by `OrderTests.Confirm_WhenTheReservationHasAlreadyExpired_*`,
  `Confirm_ExactlyAtTheExpiryInstant_IsRejected` and
  `OrderServiceTests.Confirm_WhenTheReservationHasExpired_IsRejected_AndTheSweepThenReleasesTheStock`.
- **Concurrent orders depleting different variants of one listing could leave it wrongly
  `Live`.** Each `PlaceOrderAsync` computed `RefreshAvailability` from the variants it loaded,
  so two simultaneous orders — each emptying a *different* single-unit variant — each saw the
  other variant still in stock, and both committed against a listing neither transaction
  touched. New `Listing.RegisterStockReservation(nowUtc)` always advances the listing's
  `RowVersion`, and `OrderService.PlaceOrderAsync` calls it for every affected listing:
  concurrent orders now serialize on the listing row, the loser gets a conflict and re-reads
  the true remaining stock. Covered by the deterministic
  `OrderServiceTests.PlaceOrder_TwoConcurrentOrdersDepletingDifferentVariants_LeaveTheListingSoldOut_NotLive`.
- **Administrators could place B2C orders.** New policy `FaedPolicies.CanPlaceB2COrder`
  (authenticated **and** not in the `Admin` role) now guards both `Buyer` area controllers,
  and `OrderService.GetCheckoutAsync` / `PlaceOrderAsync` re-check it via `IUserRoleService`
  as defence in depth (docs/16-PERMISSIONS-MATRIX.md "Create B2C order — Admin ❌",
  docs/08-SECURITY-AND-PRIVACY.md §2). Covered by
  `OrderServiceTests.PlaceOrder_ByAnAdministrator_IsForbidden_ServerSide` and
  `OrderHttpTests.BuyerRoutes_AreForbiddenToAdministrators` (403 at the route).
- **`Order.BuyerUserId` had no referential integrity.** Verified against docs/17
  ("Order has exactly one Buyer"), docs/04 §12 ("Do not cascade-delete completed Orders",
  "Carefully configure FK delete behavior") and the existing `MerchantProfile →
  ApplicationUser` precedent — the docs call for it. Added
  `Order → ApplicationUser` on `BuyerUserId` with `OnDelete(DeleteBehavior.Restrict)` (a
  buyer with order history can never be hard-deleted). `AddB2COrders` regenerated with
  `FK_Orders_AspNetUsers_BuyerUserId`. Covered by
  `OrderServiceTests.Order_BuyerUserId_IsReferentiallyBoundToAnIdentityUser`.
- **The documented buyer "confirm receipt" completion flow was missing.** docs/01-PRD.md §4
  lists "confirm receipt" as an individual-buyer capability. Added
  `IOrderService.ConfirmReceiptAsync` (buyer-owned order, `ReadyForPickup`/`OutForDelivery`
  → `Completed`, `Reserved → Sold` — the same transition the merchant's "mark completed"
  uses), a `Buyer/Orders/ConfirmReceipt` POST and an "I've received this order" button on the
  buyer order detail. Covered by
  `OrderServiceTests.ConfirmReceipt_ByTheBuyer_CompletesTheOrder_AndMovesReservedStockToSold`
  (with the too-early and wrong-buyer negative cases).
- **A long-but-valid pickup location could make checkout throw.** The composed fulfilment
  snapshot (name + address + area + city + hours + instructions ≈ 1.4k) exceeded the
  600-char column and `Order`'s constructor rejected it, failing every checkout against that
  location. `Order.MaxFulfillmentSnapshotLength` is now 2000 (above the largest string the
  maximum-length `MerchantLocation` fields can produce) and the constructor truncates rather
  than throwing for this server-composed field. Covered by
  `OrderTests.NewOrder_WithAnOverLongFulfilmentSnapshot_TruncatesRatherThanThrowing` and
  `OrderServiceTests.Checkout_AndPlaceOrder_WithAMaximumLengthPickupLocation_DoNotFail`.
- **The last-unit concurrency test used `Task.WhenAll`, not a deterministic interleave.**
  Replaced with `GatedApplicationDbContext` (a shared test-support `IApplicationDbContext`
  decorator that runs a hook once, immediately before the first `SaveChangesAsync`): the two
  competing `PlaceOrderAsync` calls now provably read the same original stock state and
  rowversion before either writes, and exactly one write wins. Both the last-unit and the
  multi-variant race tests use it.

### Validation (TASK-006, after the fix pass)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **263 passed (174 unit, 89 integration)**, 0 failed, 0 skipped,
  on a workstation with SQL Server LocalDB reachable.
- `AddB2COrders` regenerated (`20260903113500_AddB2COrders`) with the `BuyerUserId` FK and
  the widened `FulfillmentSnapshot` column; the web integration test host recreates the
  database from scratch (`EnsureDeleted` + `Migrate`) every run, so all migrations apply from
  empty, and `dotnet ef migrations has-pending-model-changes` reports no drift.
- SQL Server concurrency exit criterion met and strengthened: both
  `PlaceOrder_TwoBuyersCompeteForTheLastUnit_TheLoserGetsAConcurrencyConflict` and
  `PlaceOrder_TwoConcurrentOrdersDepletingDifferentVariants_LeaveTheListingSoldOut_NotLive`
  run a deterministic interleave against real SQL Server, not InMemory/SQLite
  (docs/09-TEST-STRATEGY.md §2).
- Targeted verification only, scoped to the seven findings and the regressions their fixes
  could cause — no fresh broad review.

## TASK-005 — Public Marketplace

### Behaviour implemented

- `Services/Marketplace/IPublicMarketplaceService` + `PublicMarketplaceService` — the only
  code path the public pages use to read listing/merchant data. Every method filters to
  `ListingStatus.Live` (`GetHomePageAsync`, `BrowseListingsAsync`, `GetListingBySlugAsync`)
  and `MerchantVerificationStatus.Approved` (`GetMerchantStoreHeaderBySlugAsync`,
  and every card's `MerchantIsVerified` flag) — there is no query in this service capable of
  returning a Draft/PendingReview/Rejected/Hidden/SoldOut/Archived listing or a
  non-Approved merchant's storefront (docs/03-BUSINESS-RULES.md §2,
  docs/11-ACCEPTANCE-CRITERIA.md "Public sees only Live listings"). A category/condition/
  discount-reason/brand/merchant filter given as a slug or code that does not resolve to a
  real, active row returns zero results rather than being silently ignored — an
  unresolvable filter must never fall back to "show everything"
  (docs/06-ARCHITECTURE.md §12 "slugs are never authorization identifiers, but they are
  still real lookups"). Browsing is a two-phase read (filter/sort/page down to a bounded
  list of ids, then hydrate that page's rows against the small reference tables) so the
  filter query stays simple to translate while still touching each reference table once
  per call rather than once per row (docs/06-ARCHITECTURE.md §13).
- `PublicListingDetailView` / `ListingCardView` / `PublicMerchantProfileView` — display
  shapes deliberately separate from the merchant/admin `ListingDetailView`: moderation
  history, rejection notes, `HiddenByAdmin` and submission blockers are internal review
  state and have no field on the public shapes at all, so there is no way to accidentally
  render them into a public page (docs/08-SECURITY-AND-PRIVACY.md §3). Reserved/sold
  variant counters are likewise absent from the public variant view — only
  `AvailableQuantity`/`IsSellable`, matching faed-commerce-ux "avoid exact unit-count
  obsession" while still allowing an honest "only N left" / "sold out" state.
- `Controllers/{Shop,Listing,Store}Controller` — new public, anonymous, attribute-routed
  controllers (`/shop`, `/listing/{slug}`, `/store/{slug}`); `HomeController.Index` now
  renders the real marketplace home instead of the TASK-001 placeholder. A slug that does
  not resolve returns `NotFound()`, re-executed by a new
  `app.UseStatusCodePagesWithReExecute("/status/{0}")` into a branded empty state
  (`Home.StatusCodePage` / `Views/Home/StatusCode.cshtml`) instead of the framework's bare
  404 (docs/07-UI-UX-SPEC.md §12 "do not show generic blank pages").
- Views: `Home/Index` (hero, category navigation, featured listings, how-it-works,
  condition/discount transparency, merchant acquisition CTA), `Shop/Index` and
  `Store/Index` (share one `_ShopBrowse` partial — filters, sort, product grid, pagination,
  empty state — via the `IShopBrowsePageModel` interface so the two pages cannot drift
  visually or behaviourally), `Listing/Details` (gallery, price/condition/discount blocks,
  a vanilla-JS variant picker, availability state, a B2B block, and a defect/packaging
  disclosure section kept visually separate from ordinary product photos). Filters
  (`Areas`-free `ViewModels/Marketplace/ShopFilterModel`) round-trip through the query
  string; an unresolvable filter still renders the filter UI (so the visitor can change it)
  with a "no results" empty state, distinguished from the true "nothing live yet" empty
  state.
- The B2C "Add to Order" / B2B "Make an Offer" buttons are rendered `disabled` with a
  visible hint ("Online ordering isn't switched on yet") rather than posting to endpoints
  that do not exist yet — B2C/B2B ordering is TASK-006/007/008 scope
  (docs/10-IMPLEMENTATION-PLAN.md, faed-commerce-ux "Disabled CTAs must explain why").
  Nothing about checkout/cart/reservation was scaffolded ahead of that work.
- New CSS component layer appended to `wwwroot/css/faed.css` (hero, product card, price
  block, category grid, variant picker, gallery, filter panel, sort bar, pagination, store
  header, how-it-works/transparency sections) — reuses the existing design tokens, no raw
  Bootstrap card/button styling introduced (faed-ui-direction). The mobile filter drawer is
  Bootstrap 5.3's responsive `offcanvas-lg` component (a static sidebar at ≥lg, a slide-in
  drawer with its own close button below it), not a hand-rolled one.
- No schema/migration changes — every entity, index and enum this task reads
  (`Listing`, `ListingVariant`, `ListingMedia`, `ListingDiscountReason`, `Category`,
  `ConditionGrade`, `DiscountReason`, `Brand`, `MerchantProfile`) already existed from
  TASK-003/004.

### Accessibility fixes made during the mandatory review (faed-responsive-accessibility, faed-ui-quality-gate)

- The product card's verified checkmark was `aria-hidden` with only a mouse-hover
  `title`, so a screen-reader user got no verification signal at all; added a
  `visually-hidden` "Verified merchant." text alternative alongside the icon.
- Gallery thumbnail buttons had no accessible name (an empty decorative `alt` inside an
  unlabelled `<button>`); added `aria-label` per thumbnail and grouped them under
  `role="group" aria-label="Product photos"`.
- Variant option chip groups had a plain `<span>` label with no programmatic association;
  each group is now `role="group" aria-labelledby="option-label-{id}"`.
- The JS variant picker toggled an `is-unavailable` CSS class without updating
  `aria-disabled`, so a keyboard/assistive-tech user got a silent no-op click with no
  indication why; the picker now sets `aria-disabled` alongside the class.
- Selected variant chips were distinguished by background colour alone; added a
  non-colour checkmark glyph and bold weight so the selected state survives a
  colour-only reading (faed-responsive-accessibility "selected variant state is visible
  beyond color").
- Variant chips and gallery thumbnails were undersized for touch (well under 44px) and had
  no explicit focus-visible style; both now meet the same tap-target/focus conventions as
  `.faed-btn`.
- A CSS rule that hid the mobile filter drawer's close header relied on an accidental
  specificity tie-break to be re-shown below the `lg` breakpoint rather than an explicit
  media query; rewritten so the desktop-only hide is itself inside `@media (min-width: 992px)`.
- The Shop/Store empty state said "No listings match your filters" even when zero filters
  were active (i.e. the marketplace is simply empty) — split into two distinct, correctly
  worded empty states.

### Exit-criteria coverage (tasks/TASK-005)

| Exit criterion | Covered by |
|---|---|
| Anonymous user can understand a listing without hidden critical information | `Listing/Details.cshtml` renders merchant + verified state, condition + meaning, why discounted, defect evidence (separate, labelled section), reference price only when it passed moderation, variant availability, B2C/B2B availability and fulfillment/policy text above and immediately below the fold |
| Non-Live listings cannot be accessed publicly | `PublicMarketplaceServiceTests.GetListingBySlugAsync_OnlyEverReturnsALiveListing` (Draft/PendingReview/Hidden all `null`, same slug); `BrowseListingsAsync_ExcludesNonLiveListings_AndAnUnresolvableCategoryYieldsZeroResults`; manual verification (a raw-SQL Draft listing 404s on `/listing/{slug}` and is absent from `/shop` search) |
| Mobile layout checked | faed-responsive-accessibility pass above; `offcanvas-lg` filter drawer, single-column product grid/listing layout below their breakpoints |
| Accessibility baseline checked | faed-responsive-accessibility pass above (fixes listed) |

### Manual end-to-end verification (real MVC pipeline, real SQL Server)

Ran the app against LocalDB with a merchant/listing pair inserted directly (bypassing the
already-covered TASK-002/004 flows) to exercise the read side under real HTTP: Home shows
the featured listing, category counts and verified badge; `/shop` filters correctly by
category/condition/channel/price/search, and an unresolvable category returns a genuine
zero-result empty state (not "show everything"); `/listing/{slug}` renders price, the
40%-lower discount computed from reference vs. retail price, condition meaning, the B2B
block, and the variant-availability JSON payload; `/store/{slug}` shows the verified badge
and listing count; a Draft listing inserted at the same time returns 404 on its own slug and
is absent from `/shop` search results; `/listing/does-not-exist` and `/store/does-not-exist`
both render the branded 404 page. Test data removed afterwards.

### Not implemented (correctly deferred)

B2C cart/order placement, B2B negotiation/offers, real "How It Works" as a separate route
(folded into a Home section instead), per-facet result counts on the filter sidebar,
multi-select filters (each dimension is single-select via the query string) — none of these
are TASK-005 deliverables (docs/10-IMPLEMENTATION-PLAN.md Phase 4 scope).

### Post-review fixes (code review after initial TASK-005 completion)

A review of the initial TASK-005 implementation found two P1 defects and eight P2 issues.
All are fixed.

- **A suspended merchant's Live listings stayed fully public.** Every public read
  (`PublicMarketplaceService.GetHomePageAsync`/`BrowseListingsAsync`/`GetListingBySlugAsync`
  and `ListingMediaService.OpenImageAsync`) filtered only on `ListingStatus.Live` — suspending
  a merchant changes only `MerchantProfile.VerificationStatus`, so their listings (and photos)
  kept appearing to anonymous visitors while only the storefront page itself 404'd
  (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved"). Added a single
  `PublicLiveListings()` gate (`Status == Live` **and** the owning merchant is currently
  `Approved`) that every browse/home/detail query now goes through, and the equivalent check
  in `ListingMediaService.OpenImageAsync`. Regression-covered end-to-end:
  `PublicMarketplaceServiceTests.SuspendingTheMerchant_HidesTheirLiveListingEverywhere_*`
  (service layer) and `PublicMarketplaceHttpTests.LiveListing_IsReachableByAnonymousHttp_*`
  (real HTTP — detail page, store page and image all flip to 404/403 the moment the merchant
  is suspended, while the `Listing.Status` row is untouched).
- **A listing could publish with a disclosed defect and no evidence photo.** Submission
  already required *a* discount reason and *a* product photo, but never required a defect or
  packaging photo for Grade B ("packaging imperfection"), Grade D ("cosmetic imperfection"),
  or the `PackagingDamage`/`CosmeticDefect` discount reasons — an admin could approve a listing
  that claims a physical flaw with nothing showing it (docs/03-BUSINESS-RULES.md §3 "defects
  must be disclosed and visually evidenced where applicable"). `Listing.DescribeSubmissionBlockers`
  and `Listing.SubmitForReview` now take the resolved condition-grade code and discount-reason
  codes (the aggregate itself only ever stored catalog ids) and block submission until a
  `Defect` or `Packaging` photo exists; `MerchantListingService.SubmitForReviewAsync` resolves
  those codes from the database before calling either. Covered by 6 new
  `Faed.UnitTests.ListingTests` cases (grade B/D and each reason, both the throw and the
  succeeds-once-photographed path) and
  `PublicMarketplaceServiceTests.SubmitForReviewAsync_ConditionGradeClaimsAPhysicalImperfection_WithoutEvidence_IsRejected`
  against real SQL Server.
- **The marketplace was not actually bounded to the Fashion Overstock launch sector.** Home's
  category navigation, the Shop category facet, and browse itself considered every active
  non-root category and every Live listing globally — today's seed data happens to have only
  the three launch categories, but nothing stopped a category added under an unrelated future
  sector from immediately appearing in the MVP UI (AGENTS.md §3 "Do not expose unrelated
  sectors in the MVP UI"). Added `GetLaunchSectorCategoryIdsAsync`, which walks the category
  tree from `CatalogDataSeeder.RootCategorySlug`, and applied it to every category-facing
  query; a category slug outside that set now resolves the same as one that does not exist.
  Covered by `BrowseListingsAsync_NeverExposesACategoryOutsideTheLaunchSector`, which inserts a
  real second-root "Electronics → Phones" branch and proves it is invisible to Home, the Shop
  facet list, and direct-slug browsing alike.
- **The sort dropdown could get stuck on the previous choice.** The sort mini-form emitted a
  hidden `Sort` input carrying the *current* sort value and then, immediately after, a
  `<select name="Sort">` with the newly chosen one — two same-named controls in one form, and
  ASP.NET Core model binding takes the first value in the query string, so a change away from
  "Newest" silently kept losing to the stale hidden value. Fixed by excluding `Sort` from the
  hidden-field loop in that one form (pagination/other links still carry it via
  `ToFilterRouteValues()`, which is unaffected).
- **An out-of-range page produced an empty page with a positive total, and an unbounded
  multiplication.** `Page` was lower-bounded but never capped to the real last page, so a
  hand-edited `?Page=999999` returned zero items next to a nonzero `TotalCount` — which the UI
  then misreported as "No listings here yet" with no way back — and left `(page - 1) * pageSize`
  unbounded by anything but the caller's input. `BrowseListingsAsync` now clamps `page` to the
  computed `TotalPages` once the true count is known, before paging. Covered by
  `BrowseListingsAsync_OutOfRangePage_ClampsToTheLastRealPage_*`.
- **Size and colour filtering, required by docs/07-UI-UX-SPEC.md §4, did not exist.** Generic
  listing options have no shared reference table (each merchant names their own "Size"/
  "Colour"), so `ShopQuery` gained `SizeValue`/`ColorValue` matched case-insensitively against
  option name aliases, `ShopFacets` gained `Sizes`/`Colors` (distinct values actually in use,
  scoped the same way the Brand facet already was), and the Shop/Store filter panel renders
  both when any values exist. Covered by `BrowseListingsAsync_FiltersBySizeAndColour`.
- **Browsing only ever used `RetailPrice`, even for wholesale-only listings.** A listing with
  `AllowB2C == false` has no `RetailPrice` at all (docs/04-DOMAIN-MODEL.md §3), so it was
  invisible to every price filter, mis-sorted as free/priceless, and its card said "Price on
  request" despite carrying a real `WholesaleIndicativeUnitPrice`. Price filtering and sorting
  now use `RetailPrice ?? WholesaleIndicativeUnitPrice`; `ListingCardView` gained
  `EffectivePrice`/`EffectivePriceIsWholesale`, rendered on the card and on the listing-detail
  price block as "JOD X /unit · wholesale". Covered by
  `BrowseListingsAsync_AWholesaleOnlyListing_IsPriceFilterableAndSortable_ByItsIndicativePrice`
  and `BrowseListingsAsync_SortsByPrice_UsingTheEffectivePriceForBothDirections`.
- **The channel filter's labels contradicted its behaviour.** "Retail only" and "Wholesale
  only" implied exclusivity but matched inclusively (`AllowB2C`/`AllowB2B` alone), so a
  dual-channel listing appeared under both — arguably the more useful behaviour for a buyer
  checking "can I buy this at retail", but not what the labels promised. Kept the inclusive
  behaviour (excluding dual-channel listings from a retail-minded filter would be a worse
  result for that buyer) and relabelled the options "All listings" / "Retail available" /
  "Wholesale available", with a one-line hint that a listing can offer both. Regression test
  renamed to `BrowseListingsAsync_FiltersByChannel_Inclusively` to say what it actually proves.
- **Sold-out variant combinations were selectable in the picker.** `refreshDisabledStates`
  marked a combination "possible" whenever *any* variant matched it, ignoring
  `variant.sellable` — a combination that exists but is depleted was never disabled
  (faed-commerce-ux "disable unavailable combinations"). Fixed to require a *sellable* match;
  also added `aria-live="polite"` to the availability text so the change is announced, not
  only visible. Covered indirectly by
  `GetListingBySlugAsync_VariantAvailability_ReflectsDepletedStock` (the data source
  the picker consumes); the picker script itself has no test harness in this repo.
- **Fulfillment copy asserted capabilities nothing backs, and was wrong for B2B-only
  listings.** Every listing stated "Pickup and merchant delivery options are confirmed at
  checkout" regardless of channel — merchant pickup/delivery capability is not modelled yet
  (TASK-004/005 status), and B2B fulfillment is direct pickup or seller-arranged shipping, not
  a B2C checkout at all (docs/03-BUSINESS-RULES.md §12). Split into a retail line (shown only
  when `AllowB2C`, softened to "will be shown once ordering opens") and a wholesale line (shown
  only when `AllowB2B`).
- **TASK-005's tests only ever exercised the service layer.** Added
  `PublicMarketplaceHttpTests` (`WebApplicationFactory`, real MVC pipeline, no auth) covering
  `/shop`, `/listing/{slug}` (404 for unknown, 200 with content for Live), `/store/{slug}`
  (404 for unknown), and `/listing-images/{id}` — including the same merchant-suspension
  scenario as the service-level test, proving the HTTP surface (not just the service method)
  flips to 404/403. The pre-existing suspension test was also widened: it previously checked
  only that the storefront header disappeared, which would have missed the primary P1 defect
  entirely.

### Validation (TASK-005, after the fix pass)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **207 passed (143 unit, 64 integration)**, 0 failed, 0 skipped.
  `Faed.UnitTests.ListingTests` grew by 6 (defect-evidence rule); `Faed.IntegrationTests`
  grew by 13 (10 more `PublicMarketplaceServiceTests` + a new 4-test `PublicMarketplaceHttpTests`,
  net of the pre-existing suspension test being widened in place).
- No new migration — `dotnet ef migrations has-pending-model-changes` reports no drift (the
  fix is entirely query/domain-method-signature scoped, no schema change).
- Manual end-to-end re-verification against LocalDB with a retail listing (Colour option) and
  a wholesale-only listing (no retail price) seeded directly: Shop's filter panel shows the
  Colour facet and the corrected "All listings / Retail available / Wholesale available"
  labels; the wholesale-only card and its detail page both show "JOD 3.250 /unit · wholesale"
  instead of "Price on request"; its detail page shows only "Wholesale fulfillment", not a
  retail line; `?Sort=PriceLowToHigh` round-trips correctly; `?Page=9999` returns the real
  last page instead of an empty one.

### Post-review fixes (second code review — marketplace + moderation)

A second review found ten issues (two High, six Medium, two Low). All are fixed.

- **[High] Required defect evidence could be removed after publication, and approval never
  re-checked it.** Removing the last packaging photo was not treated as material, so a Grade
  B/D or `PackagingDamage`/`CosmeticDefect` listing could go (or stay) Live with no visual
  evidence, and `Listing.Approve` published whatever the listing currently was without
  re-running the submission checks. `Listing.RemoveMedia` now takes the resolved
  condition-grade / discount-reason codes and refuses to drop the last `Defect`/`Packaging`
  photo when `Listing.DisclosesAPhysicalImperfection` is true (same shape as the existing
  last-product-photo guard); `ListingModerationService` re-runs `DescribeSubmissionBlockers`
  on approve and returns a conflict if any blocker reappeared. The shared code-resolution
  helper moved to `ListingQueries.LoadDisclosureCodesAsync`. Covered by 3 new
  `ListingTests` cases and
  `ListingServiceTests.RemoveImage_TheLastDisclosurePhotoOfAGradeBListing_IsRejected_*`.
- **[High] Size/colour filters were not variant-aware.** The filter only checked that the
  option value existed somewhere on the listing, so "White + M" matched a listing stocking
  only White/L and Black/M, whose requested SKU cannot be bought. `BrowseListingsAsync` now
  requires a single active, in-stock `ListingVariant` carrying every requested value together,
  and the size/colour facets are built the same way (values on a sellable variant only).
  Covered by
  `PublicMarketplaceServiceTests.BrowseListingsAsync_SizeAndColour_MustBeSatisfiedByOneSellableVariant_*`.
- **[Medium] Card hydration could expose a listing after its visibility changed.**
  `HydrateCardsAsync` loaded the paged ids without re-checking status/merchant approval; it
  now re-applies `PublicLiveListings()`, so a moderation hide or merchant suspension between
  the id query and the hydration query drops the card instead of rendering it.
- **[Medium] The launch-sector root lookup was case-sensitive.**
  `GetLaunchSectorCategoryIdsAsync` compared the root slug with in-memory `==` after
  materialization while the seeder matches case-insensitively; a differently-cased existing
  root made Home and Shop appear empty. Now `StringComparison.OrdinalIgnoreCase`.
- **[Medium] Paging order was nondeterministic for ties.** Every sort now ends on `l.Id`, a
  unique final key, so listings tied on price and publication timestamp keep a stable order
  across page requests.
- **[Medium] Facet construction loaded the whole catalog into memory.** The size/colour facet
  query pulled every matching `Listing` and its full option graph; it now projects the
  distinct `(option name, value)` pairs in the database.
- **[Medium] Public filter input had no server-side validation.** `ShopFilterModel` gained
  `[Range]`/`[StringLength]`/`[EnumDataType]` attributes, and `ToQuery` now clamps negative
  prices, swaps a reversed min/max range, caps search text at 100 characters, and falls back
  to the default for an undefined `Channel`/`Sort` enum value.
- **[Medium] Defect evidence sat below the description and all policy content.**
  `Views/Listing/Details.cshtml` was reordered to the trust-first sequence from
  faed-commerce-ux: purchase block (with a short fulfillment summary) → disclosed-issue
  photos → description → details & policies → wholesale block.
- **[Low] Filter radio groups had no programmatic group label.** The Shop/Store filter panel
  now uses `<fieldset>`/`<legend>` per group instead of a visual `<span>` heading.
- **[Low] The mobile filter control showed only a bullet.** `ShopFilterModel.ActiveFilterCount`
  is rendered as a numeric badge on the "Filters" toggle so one vs several applied filters is
  visible.

No new migration — the changes are query, view, view-model and domain-method-signature
scoped, with no schema change.

### Post-review fixes (third pass — three TASK-005 blocking defects)

A follow-up review flagged three blocking defects still inside TASK-005 scope. All are fixed;
scope was held to these three findings.

- **The Fashion launch-sector restriction was not enforced on direct listing-detail access.**
  Home and Shop already scoped every query to the `Fashion Overstock` category subtree
  (`GetLaunchSectorCategoryIdsAsync`), but `PublicMarketplaceService.GetListingBySlugAsync`
  filtered only on `PublicLiveListings()` (Live + approved merchant). A Live listing filed
  under a category outside the launch sector — a future, unrelated sector added as data — was
  therefore a 404 in browse but fully readable by guessing its slug at `/listing/{slug}`, a
  hole straight through AGENTS.md §3 "Do not expose unrelated sectors in the MVP UI".
  `GetListingBySlugAsync` now resolves the launch-sector category set and requires
  `launchCategoryIds.Contains(l.CategoryId)`, exactly as Home/Shop do. Regression-covered by
  `PublicMarketplaceServiceTests.GetListingBySlugAsync_ForALiveListingOutsideTheLaunchSector_ReturnsNull`
  (service, with an in-sector control listing proven still reachable) and
  `PublicMarketplaceHttpTests.ListingDetail_ForALiveListingOutsideTheLaunchSector_Returns404`
  (real HTTP route, `Listing.Status` confirmed `Live` the whole time).

- **Product-image changes on a published listing bypassed moderation.** `Listing.AddMedia` /
  `Listing.RemoveMedia` treated only `Defect` photography as material; adding, replacing or
  removing a publicly visible `Product` photo on a `Live`/`SoldOut` listing only `Touch`ed it
  and left it public, so a merchant could swap the gallery a buyer judges the item by with no
  review (AGENTS.md §8 "Do not let a merchant edit a live listing … and bypass review").
  A new `Listing.IsMaterialMedia` predicate (`Product` **or** `Defect`) now gates both
  mutators: adding/removing a Product photo routes through `ApplyMaterialChange`, so a
  `Live`/`SoldOut` listing returns to `PendingReview` with a fresh `ListingModeration` row
  (preserving the prior approval in history, the same approved/public-version semantics every
  other material change already uses), and the mutator throws outright while the listing is
  `PendingReview`. Ordinary `Packaging` shots stay non-material (they keep their existing
  last-disclosure-photo guard). `ListingModerationService.ApproveAsync` already re-runs
  `DescribeSubmissionBlockers` on approve, so a re-review cannot publish a gallery that has
  dropped below one product photo. Regression-covered by `ListingTests`
  (`AddMedia_ProductPhoto_OnLiveListing_ReturnsToPendingReview_PreservingApprovalHistory`,
  `RemoveMedia_ProductPhoto_OnLiveListing_WhenAnotherRemains_ReturnsToPendingReview`,
  `AddMedia_ProductPhoto_WhilePendingReview_Throws`,
  `AddMedia_PackagingPhoto_OnLiveListing_StaysLive`) and, against real SQL Server + real file
  storage, `PublicMarketplaceServiceTests.AddImageAsync_AProductPhotoOnALiveListing_ReturnsItToPendingReview_AndDropsItFromPublicView`
  (listing 404s from the public detail path and the new image is not anonymously served while
  pending).

- **The variant picker trapped the buyer on disabled option values.** `refreshDisabledStates`
  in `wwwroot/js/listing-detail.js` marked an option chip unavailable whenever no *sellable*
  variant matched the chip's value **combined with the current selection in every other
  option group**. From a valid `Black/M`, every `White` chip then disabled itself against the
  selected size `M` (because `White/M` is not a real variant), even though `White/L` is
  perfectly sellable — so the buyer could not move from `Black/M` to `White/L` at all. The
  rule is now per-value: a chip is disabled only when *no* sellable variant carries that
  value, independent of the other groups' current selection. Impossible partial combinations
  (e.g. `White` + `M`) are surfaced by the existing availability line
  ("That combination is not available.") instead of a dead-end disabled chip. A depleted or
  deactivated value with no sellable variant left is still disabled, so the earlier
  "sold-out combinations are selectable" fix is preserved. The same rule is now also computed
  server-side — `PublicListingDetailView.SellableOptionValueIds` — and rendered into the
  initial chip markup so a no-JS page is correct too. Regression-covered by
  `PublicListingDetailViewTests` (the `Black/M` + `White/L` anti-trap case, the
  depleted/inactive-value exclusion, and the no-options case).

No new migration — every change is query, domain-method, view-model or client-script scoped;
`dotnet ef migrations has-pending-model-changes` reports no drift.

### Validation (TASK-005, third fix pass)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **222 passed (153 unit, 69 integration)**, 0 failed, 0 skipped,
  on a workstation with SQL Server LocalDB reachable. New since the previous pass: 7 unit
  (`ListingTests` ×4, `PublicListingDetailViewTests` ×3) and 3 integration
  (`PublicMarketplaceServiceTests` ×2, `PublicMarketplaceHttpTests` ×1).
- `dotnet ef migrations has-pending-model-changes` — no changes to the model since the last
  migration.
- Targeted verification only, scoped to the three findings and regressions their changes
  could cause (per the task instruction) — no fresh broad review.

## TASK-004 — Listings, Variants, Inventory and Moderation

### Behaviour implemented

- `Listing` aggregate (`Models/Entities/Listing.cs`) — owns `ListingOption`/`ListingOptionValue`,
  `ListingVariant` (+ `ListingVariantOptionValue` join), `ListingMedia`, `ListingDiscountReason`,
  `ListingReferencePriceEvidence` and `ListingModeration` as private backing-field collections.
  Holds no authoritative stock total (AGENTS.md Rule A); `AvailableUnits` is derived from
  active variants. Every mutator that changes a material claim (category, brand, condition,
  title, description, included/missing items, prices, sales channels, discount reasons,
  options/variants, defect photos) routes through `ApplyMaterialChange`, which takes a
  `Live`/`SoldOut` listing back to `PendingReview` and opens/extends a `ListingModeration`
  row, or returns a `Hidden` listing to `Draft` so `Restore` can never republish unreviewed
  content (AGENTS.md §8, docs/02-SCOPE-AND-DECISIONS.md "Listing moderation policy").
  Non-material fields (return policy, warranty, mixed-lot flag, ordinary/packaging photos,
  reference-price evidence additions, stock quantity) do not reopen moderation.
  `UpdateDetails` applies the business-detail fields *and* the discount-reason set as one
  atomic transition under a single `RequireMaterialEditAllowed()` check — see "Bug found and
  fixed" below.
- `ListingVariant` — the authoritative stock record (AGENTS.md Rule A, docs/adr/0002).
  `AvailableQuantity`/`ReservedQuantity`/`SoldQuantity`/`InitialQuantity` all `>= 0`, enforced
  by a domain guard in `AdjustAvailable` *and* a database check constraint
  (`CK_ListingVariants_Quantities_NonNegative`). SQL Server `rowversion` `RowVersion` is
  present from the migration that first creates the table. `OptionCombinationKey` is a
  deterministic, order-independent fingerprint of the variant's option-value set, backed by
  a unique index (`ListingId`, `OptionCombinationKey`) so a duplicate combination is rejected
  by the database even when two requests race each other, not only by the in-memory aggregate
  check.
- `ListingMedia` / `ListingDiscountReason` / `ListingReferencePriceEvidence` / `ListingModeration`
  / `InventoryAdjustment` — supporting entities per docs/04-DOMAIN-MODEL.md §3-5. Photos and
  evidence store only a private `IFileStorage` object key, never a public URL
  (docs/08-SECURITY-AND-PRIVACY.md §3). `InventoryAdjustment` records who/when/variant/before/
  after/reason for every manual stock correction (docs/03-BUSINESS-RULES.md §6); stock is
  never silently overwritten.
- `Services/Listings` — `MerchantListingService` (`IMerchantListingService`): create/edit a
  listing, manage options/values/variants, upload/remove product/defect/packaging photos and
  reference-price evidence, submit for review, hide/restore/archive. Re-resolves the owning
  Approved merchant from the database on every call (never trusts a route/form value,
  docs/08-SECURITY-AND-PRIVACY.md §6, §9); a guessed listing id reads as "not found" for a
  non-owner. Uploads are buffered, structurally validated with the same fail-closed inspector
  already hardened for merchant verification documents
  (`ListingImageValidator` → `VerificationDocumentValidator.ValidatePayload`,
  docs/adr/0007), and only then stored — an upload that fails after the bytes are written is
  cleaned up rather than left orphaned.
  `InventoryService` (`IInventoryService`): variant-level stock adjustments, each written
  together with its `InventoryAdjustment` row and a listing `RefreshAvailability` call inside
  one transaction (AGENTS.md §7); a stale `rowversion` surfaces as `Result.Conflict`, never a
  raw DB exception (docs/06-ARCHITECTURE.md §9). `ListingModerationService`
  (`IListingModerationService`): admin queue, approve/reject/hide, each decision written with
  its `AdminActionLog` entry in one transaction. `ListingMediaService`
  (`IListingMediaService`): resolves one listing image for a caller entitled to see it —
  anyone once the listing is `Live`/`SoldOut`, otherwise only the owning merchant or an admin.
- `Areas/Merchant/Controllers/{Listings,Inventory}Controller` and
  `Areas/Admin/Controllers/ListingModerationController` — thin controllers behind the
  `ApprovedMerchant` / `AdminOnly` policies; `Controllers/ListingMediaController`
  (`/listing-images/{id}`) is the only route that ever serves a listing image, allows
  anonymous requests, and defers every visibility decision to `IListingMediaService`.
- Merchant listing workspace (`Areas/Merchant/Views/Listings/{Index,Create,Workspace}.cshtml`)
  and inventory screen (`Areas/Merchant/Views/Inventory/Index.cshtml`), admin moderation queue
  and detail (`Areas/Admin/Views/ListingModeration/{Index,Details}.cshtml`) — Faed design
  system components (`faed-section`, `faed-thumb-grid`, `faed-chip`, `faed-stat`,
  `faed-blockers`, native `<dialog>` for the stock-adjustment form), DB-driven category/
  condition/brand/discount-reason choices (no hard-coded catalog values), submission blockers
  surfaced as plain sentences, defect photos visually distinguished from product/packaging
  photos.
- Migration `20260901141629_AddListingsAndInventory` — all TASK-004 tables; `RowVersion`
  present on `ListingVariants` from this first migration; `dotnet ef migrations
  has-pending-model-changes` reports clean.

### Bug found and fixed during manual verification

Running the full merchant → submit → admin-approve → material-edit flow through the real
MVC pipeline against SQL Server (registration, verification, listing creation, variant/photo
upload, submission, approval, then editing the live listing's title) surfaced a real defect
that the build and the unit-only pass had not: `MerchantListingService.ApplyDetails`
originally called `Listing.UpdateDetails(...)` and then a separate `Listing.SetDiscountReasons(...)`.
When the business-detail edit was material (for example a title change on a `Live` listing),
`UpdateDetails` correctly transitioned the in-memory aggregate to `PendingReview` — but the
very next call, `SetDiscountReasons`, re-checked `RequireMaterialEditAllowed()` and now saw a
`PendingReview` listing, throwing "This listing is being reviewed and cannot be edited until a
decision is made." even though the discount reasons themselves had not changed. The edit
silently failed (redisplayed the form with that error) whenever both a material field and the
existing discount reasons were submitted together — i.e. on every normal save from the
workspace form. Fixed by merging discount-reason assignment into `UpdateDetails` itself, so
every field the business-details form submits — including discount reasons — is validated and
applied under one `RequireMaterialEditAllowed()` check and one `ApplyMaterialChange` transition.
Regression-covered by `Faed.UnitTests.ListingTests.MaterialEdit_OnLiveListing_ReturnsToPendingReview_WithoutLosingApprovalHistory`
and, against real SQL Server, `Faed.IntegrationTests.ListingServiceTests.MaterialEdit_OnALiveListing_ReturnsItToPendingReview_AndPreservesTheApprovalHistory`.

### Manual end-to-end verification (real MVC pipeline, real SQL Server, real file storage)

Performed via HTTP against the running app (LocalDB `Faed`, `LocalFileStorage`), not only
through automated tests:
register → confirm email → apply as merchant → upload a genuine PDF (passes the fail-closed
inspector) → submit → admin approves → merchant creates a Draft listing → adds a `Size`
option with `M`/`L` values → adds variant `SNK-BLK-M` (5 units) → uploads a genuine PNG
product photo (passes the fail-closed inspector) → sets retail price + discount reason →
submits for review → admin approves → listing is `Live` and its photo is publicly reachable
anonymously → merchant adjusts stock (`+3`, audited, before/after correct) → merchant edits
the title on the `Live` listing → listing correctly returns to `PendingReview`, its photo
stops being publicly reachable, and the prior approval is preserved in history (this run is
what surfaced the bug above) → admin rejects the new version → an over-large negative stock
adjustment is rejected server-side without changing the stored quantity.

### Exit-criteria coverage (tasks/TASK-004)

| Exit criterion | Covered by |
|---|---|
| Variant combination is unique | `ListingTests.AddVariant_DuplicateOptionCombination_Throws` (aggregate); `ListingServiceTests.DuplicateOptionCombination_IsRejectedByTheDatabase_EvenAcrossTwoConcurrentContexts` (unique index, real SQL Server) |
| Stock is variant-level | `ListingTests.AddVariant_DistinctCombinations_BothSucceed` (Black/M, Black/L, White/M example); `ListingVariantTests` |
| Quantities cannot become negative | `ListingVariantTests.AdjustAvailable_NegativeDeltaExceedingStock_Throws`; DB check constraint; `ListingServiceTests.AdjustStock_CannotGoNegative_AndIsAudited`; manual verification |
| Live listing material edit requires moderation | `ListingTests.MaterialEdit_OnLiveListing_ReturnsToPendingReview_WithoutLosingApprovalHistory`; `ListingServiceTests.MaterialEdit_OnALiveListing_ReturnsItToPendingReview_AndPreservesTheApprovalHistory`; manual verification (the bug this caught) |
| Public cannot see non-Live data | `ListingServiceTests.NonLiveListing_ImageIsHiddenFromAnonymous_ButVisibleToOwnerAndAdmin_AndPublicOnceLive`; manual verification (anonymous 403 before approval, 200 after) |
| Defect media is distinguishable | `ListingMediaType.Defect` kept separate from `Product`/`Packaging`; workspace/admin views label it explicitly |
| Migration includes RowVersion from first variant creation | `20260901141629_AddListingsAndInventory` creates `ListingVariants.RowVersion` as `rowversion` in its `CREATE TABLE` |

### Additional coverage

- Manual stock adjustment is audited: `ListingServiceTests.AdjustStock_CannotGoNegative_AndIsAudited`.
- Optimistic concurrency on `ListingVariant.RowVersion`, proven against real SQL Server:
  `ListingServiceTests.AdjustStock_TwoConcurrentContexts_OnlyTheFirstSaveSucceeds` (AGENTS.md
  §7). The literal "two buyers race for the last unit" scenario is B2C order-flow scope
  (TASK-006); this proves the concurrency token itself stops a lost update on the variant it
  protects.
- Condition grade and discount reasons are independent on a listing:
  `ListingTests.ConditionGrade_And_DiscountReasons_AreIndependentOnAListing` (AGENTS.md Rule B
  — a Grade A item can still carry a past-season/overstock reason).
- Submission blockers are real, server-checked sentences, not just UI hints:
  `ListingTests.SubmitForReview_WithoutProductPhoto_Throws`,
  `SubmitForReview_WhenB2CWithoutRetailPrice_Throws`,
  `SubmitForReview_ReferencePriceWithoutEvidence_Throws`,
  `SubmitForReview_ReferencePriceNotHigherThanRetail_Throws`.
- `Approve` publishes as `Live` when stock exists and as `SoldOut` (addressable, not
  purchasable) when it does not: `Approve_WithStock_PublishesAsLive`,
  `Approve_WithNoStock_PublishesAsSoldOut_NotLive`.
- Editing while `PendingReview` is rejected server-side: `Edit_WhilePendingReview_Throws`.

### Not implemented (correctly deferred)

Public marketplace browsing/listing-detail pages (TASK-005), B2C ordering and reservation
(TASK-006), B2B negotiation/deals (TASK-007/008), demo listing seed data
(docs/12-SEED-DATA.md explicitly defers this until the phases that consume it exist).

### Post-review fixes (code review after initial TASK-004 completion)

A follow-up review of the initial TASK-004 implementation found six real gaps and four
lower-severity issues; all are fixed:

- **Reference-price evidence files were unreachable.** Uploads were stored and validated but
  had no read path — an admin could see an evidence *record* but never open the uploaded
  invoice/catalogue file itself, contradicting AGENTS.md §8 "the reviewing admin sees them
  all". Added `IListingMediaService.OpenReferencePriceEvidenceAsync` (owner/admin only, never
  public — unlike listing photography), `ListingMediaController.ShowEvidence`
  (`/listing-evidence/{id}`, `[Authorize]`), and a `DbSet<ListingReferencePriceEvidence>` on
  `IApplicationDbContext`/`ApplicationDbContext` to query it directly. Both the admin review
  screen and the merchant workspace now link to it.
- **A listing could publish while its merchant was no longer approved.** `ListingModerationService`
  re-checks `MerchantProfile.VerificationStatus == Approved` at the moment of `Approve`
  (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved") — a merchant
  suspended between submission and the admin's decision now fails the approval with a clear
  `Conflict` instead of silently publishing.
- **SoldOut listing photos were served to anonymous visitors.** `ListingMediaService.OpenImageAsync`
  treated `Live` and `SoldOut` as equally public; only `Live` is (docs/03-BUSINESS-RULES.md
  §2 "public users see only Live listings" — SoldOut is "addressable to authorized users",
  not anonymous traffic). Fixed to match `Listing.IsPubliclyVisible`.
- **A listing could be edited down to zero product photos while Live.** Removing an ordinary
  (non-defect) photo never re-ran the submission checks, so a merchant could delete every
  product photo from a published listing and it stayed `Live` with none — silently violating
  "at least one product photo". `Listing.RemoveMedia` now refuses to remove the last active
  `Product` photo outright (add a replacement first); `Defect`/`Packaging` photos are
  unaffected by this rule.
- **Category validation accepted the non-leaf sector root.** The reference-data list already
  excluded "Fashion Overstock" itself, but a crafted POST could still attach a listing to it.
  `MerchantListingService.ValidateDetailsAsync` now requires `ParentCategoryId != null`.
- **Archived listings still accepted stock adjustments.** The inventory screen already hid
  archived rows, but a direct POST to `InventoryService.AdjustStockAsync` worked regardless.
  Now rejected explicitly, mirroring `Listing.RequireNotArchived` on every other mutator.
- Lower-severity: `InventoryService.AdjustStockAsync` now catches a bare `DbUpdateException`
  (not only the concurrency subtype) so the `CK_ListingVariants_Quantities_NonNegative`
  backstop can never surface as an unhandled 500; `ListingReferencePriceEvidence.ReferenceUrl`
  is validated as an absolute `http`/`https` URL at the domain layer (it is rendered as a
  clickable link), closing a `javascript:`-scheme vector; `ListingModeration.AppendReason`
  compares exact `"; "`-separated segments instead of a whole-string substring check, so a
  new reason that happens to textually contain an old one can no longer be dropped (this
  branch is currently unreachable from any mutator — see the code comment — but the fix is
  correct defensively); removed three unused entity methods (`ListingVariant.Rename`,
  `ListingOption.Rename`, `ListingMedia.Describe`).
- New regression tests: `Faed.UnitTests.ListingTests.RemoveMedia_LastProductPhoto_Throws` (+2
  related), `AddReferencePriceEvidence_NonHttpUrl_Throws` (+1 valid-URL case); against real
  SQL Server, `Faed.IntegrationTests.ListingServiceTests.ReferencePriceEvidenceFile_IsRetrievableByOwnerAndAdmin_ButNotAnonymous`,
  `Approve_WhenTheMerchantIsNoLongerApproved_Fails_AndListingStaysPending`,
  `SoldOutListingImage_IsHiddenFromAnonymous_ButVisibleToOwnerAndAdmin`,
  `Create_WithTheNonLeafCategoryRoot_IsRejected`, `AdjustStock_OnAnArchivedListing_IsRejected`.
- The concurrency scope note from the original TASK-004 write-up stands: the literal "two
  B2C requests for the last unit" / "B2C vs accepted B2B deal" scenarios
  (AGENTS.md §7, docs/09-TEST-STRATEGY.md §3) require the B2C/B2B order flow and are
  correctly out of scope until TASK-006/007/008; what TASK-004 owns — the `ListingVariant`
  `rowversion` token itself stopping a lost update — is proven in
  `AdjustStock_TwoConcurrentContexts_OnlyTheFirstSaveSucceeds`.

### Post-review fixes, round 2

A second review pass (after round 1 above was already fixed and re-verified) found one
medium-severity gap, three low-severity issues and three nits; all are fixed:

- **A merchant could reverse an admin takedown.** `Listing.HideByAdmin` and the merchant's
  own `Hide` funnelled into the same `Hide()`, recording nothing about *who* hid the listing.
  `Restore()` then let the merchant republish any `Hidden` listing whose last review was an
  approval — exactly the state an admin's hide leaves it in, so a merchant could immediately
  undo an admin takedown with no signal to the admin that it happened
  (docs/16-PERMISSIONS-MATRIX.md "Moderate listing — Admin only"). Added a
  `Listing.HiddenByAdmin` flag (migration `20260901153402_AddListingHiddenByAdmin`), set by
  `HideByAdmin` and cleared only by the new `RestoreByAdmin`; the merchant's own `Restore`
  now throws when the flag is set. `IListingModerationService.RestoreAsync` (+
  `AdminActionType.ListingRestored`, audited) exposes admin restoration via a new
  Admin/ListingModeration/Restore action and a "Restore to the marketplace" button on the
  admin Details page; the merchant workspace shows "An administrator hid this listing.
  Contact Faed support…" instead of a Republish button whenever `HiddenByAdmin` is set.
- **`RefreshAvailability` could leave a Live listing advertising zero stock.** Two requests
  each depleting a *different* variant on the same listing each computed
  `AvailableUnits` from the `Variants` navigation loaded at the start of their own request, so
  neither saw the other's fresh depletion and neither flipped the listing to `SoldOut` (only
  the touched variant's own `rowversion` was checked; the untouched sibling's staleness never
  triggered a conflict). Self-healing on the next adjustment, as originally noted, but now
  mitigated: added `Listing.RefreshAvailability(int currentAvailableUnits, DateTime nowUtc)`,
  and `InventoryService.AdjustStockAsync` supplies a total computed from a fresh, untracked
  database query over the *other* active variants plus the just-applied value for the one it
  adjusted, instead of trusting the collection loaded at the top of the request. This closes
  the window from before the request started; true simultaneous commits are a separate,
  harder problem the reservation flow (a later task) will need to solve properly.
- **The multipart upload ceiling ignored the listings image cap.** `Program.cs` sized
  `FormOptions.MultipartBodyLengthLimit` from `MerchantVerification:MaxDocumentBytes` alone;
  TASK-004 added a second upload path (`Listings:MaxImageBytes`) with its own independent
  cap that the global ceiling never accounted for. Worked today only because the listings
  cap happens to be smaller. Now sized from `Math.Max` of both configured limits.
- **No-option single-variant listings were impossible through the UI.** The domain already
  permitted a variant with an empty option set (one plain SKU, no Size/Colour), but the
  workspace only ever showed "Add variant" once at least one option existed. A merchant
  selling one undifferentiated product had to invent an option to work around it. The
  "Add variant" form now also renders with zero options defined.
- Nits: the admin defect-photo warning now matches the discount reason's stable `Code`
  (`CosmeticDefect`) instead of its display `Name`, so a future catalog-management rename
  cannot silently disable it (`ListingDetailView` gained `DiscountReasonCodes` alongside
  `DiscountReasonNames`); the moderation queue's pending count uses a dedicated
  `GetPendingCountAsync` (`SELECT COUNT(*)`) instead of running the full queue query twice;
  the workspace's empty-variants-table row computes its `colspan` from whether the Actions
  column is actually rendered instead of a hard-coded `7`.
- New regression tests: `Faed.UnitTests.ListingTests.HideByAdmin_MarksTheListing_*`,
  `Restore_AfterTheMerchantsOwnHide_Succeeds`, `RestoreByAdmin_LiftsAnAdminTakedown_*`,
  `RestoreByAdmin_WhenNotHidden_Throws`, `RefreshAvailability_WithAnExplicitTotal_*`,
  `AddVariant_WithNoOptionsDefined_*`; against real SQL Server,
  `Faed.IntegrationTests.ListingServiceTests.MerchantRestore_AfterAnAdminTakedown_Fails_AndOnlyAdminRestoreWorks`,
  `AdjustStock_DepletingTwoVariantsAcrossSeparateRequests_ListingBecomesSoldOut` (its doc
  comment is explicit that this covers sequential-but-separate requests, not genuinely
  simultaneous commits — see the unit-level test for the deterministic proof of the
  underlying `RefreshAvailability` overload behaviour).
- Not implemented, deliberately: the review's other medium finding — that a rejected material
  edit to a `Live` listing overwrites the previously-approved content in place, with no
  retained approved/submitted version to revert to, and the re-reviewing admin sees only a
  comma-list of which fields changed rather than their before/after values — is a genuine
  partial gap against AGENTS.md §8 ("preserve merchant draft; submitted version; …
  approved/public version semantics"), but implementing full version snapshotting (or an
  automatic revert-to-last-approved on rejection) is a product-policy decision, not a bug fix,
  and the review's own text flagged it as needing team confirmation rather than a clear
  requirement. Left for the product owner to decide before it is built (AGENTS.md §12 "Do not
  silently resolve an unresolved product decision").

### Validation (TASK-004)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **184 passed (137 unit, 47 integration), 0 failed, 0 skipped**
  on a workstation where SQL Server LocalDB (`MSSQLLocalDB`) is reachable, stable across
  repeated runs; the integration suite is `[SkippableFact]` by design and skips rather than
  fails with no reachable SQL Server (docs/09-TEST-STRATEGY.md §2).
- `dotnet ef database update` — `AddListingsAndInventory` and
  `AddListingHiddenByAdmin` both apply from the existing schema;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- Manual end-to-end verification against the real running app, real SQL Server LocalDB and
  real (local) file storage — including the admin hide → merchant-restore-blocked →
  admin-restore → merchant-can-self-hide-again round trip, with the audit log confirmed
  (`ListingApproved`, `ListingHidden`, `ListingRestored`).

## TASK-003 — Catalog Foundations

### Behaviour implemented

- `Category` — self-referencing hierarchy (`ParentCategoryId`, `Name`, `Slug`, `SortOrder`,
  `IsActive`). Globally unique `Slug`; `OnDelete(Restrict)` so a populated branch is never
  cascade-removed. The sector is a data row, not an enum — future sectors are added as data
  (AGENTS.md §3, docs/14-FUTURE-EXPANSION.md).
- `ConditionGrade` — DB reference table (not an enum, docs/19 "Enums vs tables"), unique
  `Code`. Seeded A–D only; there is no Grade E in the schema or seed.
- `DiscountReason` — DB reference table, unique `Code`, optional `Description`. Kept
  independent of `ConditionGrade` — no FK or navigation between them
  (docs/adr/0003-CONDITION-VS-DISCOUNT-REASON.md).
- `Brand` — optional, admin-controlled only (no merchant-authored brands), unique `Slug`.
  Table created; no brands seeded (docs/13-OPEN-QUESTIONS.md items 5–6).
- `CatalogDataSeeder` (`Data/Seed`) — runtime idempotent seeder invoked from `Program.cs`
  after `IdentityDataSeeder`, in every environment. Matches each row on its natural key
  (`Code` / `Slug`), case-insensitively to match SQL Server's default collation, and
  inserts only when missing — so re-running never duplicates and never overwrites a later
  admin edit, even one that changed a key's casing. Schema must already be applied — the
  app does not migrate on startup.
- `IApplicationDbContext` extended with the four catalog `DbSet`s for later use-case
  services (TASK-004+).

### Decisions recorded (docs/13-OPEN-QUESTIONS.md)

- Item 4 (taxonomy depth): seed root + three launch categories only; deeper taxonomy
  deferred. The lower-level tree in docs/12-SEED-DATA.md is dev/demo data for a later task.
- Items 5–6 (Brand): optional everywhere, admin-controlled only.
- docs/12-SEED-DATA.md updated to list all eight discount reasons (adds
  `Other Approved Reason`) and to note the deferred sub-categories.

### Not implemented (correctly deferred)

Catalog admin UI / CRUD (TASK-010), listings, options, variants, reference-price evidence
(TASK-004), brand seed data.

## TASK-001–003 review hardening

A review of the completed TASK-001/002/003 work produced the following fixes. No new
feature scope; TASK-004 remains not started.

1. **Verification uploads — active/executable content (fails closed).**
   `VerificationDocumentValidator.ValidatePayload` inspects the whole buffered upload rather
   than trusting a signature. Images are parsed structurally: JPEG marker by marker with
   `EOI` required as the last byte, PNG chunk by chunk with every CRC32 verified, `IEND`
   required last, and the raster inflated and matched against the `IHDR` geometry. No file
   may contain a `<script` / `<?php` marker or an embedded ZIP/RAR/7z/ELF/PE/second-PDF
   payload anywhere. The PDF active-content scan (`/JavaScript`, `/Launch`, `/EmbeddedFile`,
   `/RichMedia`) runs over the raw bytes, over a copy with PDF name hex-escapes resolved
   (`/Java#53cript` → `/JavaScript`), and over the decoded content of every Flate stream —
   the filter name is de-escaped first, so `/Flate#44ecode` is still recognised, and a
   `/DecodeParms` predictor is reversed so the bytes scanned are the bytes a reader consumes.
   A PDF is **rejected** (not trusted) when any part cannot be inspected: encryption, LZW,
   an external stream source, an unrecognised/indirect filter or an irreversible predictor, a
   Flate stream that will not inflate, or exhausting the 64 MB / 512-stream inflate budget.

   The structural rules are set at the point where full inspection is still possible, not
   tighter: multiple `%%EOF`/`startxref` markers (linearized and incrementally-saved PDFs),
   PDF 1.5+ cross-reference streams and PNG ancillary chunks are all accepted, because every
   byte is scanned regardless. An earlier, stricter revision refused all of them and thereby
   rejected 8 of 10 real PDFs and 6 of 7 real PNGs — friction with no safety gain. Measured
   on unmodified real-world files the validator now accepts 10/10 PDFs, 3/3 JPEGs and 7/7
   PNGs while every hostile fixture in `VerificationDocumentValidatorTests` is still refused.
   Recorded as `docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md`
   (docs/08-SECURITY-AND-PRIVACY.md §3 "no executable content"). Test fixtures use a real
   minimal PDF instead of the string `"%PDF-1.4 fake"`.
2. **Privileged service methods now recheck the actor.** `IUserRoleService.IsInRoleAsync`
   was added; `MerchantVerificationService` approve/reject/suspend/reinstate and
   `OpenVerificationDocumentAsync` return `Forbidden` unless the supplied user id actually
   holds the `Admin` role — the MVC `AdminOnly` policy is no longer the only gate
   (docs/08-SECURITY-AND-PRIVACY.md §2).
3. **Approval is atomic with the Merchant-role grant.** The status change, its audit row
   and the `AddToRoleAsync` call now run inside one `IApplicationDbContext.BeginTransactionAsync`
   transaction. A permanent role-sync failure rolls the whole decision back (returning a
   retryable `Conflict`) instead of leaving an approved profile that can never be
   re-approved (AGENTS.md §7).
4. **Merchant concurrency conflicts return `Conflict`, not HTTP 500.** `SaveDraftAsync` and
   `RemoveDocumentAsync` now catch `DbUpdateConcurrencyException` and surface a conflict
   result, matching `SubmitForReviewAsync` and the admin decision path.
5. **Catalog root lookup is collation-independent.** `CatalogDataSeeder` loads existing
   categories once and matches every slug — the `fashion-overstock` root included — with an
   ordinal-ignore-case comparer, instead of delegating the root lookup to the database
   where a case-sensitive server collation could miss it and allow a second root insert.
6. **Admin decision form no longer overflows narrow screens.** The reject form's inline
   `min-width: 18rem` was replaced with a `.faed-decision-row__grow` rule
   (`flex: 1 1 16rem; min-width: 0`) so the panel stays within a 320px viewport.
7. **Upload validation is field-level.** `VerificationController.UploadDocument` re-renders
   the verification page with the error bound to the document-type or file field
   (`asp-validation-for`) instead of a single `TempData` banner after a redirect
   (docs/07-UI-UX-SPEC.md, faed-responsive-accessibility "field-level errors").
8. **Integration tests fail (not skip) when SQL Server should have been there.** The suite
   still skips gracefully on a developer workstation that has no reachable SQL Server and was
   never told where to find one (unit tests run, `dotnet test` stays green with a lower count
   — the documented behaviour, docs/09 §2). It hard-fails when `CI=true` *or* when
   `Faed_TEST_CONNECTION` is set but unreachable, so neither a green pipeline nor a typo in
   that variable can silently omit the SQL Server proof. README documents pointing
   `Faed_TEST_CONNECTION` at a container when LocalDB is unavailable.

### Post-audit fixes

A second, evidence-based audit of the work above found three defects in it. All are fixed.

9. **The hosted integration tests were writing to the application database.**
   `DependencyInjection.AddPersistence` read `ConnectionStrings:DefaultConnection` eagerly
   during service registration, before `WebApplicationFactory`'s configuration override was
   merged — so the whole web test host used the application's `Faed` catalog while the
   disposable `Faed_WebTests` catalog was created, migrated and dropped unused. The
   connection string is now resolved from the built `IConfiguration` when the context options
   are created, the factory re-registers the context against the test catalog as defence in
   depth, and `TestHostDatabaseTargetTests` asserts the hosted context really targets
   `Faed_WebTests`. This violated docs/09-TEST-STRATEGY.md §2 ("never read the application's
   normal connection string") and made every web test order- and history-dependent.
10. **Two of the finding-4 regression tests did not pass.**
    `SaveDraft_WhenAnotherMerchantClaimsTheSlug_RetriesWithTheNextSlug` and
    `SaveDraft_WhenAnotherRequestCreatesTheUsersFirstApplication_ReturnsConflict` failed
    because leftover rows in the shared application database made the injected "racing" write
    collide with an earlier run instead of with the test's own insert. Fixing item 9 makes
    both deterministic; the service logic itself was already correct.
11. **A deactivated administrator still passed the service-level role recheck.**
    `UserRoleService.IsInRoleAsync` now requires `ApplicationUser.IsActive`, since disabling
    an account leaves its role rows in place (docs/08-SECURITY-AND-PRIVACY.md §2).

CI is now real rather than hypothetical: `.github/workflows/ci.yml` runs restore, build,
unit tests and integration tests against a SQL Server service container on push and pull
request (docs/09-TEST-STRATEGY.md §6).

### Responsive / accessibility review (findings 6–7)

- Responsive: fixed one horizontal-overflow source (the admin decision form on ~320px).
  No other overflow, tap-target or small-width spacing issues found in the merchant/admin
  verification views.
- Accessibility: upload errors are now field-level with `.faed-field__error` messages and a
  model-only validation summary; labels, semantic headings, focus-visible outlines and
  non-color status text were already in place and unchanged.

## TASK-002 — Merchant Verification

### Behaviour implemented

- `MerchantProfile` aggregate (1:1 with the Identity user) with the verification state
  machine `Draft → PendingReview → Approved / Rejected / Suspended` and
  `Suspended → Approved` (reinstate). A user can never self-assign `Approved`; every
  decision is a guarded domain transition that records the reviewing admin, timestamp and
  reason.
- `MerchantVerificationDocument` — private evidence files. Only a storage object key +
  metadata are stored; there is no public URL. Removing a document soft-deactivates it so
  history is retained (safe reversible default for open question item 3).
- `AdminActionLog` — append-only audit of merchant approve/reject/suspend/reinstate and
  every verification-document access.
- `IFileStorage` abstraction + `LocalFileStorage` development implementation: writes to a
  private directory outside `wwwroot` (`{ContentRoot}/App_Data/private-storage` by
  default), generates the object key server-side, validates keys on read against path
  traversal.
- Service layer: `IMerchantVerificationService` with use-case methods, a `Result`
  pattern (Validation / NotFound / Forbidden / Conflict), server-side upload validation
  (size, content type, extension), and `IApplicationDbContext` as the persistence seam
  (not a generic repository).
- Authorization policies `ApprovedMerchant` (DB-checked verification state, not a role)
  and `AdminOnly`, plus policy name constants (`Authorization/FaedPolicies`) and role name
  constants (`Models/Identity/FaedRoles`).
- Merchant self-service UI (`/Merchant/Verification`) and admin queue/detail/decision UI
  (`/Admin/MerchantVerification`) using a new Faed design-token CSS layer.
- The `Merchant` role is granted on approval for nav/UI convenience; selling capability
  is always governed by verification state.

### Open questions handled with reversible defaults

- Accepted Jordanian document types (item 1): flexible `MerchantVerificationDocumentType`
  enum — `CommercialRegistration`, `TaxRegistration`, `Other`. No personal/national
  identity document is offered (docs/08-SECURITY-AND-PRIVACY.md §14).
- Email confirmation before application (item 2): none added beyond the existing
  `RequireConfirmedAccount`; any authenticated user may start an application.
- Rejected-document retention (item 3): documents are retained (soft-deactivated), never
  auto-deleted.

### Post-review hardening (TASK-002 code review)

- Uploaded document contents are now validated by byte signature (real `%PDF-`, JPEG,
  PNG magic bytes), the content type must pair with the file extension, and admin
  downloads are served as attachments (`Content-Disposition: attachment`, `nosniff`),
  never inline.
- `MerchantProfile` carries a SQL Server `rowversion` concurrency token; competing admin
  decisions surface as a `Conflict` result instead of a silent last-write-wins.
- Rejection/suspension reason length is validated (≤ `MerchantProfile.MaxDecisionReasonLength`
  = 1000) before persistence in both the domain and the application service.
- `LocalFileStorage` is registered only outside Production (Production resolves a
  fail-fast `IFileStorage`); the configured storage root is rejected if it resolves
  inside the web root.
- Undefined document-type enum values are rejected (`Enum.IsDefined` in the validator,
  `[EnumDataType]` on the input model).
- Verification timestamps render in Asia/Amman via `AmmanTime`, not raw UTC.
- The multipart upload ceiling tracks `MerchantVerification:MaxDocumentBytes`
  (`FormOptions.MultipartBodyLengthLimit`) and the merchant view shows the configured
  limit instead of a hard-coded "10 MB".

### Not implemented (correctly deferred)

Categories, listings, variants, orders, B2B, storefront pages, email provider, cloud
storage provider. `MerchantLocation` / `MerchantDeliveryZone` are in the domain doc but
belong to fulfilment phases and were not modelled.

## Solution structure

```text
Faed.slnx
src/
└── Faed.Web/
    ├── Models/
    │   ├── Entities/       # MerchantProfile, MerchantVerificationDocument, AdminActionLog,
    │   │                   # Category, ConditionGrade, DiscountReason, Brand,
    │   │                   # Listing (+ Option/OptionValue/Variant/VariantOptionValue/Media/
    │   │                   # DiscountReason join/ReferencePriceEvidence/Moderation),
    │   │                   # InventoryAdjustment
    │   ├── Enums/          # MerchantVerificationStatus, *DocumentType, AdminActionType,
    │   │                   # ListingStatus, ListingMediaType, ListingModerationStatus,
    │   │                   # ReferencePriceEvidenceType, InventoryAdjustmentType
    │   ├── Identity/       # ApplicationUser, FaedRoles
    │   └── DomainException.cs
    ├── Data/
    │   ├── ApplicationDbContext.cs   # + IApplicationDbContext, shared with Identity
    │   ├── Configurations/           # IEntityTypeConfiguration<T> for each entity
    │   ├── Migrations/               # EF Core migrations
    │   └── Seed/           # IdentityDataSeeder, CatalogDataSeeder (both idempotent)
    ├── Services/
    │   ├── Abstractions/   # IApplicationDbContext, IFileStorage, IUserRoleService, IClock
    │   ├── Common/          # Result.cs, Slug.cs
    │   ├── Merchants/      # IMerchantVerificationService + implementation, models, validator, slug
    │   ├── Listings/        # IMerchantListingService, IInventoryService,
    │   │                    # IListingModerationService, IListingMediaService + implementations,
    │   │                    # ListingOptions, ListingImageValidator, ListingQueries, models
    │   ├── Marketplace/     # IPublicMarketplaceService + implementation (anonymous-safe reads)
    │   ├── Storage/        # LocalFileStorage
    │   ├── UserRoleService.cs
    │   └── SystemClock.cs
    ├── Authorization/      # FaedPolicies, ApprovedMerchant handler, ClaimsPrincipal ext.
    ├── Controllers/         # Home, Shop, Listing, Store (public marketplace),
    │                        # ListingMediaController (public/private image serving)
    ├── Areas/{Admin,Merchant,Identity}/
    │   ├── Admin/Controllers/       # MerchantVerificationController, ListingModerationController
    │   └── Merchant/Controllers/    # VerificationController, ListingsController, InventoryController
    ├── ViewModels/         # ErrorViewModel, Marketplace/ (Shop/Store/Listing page + filter
    │                       # models; area-local view models under each Area/ViewModels)
    ├── Rendering/          # AmmanTime, MerchantStatusDisplay, ListingStatusDisplay (view-only helpers)
    ├── DependencyInjection.cs   # AddFaedPlatform composition helper
    └── Program.cs
tests/
├── Faed.UnitTests/         # MerchantProfile / Listing / ListingVariant state machines,
│                           # upload validator, slug, foundation
└── Faed.IntegrationTests/  # SQL Server persistence; merchant-verification and listing
                            # services + MVC authorization (WebApplicationFactory + test
                            # auth scheme); listing/variant/moderation/inventory concurrency
```

Both test projects reference `src/Faed.Web` directly. There are no other production
projects and no project-reference layering.

## Migrations

- `20260831174908_InitialIdentity` (`src/Faed.Web/Data/Migrations`) — ASP.NET Core Identity
  schema for `ApplicationUser` (adds `CreatedAtUtc` default `SYSUTCDATETIME()`, `IsActive`
  default `true`). Replaces the Visual Studio template's `00000000000000_CreateIdentitySchema`.
- `20260831205644_AddCatalog` (`src/Faed.Web/Data/Migrations`) — `Categories` (unique
  `Slug`, self-referencing FK `OnDelete(Restrict)`, index on `(ParentCategoryId, SortOrder)`),
  `ConditionGrades` (unique `Code`), `DiscountReasons` (unique `Code`), `Brands` (unique
  `Slug`). All Guid keys `ValueGeneratedNever`. `has-pending-model-changes` reports clean
  after build.
- `AddMerchantVerification` (`src/Faed.Web/Data/Migrations`) — `MerchantProfiles` (unique
  `UserId`, unique `PublicSlug`, indexed `VerificationStatus`; enum stored as text;
  `rowversion` concurrency token; restricted delete from `AspNetUsers`),
  `MerchantVerificationDocuments` (cascade from profile), `AdminActionLogs`. All Guid keys
  are `ValueGeneratedNever` (assigned by the entity constructor). Migration IDs are
  unchanged by the restructure; `dotnet ef migrations has-pending-model-changes` reports
  clean.
- `20260901141629_AddListingsAndInventory` (`src/Faed.Web/Data/Migrations`) — `Listings`
  (unique `Slug`, restricted FKs to `MerchantProfiles`/`Categories`/`ConditionGrades`/`Brands`,
  `rowversion`), `ListingOptions`/`ListingOptionValues` (unique per-listing name / per-option
  value), `ListingVariants` (`rowversion` present from this first migration; check constraint
  `CK_ListingVariants_Quantities_NonNegative`; unique `(ListingId, Sku)` and
  `(ListingId, OptionCombinationKey)`), `ListingVariantOptionValues`, `ListingMedia`,
  `ListingDiscountReasons` (restricted FK to `DiscountReasons`), `ListingReferencePriceEvidence`,
  `ListingModerations`, `InventoryAdjustments` (restricted FK to `ListingVariants` — the audit
  trail outlives the variant edit that produced it). `has-pending-model-changes` reports
  clean after build.
- `20260901153402_AddListingHiddenByAdmin` (`src/Faed.Web/Data/Migrations`) — adds
  `Listings.HiddenByAdmin` (`bit NOT NULL DEFAULT 0`), distinguishing an admin takedown from
  the merchant's own hide so only an admin can lift the former. `has-pending-model-changes`
  reports clean after build.

## Persistence

- One application `DbContext` (`ApplicationDbContext`) in `src/Faed.Web/Data`, shared
  with Identity and exposed to services through `IApplicationDbContext`.
- SQL Server; local development uses LocalDB database `Faed` (non-secret connection
  string in `appsettings.json`).
- EF Core `rowversion` concurrency is introduced with the inventory model in a later task.

## Private file storage

- `IFileStorage` (`Services/Abstractions`) with `LocalFileStorage` (`Services/Storage`) for development.
- Root defaults to `{ContentRoot}/App_Data/private-storage` (gitignored) or
  `FileStorage:LocalRootPath` when set; startup fails if the resolved root is inside the
  web root. Object keys are server-generated and validated against traversal on read.
- `LocalFileStorage` is registered only outside Production. In Production `IFileStorage`
  resolves to a fail-fast stub until a cloud object store is bound to the interface
  (docs/06-ARCHITECTURE.md §8).

## Identity

- Individual Accounts (ASP.NET Core Identity) preserved from the Visual Studio baseline.
- `AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>()`.
- Roles `Buyer`, `Merchant`, `Admin` seeded idempotently at startup. The `Merchant` role
  is additionally granted to a user on verification approval (idempotent).
- Optional development admin seeding: only when `Faed:AdminSeed:Email` +
  `Faed:AdminSeed:Password` are supplied (user secrets / env) and the environment is not
  Production. No password is stored in source control. Seeds for the other roles belong to
  the phase that first needs one (AGENTS.md §12).
- Merchant verification is a domain state, not an Identity role. Policies: `AdminOnly`
  (role check), `ApprovedMerchant` (per-request DB check of verification status).

## Locked product choices

- English MVP website
- Amman
- Fashion Overstock launch
- Clothing / Shoes / Bags & Accessories
- Verified merchants only as sellers
- B2C + B2B
- no real online payment
- no platform shipping
- no warehouse/fleet
- no used goods
- no Grade E

## Current validation (TASK-002 + single-project architecture audit)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — 52 passed (30 unit, 22 integration), 0 failed. Integration
  tests run against allow-listed LocalDB test databases (`Faed_IntegrationTests`,
  `Faed_WebTests`) which they create and drop; they skip when no SQL Server is reachable,
  override any configured catalog, and never touch the app connection string. Coverage
  includes Home/Login/Register availability, seeded roles, destructive database guards,
  byte-signature rejection of renamed
  uploads, competing-admin concurrency conflict, over-length reason rejection, and a
  non-admin POST to every decision action (Approve/Reject/Suspend/Reinstate) returning
  403 through the real MVC pipeline with no state or audit change.
- `dotnet ef database update` — both migrations apply from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs (Development); Home renders; anonymous `/Merchant/Verification` and
  `/Admin/MerchantVerification` redirect to login; role seeding idempotent; admin seed
  correctly skipped when unconfigured.

### Exit-criteria coverage (tasks/TASK-002)

| Exit criterion | Covered by |
|---|---|
| Buyer cannot access merchant-only workflow | `AdminQueue_Buyer_IsForbidden`, `SellingProbe_*` (pending → 403) |
| Pending merchant cannot submit listings | `ApprovedMerchant` policy DB-checks status; `IsApprovedMerchant` false for pending; `SellingProbe_PendingMerchant_IsForbidden` |
| Admin can approve/reject | `Approve_MovesToApproved_*`, `Reject_RequiresReason_AndRecordsIt`, `AdminQueue_Admin_IsAllowed` |
| Non-admin cannot approve/reject/suspend/reinstate | `AdminDecisions_PostedByBuyer_AreForbidden_AndChangeNoStateOrAudit`, `Decisions_WithoutAnAdminUserId_AreForbidden_AndChangeNothing` |
| Private document URL is not public | no route exposes the storage key; `VerificationDocument_Anonymous_IsChallenged` |
| Unauthorized document request fails | `VerificationDocument_Buyer_IsForbidden` |
| Audit entry is persisted | `Approve_*_WritesAudit`, `OpenVerificationDocument_*_AuditsAccess` |
| Relevant tests pass | full suite green |

## Current validation (TASK-003)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — after the TASK-001–003 review hardening and the post-audit
  fixes: **135 passed (100 unit, 35 integration), 0 failed, 0 skipped** on a workstation
  where SQL Server LocalDB (`MSSQLLocalDB`) is reachable. `dotnet build Faed.slnx` succeeds
  with 0 warnings and 0 errors. The integration tests are `[SkippableFact]` by design
  (docs/09-TEST-STRATEGY.md §2): on a machine with **no** reachable SQL Server they skip
  rather than fail, so a green run there reports fewer executed tests — the SQL Server exit
  criteria for TASK-001/002/003 are only proven on a run where the integration suite
  actually executes. New coverage: catalog entity invariants; EF model shape
  (condition/discount-reason independence, self-referencing `Category`, unique
  slug/code indexes); startup seeds A–D + eight reasons + launch taxonomy; no Grade E;
  seeder is idempotent on re-run and when an existing slug differs only by casing; a second
  root `Category` persists to SQL Server with no schema change; DB-level unique-slug
  enforcement for `Category` and `Brand`. Review hardening added: `ValidatePayload` rejects
  `%%EOF`-less PDFs, JavaScript/Launch/EmbeddedFile/RichMedia PDFs (including markers hidden
  behind PDF name hex-escapes or inside a Flate stream, and filter names hex-escaped to
  `/Flate#44ecode`), encrypted / LZW / unrecognised-filter / non-inflatable-stream PDFs,
  and script-carrying image polyglots, while still accepting a normal compressed PDF and an
  image-only-stream PDF; a non-admin actor calling the verification service directly is
  `Forbidden` and changes no state or audit.
  Post-audit coverage: the hosted `ApplicationDbContext` targets the disposable
  `Faed_WebTests` catalog and never the application database; the slug and first-application
  unique-index races resolve to the right result while an unrelated `DbUpdateException` still
  propagates; `/JavaScript` visible only after a `/DecodeParms` predictor is reversed is
  rejected, as is JavaScript introduced by an appended PDF revision, a script marker hidden
  in a compressed PNG `zTXt`, an archive in a compressed `iTXt`, a `zTXt` that will not
  inflate, an unknown *critical* PNG chunk, and `/DecodeParms` carrying an unknown key —
  while an incrementally-saved PDF, a cross-reference-stream PDF, a predictor-encoded stream,
  clean content behind a hex-escaped `/Flate#44ecode` filter name, and a PNG carrying `tEXt`,
  `iCCP` and vendor provenance chunks are all accepted.
- `dotnet ef database update` — `AddCatalog` applies from the existing schema;
  `dotnet ef migrations has-pending-model-changes` reports no drift (after build).
- App runs (Development); Home and `/Identity/Account/Login` return 200; `CatalogDataSeeder`
  populates `ConditionGrades` (4), `DiscountReasons` (8) and `Categories` (4) and is a no-op
  on subsequent starts.

### Exit-criteria coverage (tasks/TASK-003)

| Exit criterion | Covered by |
|---|---|
| Migration applies from an empty database | `SqlServerPersistenceTests` (all migrations), `dotnet ef database update` |
| Seed runs repeatedly without duplication | `CatalogSeedTests.Seed_RunAgain_AddsNoDuplicateRows`, `Seed_IsIdempotent_WhenAnExistingSlugDiffersOnlyByCasing` |
| A second root category can be added by data alone | `CatalogSeedTests.SecondRootCategory_CanBeAddedByDataAlone_WithNoSchemaChange` |
| Condition and discount reason separate; Grade E absent | `CatalogModelTests.ConditionGrade_And_DiscountReason_AreIndependent`, `CatalogSeedTests.ConditionGrades_DoNotIncludeGradeE` |
| No catalog/condition/reason values hard-coded in code or views | reference tables + `CatalogDataSeeder`; no views added |
| Catalog unit/integration tests pass | full suite green |
| `dotnet build` succeeds; `PROJECT_STATUS.md` updated | this document |

## Validation (TASK-001)

- `dotnet build Faed.slnx` — succeeds, 0 warnings.
- `dotnet test Faed.slnx` — 3 passed (2 unit, 1 SQL Server integration against LocalDB
  `Faed_IntegrationTests`, which the test creates and drops via its own
  `Faed_TEST_CONNECTION` variable — never the app connection string).
- `dotnet ef database update` — `InitialIdentity` applies from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs; Home renders; Register/Login reachable; registration creates a user with
  `CreatedAtUtc`/`IsActive` populated; role seeding is idempotent across restarts.
