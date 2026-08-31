# 01 — Product Requirements Document

## 1. Product vision

Faed helps merchants recover value from inventory that is still sellable but difficult to sell through the normal full-price channel.

Examples:
- overstock;
- past-season inventory;
- customer returns that remain resellable;
- open-box inventory;
- damaged packaging;
- display items;
- minor cosmetic defects;
- missing non-essential packaging/accessories.

The platform converts this inventory into a structured marketplace asset rather than an unstructured classified ad.

---

## 2. The problem

### Merchant problem

A merchant may currently:
- leave stock in storage and freeze working capital;
- discount inside the primary store and weaken price positioning;
- post manually on social media/classifieds;
- sell too cheaply to a liquidator;
- write the item off.

### Buyer problem

A buyer sees discounted items but may not know:
- why the price is lower;
- whether the seller is a real business;
- whether the item is new, opened, returned, or displayed;
- what defect exists;
- whether the defect was already disclosed;
- which accessories/tags/packaging are included;
- what return policy applies.

### Merchant-buyer problem

Resellers need a structured way to:
- find real merchant inventory;
- request quantities;
- negotiate;
- understand condition;
- reserve stock;
- record fulfillment.

---

## 3. Product value proposition

### For selling merchants
- Recover cash from difficult inventory.
- Sell retail and wholesale from one inventory source.
- Reach consumers and resellers.
- Keep liquidation inventory separate from full-price positioning.
- Reduce manual negotiation.
- Track recovered inventory value.

### For individual buyers
- Access meaningful discounts.
- Understand the exact reason for the discount.
- See real defect/packaging evidence.
- Buy from a verified merchant.
- See policy information before ordering.

### For merchant buyers
- Discover stock for resale.
- Submit structured offers.
- Negotiate through recorded counter-offers.
- Buy from verified merchants.
- Track accepted deals and receipt.

---

## 4. Users

### Individual Buyer
Can:
- register/login;
- browse;
- search/filter;
- view listing disclosure;
- create B2C orders;
- choose eligible fulfillment;
- view order history;
- cancel when policy allows;
- confirm receipt;
- open disputes;
- review seller after completion.

Cannot:
- create listings;
- sell;
- access merchant wholesale tools unless separately verified as a merchant.

### Merchant
Can:
- register as merchant;
- submit business verification;
- manage merchant profile;
- create listings after approval;
- manage variants and stock;
- sell B2C;
- buy/sell B2B;
- negotiate offers;
- manage fulfillment;
- view analytics.

### Admin
Can:
- review merchant verification;
- access private verification files;
- approve/reject merchant applications;
- moderate listings;
- manage reference/catalog data;
- monitor orders/deals;
- resolve disputes;
- manage abusive reviews/accounts;
- view audit history.

---

## 5. Launch scope

### Geography
Amman, Jordan.

### Sector
**Fashion Overstock**

### Launch categories
1. Clothing
2. Shoes
3. Bags & Accessories

Lower-level taxonomy can include Men, Women, Kids, product types, etc., but these are not separate launch sectors.

### Accepted inventory
- New overstock.
- Past-season new inventory.
- New inventory with damaged/missing packaging.
- Opened but not actually used.
- Unworn resellable customer returns.
- Display item.
- Minor cosmetic defect with clear evidence.
- Item missing a non-essential element when clearly disclosed.

### Excluded inventory
- Actually used/worn secondhand inventory.
- Counterfeit goods.
- Unknown/stolen source.
- Sensitive hygiene/intimate items.
- Unsafe items.
- High-risk luxury goods without provenance controls.
- Items requiring a different compliance/operating model.
- Materially damaged items unsuitable for normal use.

---

## 6. Physical condition model

Condition describes the **physical state**, not the commercial reason for discount.

### Grade A — New / Complete
New, unused, complete, with normal packaging/tags where normally expected.

### Grade B — New / Packaging Imperfection
New and unused, but packaging/tag/box is damaged or missing.

### Grade C — Opened or Returned / Unused
Opened, inspected, or customer-returned, but not actually used/worn and still physically sound.

### Grade D — Display / Cosmetic Imperfection
Display item or minor cosmetic imperfection that does not prevent normal use and is clearly disclosed.

No used-product Grade E in the Fashion MVP.

---

## 7. Discount reasons

Separate structured reasons may include:
- `Overstock`
- `PastSeason`
- `CustomerReturn`
- `DisplayItem`
- `PackagingDamage`
- `CosmeticDefect`
- `MissingNonEssentialItem`
- `OtherApprovedReason`

A listing may have more than one reason when valid.

---

## 8. Core listing experience

Every public listing must make the following obvious:

1. seller verification;
2. product identity;
3. physical condition;
4. why it is discounted;
5. visible defects;
6. reference price and Faed price;
7. available variants;
8. available quantity;
9. B2C/B2B availability;
10. fulfillment options;
11. return/warranty information where applicable.

The user should not need to open hidden tabs to discover a material defect.

---

## 9. B2C model

- One merchant per order.
- An order may contain multiple items/variants from that merchant.
- No multi-merchant cart/order in MVP.
- Cash-based fulfillment in MVP:
  - pickup;
  - merchant delivery.
- Stock is reserved atomically when the order is successfully placed.
- Cancelled/expired orders release reserved stock.
- Completed orders convert reserved stock into sold stock.
- Reviews only after `Completed`.

---

## 10. B2B model

B2B has two separate concepts:

### Negotiation
- buyer merchant submits quantity/price proposal;
- seller can accept, reject, or counter;
- each revision is recorded;
- active proposal has its own expiry.

### Deal
Created only after an offer is accepted.

The deal:
- snapshots accepted commercial terms;
- reserves stock;
- has a separate reservation/fulfillment expiry;
- moves through fulfillment statuses;
- can complete, cancel, or enter dispute.

Merchant shipping is arranged between the parties. Faed does not book transport.

---

## 11. Fulfillment

### B2C
- `Pickup`
- `MerchantDelivery`

### B2B
- direct pickup;
- `SellerArrangedShipping`

Future-compatible enum/value:
- `PlatformShipping` — disabled in MVP.

---

## 12. Payments

### B2C MVP
- Cash on pickup.
- Cash on merchant delivery.

### B2B MVP
- Payment arrangement is handled between the parties after acceptance.
- No escrow.
- No marketplace payment splitting.

Real payment integration is deferred.

---

## 13. Trust and disputes

Faed trust is based on more than ratings.

Signals may include:
- verification state;
- completed transactions;
- cancellation rate;
- dispute rate;
- fulfillment reliability;
- verified reviews.

Complaint principle:
- a clearly disclosed imperfection is not automatically an undisclosed-defect complaint;
- a material mismatch or undisclosed defect may be disputed;
- admin reviews the evidence and transaction history.

---

## 14. Merchant analytics

MVP dashboard should show:
- original/reference stock value listed;
- expected discounted value;
- completed recovered sales value;
- units listed/sold;
- sell-through rate;
- average time to sale;
- B2C vs B2B recovered value;
- cancelled orders;
- active B2B negotiations;
- stale listings.

All analytics must derive from transactional/listing data, not merchant-editable totals.

---

## 15. Revenue model

Validation MVP is free.

Do not implement:
- commissions;
- subscriptions;
- promoted listings;
- paid shipping;
- payment fees.

The schema must not make future monetization difficult.

---

## 16. Success definition

The software MVP is successful only if it can support a real validation test with:
- verified merchants;
- real inventory;
- real B2C transactions;
- real B2B negotiations/deals;
- reliable stock accounting;
- clear disclosure;
- measurable recovered value.

The product is not validated merely because the application works technically.
