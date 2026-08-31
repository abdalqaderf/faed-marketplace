# 17 — Data and Domain Invariants

These rules should be enforced at the strongest practical layer: database constraints, application logic, transactions, and tests.

## Inventory

For every `ListingVariant`:
- `AvailableQuantity >= 0`
- `ReservedQuantity >= 0`
- `SoldQuantity >= 0`
- quantity changes are auditable;
- stock is never silently overwritten;
- `RowVersion` protects concurrent updates.

No transaction may reserve more than current available stock.

## Listing

- A Listing belongs to exactly one Merchant.
- A public Listing must be `Live`.
- A `Live` Listing's merchant must be approved.
- At least one of `AllowB2C` or `AllowB2B` must be true before publication.
- `RetailPrice` is required when `AllowB2C = true`.
- `WholesaleMinQuantity` is required and positive when `AllowB2B = true`.
- Grade E is invalid for Fashion MVP.
- Condition and DiscountReason are independent.

## Variant

- A sellable variant belongs to exactly one Listing.
- One Listing cannot have duplicate option-value combinations.
- Variant SKU is unique at least within the merchant/listing policy selected by implementation.
- Inactive variants cannot be newly purchased.

## Money

- Currency for MVP is JOD.
- Monetary amounts use `decimal(18,3)`.
- Quantity > 0 for order/deal lines.
- Line total = server-calculated unit price × quantity.
- Order/deal total = server-calculated line totals + eligible fulfillment/shipping snapshot.

## B2C Order

- Order has exactly one Buyer.
- Order has exactly one Selling Merchant.
- Order has at least one OrderItem.
- All items belong to the same selling merchant.
- Order price snapshots never change after creation.
- Stock release/consume occurs exactly once per reservation lifecycle.

## B2B Negotiation

- Selling and buying merchants cannot be the same merchant.
- Both merchants must be approved when a new negotiation is created.
- Revision numbers are strictly increasing and unique per negotiation.
- Previous revisions are immutable.
- Only the active non-expired revision can be accepted.
- Accepted negotiation creates at most one B2BDeal.

## B2B Deal

- Deal is backed by exactly one accepted revision.
- Every deal line corresponds to the accepted revision.
- Inventory for all deal lines reserves atomically or not at all.
- Reservation release is idempotent.
- Completed deal cannot return to normal fulfillment states.

## Review

- Rating ∈ [1,5].
- Review references exactly one supported completed transaction.
- Reviewer participated in transaction.
- Duplicate permitted review is blocked.

## Dispute

- References exactly one transaction context.
- RaisedByUser participated in transaction.
- Resolution actor must be Admin.

## Moderation/Audit

- Merchant approval/rejection is auditable.
- Listing approval/rejection/hiding is auditable.
- Dispute resolution is auditable.
- Private document authorization is never based on knowing the storage object key.
