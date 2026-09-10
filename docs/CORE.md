# Core Scope

What Phase 1 is, who does what, and which entities exist.
Companion documents: `BUSINESS-MODEL.md` (why), `04-DOMAIN-MODEL.md` (entity reference),
`PHASE-PLAN.md` (execution).

---

## 1. The one sentence

> A verified, subscribed merchant publishes an open-box or ex-display item with a disclosed
> condition, discount reason and defect photo; an admin reviews it; a buyer reserves it, then
> inspects and pays cash at pickup — with no overselling when two orders arrive at once.

This is the acceptance test for every feature. **What does not serve it does not enter
Phase 1.**

---

## 2. Roles

| Role | Can | Cannot |
|---|---|---|
| **Buyer** | Browse · search · reserve · track orders · review after collection | Publish · sell |
| **Merchant** | Verify · subscribe · create drafts · publish (two conditions) · manage stock · handle orders · view analytics | Buy · review others' listings |
| **Admin** | Approve verification · activate subscriptions · moderate listings · manage reference data · monitor orders | Buy · sell |

A user holds exactly one role. There is no storefront login separate from the merchant account
that owns it.

---

## 3. Merchant journey

### 3.1 Registration and activation — verify first, pay second

```
Register ──► Upload documents ──► Build drafts (while waiting)
                                          │
                                   Admin approves ✓
                                          │
                        "Your store is approved — choose a plan to publish"
                                          │
                              Pay ──► Activated ──► Publish
```

**The publish gate is two independent conditions:** `verification approved` **and**
`subscription active`. An approved merchant with no subscription is a valid state — the
conditions are not coupled.

**Drafts are available immediately after registration**, before approval and before payment.

### 3.2 Publishing an item — one page, nine fields

| # | Field | Note |
|---|---|---|
| 1 | Photos | Drag and drop · first is the cover |
| 2 | Title | |
| 3 | Category | Three cards — not a dropdown |
| 4 | **What's the condition?** | The four cards below |
| 5 | **Warranty** | Dropdown: None · Manufacturer warranty · Shop warranty (+ optional months) |
| 6 | Price | JOD |
| 7 | Original price | Optional — produces a "save %" badge and requires evidence |
| 8 | Quantity | Defaults to 1 |
| 9 | Description | **Optional.** Free text, last on the form |

**Only eight of the nine ask the merchant to decide anything.** The description is an optional
text box that costs no thought — it is not the kind of field that made the old form confusing.

**Why a description survives the reduction:** the structured fields cannot carry everything a
buyer needs. The condition card says "Ex-display — light scratches" and the defect photo shows
one, but *"the scratch is on the back panel, not visible on a counter"* is what actually closes
the sale — along with the model number, the wattage, and what is in the box. Removing it would
make a Faed listing thinner than a classifieds post, which inverts the entire premise. It is
optional because a sealed item may need nothing beyond its title, and forcing prose invites
"good product" filler.

One button: **Publish**. No draft-then-submit-for-review step — the merchant clicks publish
and sees "Under review".

**Why warranty is a field and not free text:** for a 120 JOD appliance, *"is it under
warranty?"* is the second question every buyer asks after *"what's wrong with it?"*. Leaving
it to the description to save one field is a false economy.

### 3.3 The unified condition question

Instead of asking for a condition grade **and** discount reasons — two overlapping taxonomies
for the same fact — one question with four cards:

| Card | What the merchant sees | Stored as |
|---|---|---|
| 1 | **Sealed** — new, unopened box | Grade A + Overstock |
| 2 | **Box opened or damaged** — item is new and unused | Grade B + PackagingDamage |
| 3 | **Customer return** — opened and checked, never used | Grade C + CustomerReturn |
| 4 | **Ex-display** — light scratches or marks from showroom use | Grade D + DisplayItem |

**One click fills both database fields.** The schema does not change — the UI simply stops
leaking internal vocabulary.

Selecting card 2 or 4 **immediately reveals** an upload box labelled "Photo of the scratch or
box". The defect-photo requirement moves from a late error message to a field that appears
when it is relevant.

An "Add another reason" link exposes the remaining discount reasons for advanced merchants.

### 3.4 Pause and Delete

Two words for the merchant instead of five internal terms:

| Merchant sees | What happens |
|---|---|
| **Pause** | Hidden from the shop, restored in one click — and frees a quota slot |
| **Delete** | Gone from the merchant's view; the record stays in the database |

**Never a physical delete.** An item referenced by a past order must not vanish, or the order
loses its meaning.

### 3.5 Plan quota

| Plan | Monthly | Active listings |
|---|---|---|
| Basic | 35 JOD | 15 |
| Standard | 50 JOD | 40 |
| Pro | 80 JOD | 120 |

