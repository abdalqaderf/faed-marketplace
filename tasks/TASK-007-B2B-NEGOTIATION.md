# TASK-007 — B2B Negotiation

## Objective
Implement structured merchant-to-merchant offer and counter-offer history.

## Deliverables
- `B2BNegotiation`
- immutable `B2BOfferRevision`
- variant quantity lines
- MOQ validation
- offer expiry
- accept/reject/counter commands
- seller/buyer views

## Critical rules
- Old revisions are never overwritten.
- Active revision expiry blocks acceptance.
- Buying merchant cannot be the seller merchant.
- No stock is permanently consumed by negotiation alone.

## Exit criteria
A complete offer/counter-offer history is auditable and permission-safe.
