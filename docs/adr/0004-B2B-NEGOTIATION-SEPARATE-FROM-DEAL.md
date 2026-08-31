# ADR 0004 — B2B Negotiation Separate from Accepted Deal

## Status
Accepted.

## Decision
Model `B2BNegotiation` + immutable `B2BOfferRevision` separately from `B2BDeal`.

## Why
Offer/counter-offer lifecycle is different from fulfillment lifecycle.
`OfferExpiresAt` is not the same as `ReservationExpiresAt`.

## Consequences
- Counter-offers are auditable.
- Accepted terms are snapshotted.
- Stock reservation belongs to the accepted deal.