The quota counts **listings live at the same time**. At the limit:

> You've reached your plan limit (40 listings). Pause one or upgrade to publish.

**On subscription expiry:** listings are hidden, not deleted, and return on renewal.

**On downgrade:** the most recently published listings up to the new quota stay live; the rest
are paused automatically and the merchant is notified.

---

## 4. Buyer journey

```
Home ──► Browse or search ──► Item page ──► Reserve
                                              │
                             ┌────────────────┴────────────────┐
                             │                                 │
              Merchant confirms within 12 h      No confirmation ──► auto-cancel
                             │                                       + notify buyer
                             ▼
              Address, phone and WhatsApp button revealed
                             │
                             ▼
                 Travel · inspect · pay cash
                             │
                             ▼
                          Review
```

### 4.1 Reserve, not checkout

**"Checkout" is the wrong word and is replaced.** The buyer pays nothing on the site; they
hold an item to go and buy it in cash. The wrong word creates an expectation of entering a
card, so the user hesitates and leaves.

The page becomes a three-line confirmation:

> **Reserve this item**
> Pickup from Amman Appliances — Abdali, Amman. Full address and phone shown once the shop confirms
> Expires in 12 hours if the shop doesn't confirm · Pay cash at the shop
> `[ Reserve ]`

The first line names the shop's **area** only — the street address, pickup hours and phone
number are revealed after the merchant confirms (`BUSINESS-MODEL.md` §8.7). Before that the
buyer knows which part of Amman to plan around, not the door.

### 4.2 Filters — four, plus one sort control

| Kept | Removed | Reason |
|---|---|---|
| Category | Size · Colour | Fashion-sector leftovers |
| Condition | Brand | Dropped from Phase 1 |
| Price range | Channel (B2C/B2B) | Dies with the B2B removal |
| Text search | Discount reason | Merged into the condition question |

The advanced-filter drawer is removed — meaningless for a catalogue of dozens of items.

**Sort is not a filter and stays.** Three options: Newest · Price low–high · Price high–low.
A discount marketplace without "cheapest first" fails its own buyers, and removing sort would
leave the merchant response rate (§4.5) with nothing to act on.

**Default order: newest first, with response rate as the tie-breaker** among listings published
the same day. Explainable in one sentence, and it gives the response rate real weight instead of
making it a decorative number.

### 4.3 The three clocks — and who each one is for

| Window | Runs on | On expiry |
|---|---|---|
| **12 h** | **The merchant, to confirm** | Cancelled · stock released · buyer told the shop did not respond |
| **48 h** | **The buyer, to collect** — starts at confirmation | `NoShow` · stock returns to sale |
| **72 h** | A `ReadyForPickup` order with no movement | `NoShow` · stock returns to sale |

Stock is held from the moment of reservation and stays held through the whole chain; it is
released only on `Cancelled` or `NoShow`.

The 12 h window protects the **buyer** from a silent shop. The 48 h window protects the
**merchant** from a reservation that ties up a one-off item forever.

**Both deadlines must be visible to the person they bind.** The reserve page states the shop's
12 h window before the buyer commits; the confirmed-order page states the buyer's collection
deadline as a date and time, not a duration — *"Collect by Thursday, 6 PM"*. Until Phase 9 the
buyer was never told their own deadline and learned it only from a `NoShow` notice.

### 4.4 Order reference

Six characters from an unambiguous alphabet (`ABCDEFGHJKMNPQRSTUVWXYZ23456789` — no I, L, O,
0 or 1), displayed as `#FA7K2M`. Unique index. It has to be read aloud over the phone and typed
into WhatsApp, so confusable characters are excluded by design.

### 4.5 Reviews

Available only after a completed order, once per order. A merchant's reputation accumulates
only through real transactions on the platform.

---

## 5. Admin journey

| Screen | Action |
|---|---|
| Verification queue | Review merchant documents — approve, or reject with a reason |
| Subscriptions | Activate a month · extend · cancel — with a recorded payment reference |
| Listing queue | Review every listing before it appears — approve, or reject with a note |
| Reference data | Categories · condition grades · discount reasons · plans, prices and quotas |
| Orders | Read-only monitoring |

**Rejection history is preserved.** A listing rejected twice for the same reason is
information the admin needs.

---

## 6. Entities — seventeen

**Identity & merchant** — `ApplicationUser` · `MerchantProfile` ·
`MerchantVerificationDocument` · `MerchantLocation`

**Subscriptions** *(new)* — `SubscriptionPlan` · `MerchantSubscription`

