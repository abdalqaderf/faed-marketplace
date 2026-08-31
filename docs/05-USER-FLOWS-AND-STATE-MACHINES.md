# 05 — User Flows and State Machines

## 1. Merchant onboarding

```text
Create account
  -> Merchant application
  -> Upload verification documents
  -> Submit
  -> PendingReview
      -> Approved
      -> Rejected
      -> Suspended (later admin action)
```

Only `Approved` merchant can submit listings.

---

## 2. Listing lifecycle

```text
Draft
  -> PendingReview
      -> Live
      -> Rejected -> Draft -> PendingReview
Live
  -> Hidden
  -> SoldOut
  -> Archived
  -> material edit -> PendingReview
SoldOut
  -> Live (if stock replenished and listing remains valid)
```

Public requests must never expose Draft/Pending/Rejected/Hidden listings.

---

## 3. B2C checkout

```text
Browse
 -> Listing
 -> Select variants/quantity
 -> Add same-merchant items
 -> Checkout
 -> Server revalidates price + stock + merchant
 -> Reserve stock atomically
 -> Create Order
 -> Confirmation page
```

If any item cannot reserve, the order creation fails atomically.

No partial success.

---

## 4. B2C order states

Primary paths:

```text
Pending
  -> Confirmed
      -> ReadyForPickup -> Completed
      -> OutForDelivery -> Completed
  -> Cancelled
  -> NoShow
```

Dispute path:

```text
Confirmed / ReadyForPickup / OutForDelivery / Completed
  -> Disputed
  -> resolution
```

Exact transition permissions are service-level rules.

`Completed` is terminal for normal fulfillment.

---

## 5. B2B negotiation

```text
Buyer submits Revision 1
 -> Open negotiation
Seller:
 -> Accept
 -> Reject
 -> Counter (Revision 2)

Buyer:
 -> Accept
 -> Reject
 -> Counter (Revision 3)

Active revision expires
 -> Negotiation Expired
```

Previous revisions remain immutable.

---

## 6. B2B acceptance

```text
Accept active revision
 -> server revalidates all line stock
 -> reserve all requested variants atomically
 -> mark Negotiation Accepted
 -> create B2BDeal
```

If one variant fails, no variant is reserved and no deal is created.

---

## 7. B2B deal lifecycle

```text
AwaitingFulfillment
  -> ReadyForPickup -> Delivered -> Completed
  -> Shipped -> Delivered -> Completed
  -> Cancelled
  -> Disputed
```

`ReservationExpiresAtUtc` belongs to the deal, not the negotiation offer.

On deal expiry/cancellation before completion:
- release remaining reserved stock.

On completion:
- reserved stock becomes sold stock.

---

## 8. Review eligibility

```text
Transaction Completed
 + reviewer participated
 + no previous allowed review
 = review allowed
```

Everything else = server-side rejection.

---

## 9. Stock conflict flow

Example: one unit remains.

```text
Request A reads Available = 1
Request B reads Available = 1

A reserves + saves RowVersion -> succeeds
B saves stale RowVersion -> concurrency exception
B transaction rolls back
B receives "Stock changed" message
```

Never use front-end state as concurrency control.

---

## 10. Admin dispute flow

```text
Dispute Open
 -> UnderReview
 -> Resolved
 or
 -> Rejected
```

Admin action must log:
- actor;
- target;
- timestamp;
- outcome;
- note.
