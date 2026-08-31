# 11 — MVP Acceptance Criteria

The project is not MVP-complete until these statements are true.

## Identity / merchant trust
- [ ] A normal buyer cannot sell.
- [ ] A merchant account can submit verification.
- [ ] A pending/rejected/suspended merchant cannot submit a listing.
- [ ] Admin can approve/reject merchant.
- [ ] Verification document is not publicly reachable.
- [ ] Verification actions are audited.

## Catalog
- [ ] Categories are DB-driven.
- [ ] Fashion launch categories are seeded.
- [ ] Condition Grades A-D are seeded.
- [ ] Discount reasons are separate from condition.
- [ ] No Grade E is exposed.

## Listings
- [ ] Merchant can create Draft.
- [ ] Merchant can define Size/Color through generic options.
- [ ] Each variant has independent inventory.
- [ ] Listing supports B2C, B2B, or both.
- [ ] Defect photos can be distinguished from normal photos.
- [ ] Material edits require moderation.
- [ ] Public sees only Live listings.
- [ ] Admin can approve/reject/hide.

## B2C
- [ ] Order can contain multiple variants/items from one merchant.
- [ ] Multi-merchant order is rejected.
- [ ] Unit prices/totals are server-generated.
- [ ] Stock reserves atomically.
- [ ] Concurrent last-unit test passes on SQL Server.
- [ ] Cancel/expiry releases stock.
- [ ] Completion moves reserved stock to sold.
- [ ] Pickup works.
- [ ] Merchant delivery works.
- [ ] Buyer sees own order history only.

## B2B
- [ ] Verified merchant can submit offer.
- [ ] Offer can contain variant quantities.
- [ ] MOQ is enforced.
- [ ] Counter-offer creates immutable new revision.
- [ ] Offer expiration is separate from deal reservation expiration.
- [ ] Acceptance reserves all lines atomically.
- [ ] Accepted deal has independent fulfillment state.
- [ ] Seller-arranged shipping reference can be stored.
- [ ] Cancellation/expiry releases stock exactly once.
- [ ] Completion moves stock to sold.

## Trust
- [ ] Review requires Completed transaction.
- [ ] Duplicate review blocked.
- [ ] Dispute requires transaction participation.
- [ ] Admin can resolve dispute.
- [ ] Resolution is audited.

## Analytics
- [ ] Merchant sees recovered value.
- [ ] Merchant sees units sold/sell-through.
- [ ] Merchant sees B2C vs B2B.
- [ ] Merchant sees cancellation count.
- [ ] Merchant sees active negotiations/stale inventory.

## UI
- [ ] All system UI is English.
- [ ] Mobile-first pass completed.
- [ ] Condition meaning visible, not only letter grade.
- [ ] Discount reason visible.
- [ ] Defect information visible.
- [ ] Empty/error states implemented.
- [ ] Basic accessibility checks completed.

## Engineering
- [ ] Solution builds cleanly.
- [ ] Migrations apply from empty DB.
- [ ] No secrets in repo.
- [ ] Critical tests pass.
- [ ] No EF entities passed directly to Razor views.
- [ ] No future modules scaffolded unnecessarily.