**Catalog** — `Category` · `ConditionGrade` · `DiscountReason`

**Listing** — `Listing` · `ListingDiscountReason` · `ListingMedia` · `ListingVariant` ·
`ListingReferencePriceEvidence` · `ListingModeration`

**Transactions** — `Order` · `OrderItem` · `Review`

**Merchant response rate needs no entity** — it is computed from `Order` statuses and
timestamps.

### 6.1 New and changed fields

| Entity | Change |
|---|---|
| `Listing` | **Add** `WarrantyType` (`None` · `ManufacturerWarranty` · `ShopWarranty`) and `WarrantyMonths` (nullable int). **Remove** `AllowB2C`, `AllowB2B`, `AllowMixedVariantB2B`, `WholesaleIndicativeUnitPrice`, `WholesaleMinQuantity`, `WarrantyText`, `BrandId` |
| `SubscriptionPlan` | `Code` · `Name` · `MonthlyPriceJod` · `ActiveListingQuota` · `HasFeaturedPlacement` · `SortOrder` · `IsActive` — seeded reference data |
| `MerchantSubscription` | `MerchantProfileId` · `SubscriptionPlanId` · `Status` · `StartsAtUtc` · `ExpiresAtUtc` · `PaymentReference` · `ActivatedByAdminId` · `RowVersion` |
| `ReferencePriceEvidenceType` | Reduced from six values to **two**: `Photo` · `Link` |
| `Order` | **Add** `Reference` (6 chars, unique). **Remove** nothing |
| `Dispute`, `Review` | `Review` loses `B2BDealId` and its "exactly one" constraint — `OrderId` becomes required. `Dispute` is removed entirely |

---

## 7. What was removed and why

| Removed | Reason |
|---|---|
| `B2BNegotiation` · `B2BOfferRevision` · `B2BOfferLine` · `B2BDeal` · `B2BDealLine` | ~40% of the complexity with no public surface and no validated demand. Design archived in `B2B-DESIGN.md` |
| `Dispute` · `DisputeEvidence` | Inspection before payment substantially removes the need |
| `AdminActionLog` | An audit trail that does not serve the one sentence |
| `InventoryAdjustment` | Stock auditing, premature |
| `MerchantDeliveryZone` | Delivery is the merchant's responsibility, not the platform's |
| `Brand` | Already documented as optional |
| `ListingOption` · `ListingOptionValue` · `ListingVariantOptionValue` | **Kept in the schema, hidden from the UI** — an item is one physical unit, so a single variant is created automatically |

**The merchant never sees the words "variant" or "SKU".** The quantity field creates the
variant and generates its SKU.

---

## 8. Background services

| Service | Job |
|---|---|
| Reservation expiry | Cancel an unconfirmed order after **12 h** and release stock |
| No-show | Move a confirmed, uncollected order to `NoShow` after **48 h** |
| Auto-close | Close a stale ready-for-pickup order after **72 h** |
| Subscription expiry | Hide a merchant's listings when the period ends |

The pattern already exists (`ReservationExpiryService`) — it is extended, not invented.

---

## 9. Technical rules that stay

| Rule | Why |
|---|---|
| **`rowversion` on stock quantity** | Two buyers, one unit, one moment. Stronger and clearer with quantity 1 than it ever was with clothing |
| **Snapshots in `OrderItem`** | Price, title and condition captured at order time — a past order never changes when a listing is edited |
| **Moderation history preserved** | Every approve/reject is a new row, never an update |
| **No cascade deletes of transactional history** | Archive and deactivate instead of physical deletion |
| **Background services for deadlines** | No request-time checks — the system resolves by itself |
| **Upload validation** | The principle stays — type, size and signature — with a simplified implementation instead of 2,705 lines |

---

## 10. The reduction

| | Before | After |
|---|---|---|
| Entities | 30 | **17** |
| C# lines | ~25,600 | ~19,500 |
| Decisions to publish an item | ~20 | **8** (plus one optional note) |
| Pages to publish an item | 2 | **1** |
| Shop filters | 10 | **4** (sort control unchanged) |
| Lifecycle words shown to merchants | 5 | **2** |
| Steps to a first draft | register + documents + wait | **register, then create** |
| Longest a buyer can wait | unbounded | **12 hours** |

---

## 11. Declared limits of Phase 1

- No payment processing and no escrow — cash between the two parties
- No shipping or warehousing — pickup from the shop; delivery is the merchant's own
- No in-app chat — structured status events plus a WhatsApp link
- No B2B — deferred
- No dispute system — deferred
- No annual plans or term discounts — monthly only
- No individual sellers — verified, subscribed merchants only
- Amman only · English UI · JOD with three decimal places
