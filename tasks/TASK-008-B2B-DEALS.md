# TASK-008 — B2B Deal and Fulfillment

## Objective
Turn an accepted B2B offer revision into an atomic stock reservation and fulfillment deal.

## Deliverables
- `B2BDeal`
- deal lines
- accepted-term snapshots
- reservation expiry
- Pickup / SellerArrangedShipping
- shipment reference
- fulfillment states
- expiry/release job
- completion/cancellation

## Mandatory tests
- all requested variants reserve atomically or none do;
- B2C vs B2B competition is safe;
- two B2B accept attempts cannot oversell;
- repeated expiry processing does not double-release;
- completion moves Reserved -> Sold.

## Exit criteria
End-to-end merchant-to-merchant deal works safely against SQL Server.
