# TASK-006 — B2C Orders

## Objective
Implement safe single-merchant consumer ordering with variant-level reservation.

## Deliverables
- Order + OrderItems
- same-merchant cart/order builder
- server-calculated totals
- Pickup
- MerchantDelivery
- transactional stock reservation
- order status service
- cancellation/completion
- configurable reservation expiry
- buyer and merchant order views

## Mandatory tests
- forged price rejected/recomputed;
- multi-merchant order rejected;
- two buyers compete for last unit: one succeeds;
- cancellation releases;
- completion moves Reserved -> Sold;
- unauthorized order access blocked.

## Exit criteria
End-to-end B2C purchase can complete safely against SQL Server.
