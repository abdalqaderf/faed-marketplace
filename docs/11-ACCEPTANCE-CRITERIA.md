# 11 — MVP Acceptance Criteria

The project is not MVP-complete until these statements are true.

> **TASK-011 verification.** Every criterion below was verified during the TASK-011 hardening
> and delivery pass. Evidence is the automated suite (`dotnet test Faed.slnx` — unit +
> SQL Server integration), the deterministic demo data set
> (`src/Faed.Web/Data/Seed/DemoDataSeeder.cs`), and the audit in
> `docs/24-DELIVERY-AND-HARDENING.md`. Items that depend on unresolved infrastructure
> decisions are listed as known limitations in that document and in `DEPLOYMENT.md` §3;
> none of them is an MVP functional gap.

## Identity / merchant trust
- [x] A normal buyer cannot sell.
- [x] A merchant account can submit verification.
- [x] A pending/rejected/suspended merchant cannot submit a listing.
- [x] Admin can approve/reject merchant.
- [x] Verification document is not publicly reachable.
- [x] Verification actions are audited.

## Catalog
- [x] Categories are DB-driven.
- [x] Fashion launch categories are seeded.
- [x] Condition Grades A-D are seeded.
- [x] Discount reasons are separate from condition.
- [x] No Grade E is exposed.

## Listings
- [x] Merchant can create Draft.
- [x] Merchant can define Size/Color through generic options.
- [x] Each variant has independent inventory.
- [x] Listing supports B2C, B2B, or both.
- [x] Defect photos can be distinguished from normal photos.
- [x] Material edits require moderation.
- [x] Public sees only Live listings.
- [x] Admin can approve/reject/hide.

## B2C
- [x] Order can contain multiple variants/items from one merchant.
- [x] Multi-merchant order is rejected.
- [x] Unit prices/totals are server-generated.
- [x] Stock reserves atomically.
- [x] Concurrent last-unit test passes on SQL Server.
- [x] Cancel/expiry releases stock.
- [x] Completion moves reserved stock to sold.
- [x] Pickup works.
- [x] Merchant delivery works.
- [x] Buyer sees own order history only.

## B2B
- [x] Verified merchant can submit offer.
- [x] Offer can contain variant quantities.
- [x] MOQ is enforced.
- [x] Counter-offer creates immutable new revision.
- [x] Offer expiration is separate from deal reservation expiration.
- [x] Acceptance reserves all lines atomically.
- [x] Accepted deal has independent fulfillment state.
- [x] Seller-arranged shipping reference can be stored.
- [x] Cancellation/expiry releases stock exactly once.
- [x] Completion moves stock to sold.

## Trust
- [x] Review requires Completed transaction.
- [x] Duplicate review blocked.
- [x] Dispute requires transaction participation.
- [x] Admin can resolve dispute.
- [x] Resolution is audited.

## Analytics
- [x] Merchant sees recovered value.
- [x] Merchant sees units sold/sell-through.
- [x] Merchant sees B2C vs B2B.
- [x] Merchant sees cancellation count.
- [x] Merchant sees active negotiations/stale inventory.

## UI
- [x] All system UI is English.
- [x] Mobile-first pass completed.
- [x] Condition meaning visible, not only letter grade.
- [x] Discount reason visible.
- [x] Defect information visible.
- [x] Empty/error states implemented.
- [x] Basic accessibility checks completed.

## Engineering
- [x] Solution builds cleanly.
- [x] Migrations apply from empty DB.
- [x] No secrets in repo.
- [x] Critical tests pass.
- [x] No EF entities passed directly to Razor views.
- [x] No future modules scaffolded unnecessarily.
