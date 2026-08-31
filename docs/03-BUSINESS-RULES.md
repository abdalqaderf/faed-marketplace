# 03 — Business Rules

## 1. Merchant verification

A merchant seller must:
1. have an authenticated account;
2. submit a merchant application;
3. provide required business verification document(s);
4. receive admin approval.

States:
- `Draft`
- `PendingReview`
- `Approved`
- `Rejected`
- `Suspended`

A merchant with `Rejected` or `Suspended` status cannot create/publish listings or act as seller.

Verification documents are private.

---

## 2. Listing lifecycle

Suggested states:
- `Draft`
- `PendingReview`
- `Live`
- `Rejected`
- `Hidden`
- `SoldOut`
- `Archived`

Rules:
- only verified merchants may submit;
- public users see only `Live`;
- admin can reject with reason;
- material edits to `Live` create a new review requirement;
- sold-out listing remains historically addressable to authorized users but is not purchasable;
- archived listing is not purchasable.

---

## 3. Condition and discount reason

Never combine them.

Condition answers:
> What physical state is the item in?

Discount reason answers:
> Why is this merchant selling it below its normal channel/price?

The condition suggestion questionnaire can recommend a grade, but the merchant must confirm the answers and admin may override during moderation.

---

## 4. Reference price

A merchant cannot use reference price as an untrusted marketing number without provenance metadata.

Model evidence/source metadata so the platform can capture:
- merchant current price;
- previous store price;
- catalog price;
- product URL;
- invoice/catalog evidence;
- admin note.

MVP may rely on manual review.

Do not hard-code a minimum discount percentage as a domain invariant.

---

## 5. Listing variants / SKU

Stock belongs to `ListingVariant`.

Examples:
- Black / M
- Black / L
- White / M

Each variant has:
- SKU or generated identifier;
- option combination;
- initial quantity;
- available quantity;
- reserved quantity;
- sold quantity;
- active state;
- `RowVersion`.

Invariant:

```text
InitialQuantity + PositiveAdjustments
=
AvailableQuantity + ReservedQuantity + SoldQuantity + NegativeAdjustments
```

If the implementation does not track adjustment totals directly, inventory adjustments must still be auditable.

---

## 6. Inventory adjustments

Merchant may adjust stock with a reason.

Examples:
- discovered extra stock;
- damaged/lost outside Faed;
- manual correction.

Every manual adjustment records:
- who;
- when;
- variant;
- old quantity;
- new quantity/delta;
- reason.

Never silently overwrite stock.

---

## 7. B2C order

An order:
- belongs to one buyer;
- belongs to one selling merchant;
- contains one or more order items;
- each item references a listing variant and stores snapshots.

Snapshots include at least:
- listing title;
- variant description;
- unit price;
- condition grade;
- relevant discount reason(s).

All order items must belong to the same merchant.

### Reservation
When an order is placed:
1. validate listing/variant is live;
2. validate merchant is approved;
3. validate current price server-side;
4. validate requested quantity;
5. atomically move quantity from Available to Reserved;
6. create the order and items in the same transaction.

When cancelled/expired:
- Reserved → Available.

When completed:
- Reserved → Sold.

The reservation policy duration is configurable, not a domain constant.

---

## 8. B2C statuses

Suggested:
- `Pending`
- `Confirmed`
- `ReadyForPickup`
- `OutForDelivery`
- `Completed`
- `Cancelled`
- `NoShow`
- `Disputed`

Allowed transitions must be implemented explicitly.

Do not allow arbitrary status assignment from controller input.

---

## 9. B2B negotiation

A `B2BNegotiation` is not a fulfillment record.

It includes:
- listing;
- seller merchant;
- buyer merchant;
- state;
- current offer revision.

States:
- `Open`
- `Accepted`
- `Rejected`
- `Expired`
- `Cancelled`

Each `B2BOfferRevision` records:
- who proposed it;
- offer version number;
- line quantities by variant;
- proposed unit price or line price structure;
- total;
- message;
- `OfferExpiresAt`;
- created timestamp.

Counter-offer creates a new immutable revision.

Do not overwrite the previous offer.

---

## 10. B2B accepted deal

On acceptance:
1. revalidate all requested variant quantities;
2. reserve all required stock atomically;
3. mark negotiation `Accepted`;
4. create `B2BDeal`;
5. snapshot accepted terms;
6. set `ReservationExpiresAt`.

If any line cannot be reserved, acceptance fails as a whole.

Suggested fulfillment states:
- `AwaitingFulfillment`
- `ReadyForPickup`
- `Shipped`
- `Delivered`
- `Completed`
- `Cancelled`
- `Disputed`

If deal expires/cancels before stock is consumed:
- Reserved → Available.

On completion:
- Reserved → Sold.

---

## 11. B2B MOQ

Initial launch default:
- 10 total units.

Merchant may set a higher MOQ per listing.

MOQ must be configurable and is not a hard-coded permanent platform constant.

For fashion, mixed variants may count toward the listing MOQ if the seller allows mixed-lot purchase.

---

## 12. Fulfillment

### Merchant location
A merchant may have one or more pickup locations.

### Merchant delivery
Merchant defines:
- service zone;
- fee;
- minimum order;
- estimate.

Snapshot selected fulfillment information on the order.

### B2B
Parties may use:
- direct pickup;
- seller-arranged shipping.

Faed can store a seller-entered shipment reference but does not book/price shipping.

---

## 13. Reviews

Review requirements:
- related order/deal exists;
- related transaction is `Completed`;
- reviewer participated in the transaction;
- reviewer has not already submitted the allowed review for that transaction.

Enforce server-side and with a unique database constraint where practical.

---

## 14. Complaints

A complaint must reference exactly one transaction context:
- B2C order; or
- B2B deal.

Evidence may include:
- text;
- images.

Admin resolution is auditable.

A disclosed cosmetic issue alone is not automatically an undisclosed-defect claim.

---

## 15. Analytics

Never store manually editable "recovered value".

Derive from:
- completed orders;
- completed deals;
- listing/reference values;
- quantities;
- timestamps.

Caching/precomputed aggregates may be introduced later if needed.

---

## 16. Authorization ownership rules

A merchant may only:
- edit own listings;
- manage own inventory;
- respond as seller to own negotiations;
- view deals it participates in.

A buyer may only:
- view own private orders;
- act on own eligible orders.

Admin actions require admin role/policy and should be audit logged.
