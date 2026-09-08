# B2B Module — Archived Design

> **Status: removed from Phase 1.** This document is the design record of the
> merchant-to-merchant (wholesale) module as it existed in the two-sided version of Faed on
> `main`, written from `src/Faed.Web/Services/B2B/` and the five B2B entities **before they
> are deleted in Phase 2**. It exists so the module can be rebuilt a year from now without
> re-deriving it from git history.
>
> Nothing here is part of the open-box / ex-display marketplace. `CLAUDE.md` forbids
> re-introducing B2B features without a fresh decision.

---

## 1. What the module did

Two **verified, approved** merchants could negotiate a bulk purchase of one merchant's
listing and then fulfil it, entirely inside Faed, with **no money touching the platform**.

The flow had two clean halves, deliberately modelled as **two separate aggregates**:

| Half | Aggregate | Responsibility |
|---|---|---|
| **Negotiation** | `B2BNegotiation` + `B2BOfferRevision` + `B2BOfferLine` | Append-only offer / counter-offer history. Consumes **no stock**. |
| **Deal** | `B2BDeal` + `B2BDealLine` | The fulfilment record created on acceptance. **Holds a stock reservation**, runs the hand-over state machine. |

The split is the central design decision. A negotiation is a *conversation about terms*; a
deal is a *commitment against inventory*. They have different lifecycles, different expiry
clocks, different concurrency concerns, and different reasons to exist after they close
(the negotiation is kept as the audit trail of what was agreed; the deal is kept as the
transactional history that reviews and disputes attach to).

### Participants and vocabulary

- **Selling merchant** — owns the listing, receives the first offer, responds to offers.
- **Buying merchant** — opens the negotiation by making the first offer on someone else's
  listing.
- **Offer / revision** — one complete set of proposed terms: a single unit price, one or
  more variant lines with quantities, an optional free-text message, and an expiry.
- **Line** — one `(ListingVariant, quantity)` pair inside a revision or a deal. Price is
  **never per-line**; the whole offer carries one proposed unit price.

### Listing-level wholesale configuration (on the `Listing` entity, also removed)

- `AllowB2B` (bool) — the listing is offered wholesale at all.
- `WholesaleMinQuantity` (int?) — minimum order quantity (MOQ), required and positive when
  `AllowB2B`.
- `AllowMixedVariantB2B` (bool) — whether quantities across variants count **together**
  toward the MOQ (mixed lot) or each variant must reach the MOQ **on its own**.
- `WholesaleIndicativeUnitPrice` (decimal?) — a non-binding starting price shown on the
  "make an offer" form.

---

## 2. Entities

All five are plain EF Core classes with **private setters, a private parameterless
constructor for EF, and encapsulated collections** (`_revisions`, `_lines` exposed as
`IReadOnlyCollection`). Business rules live in the entities; the services orchestrate,
translate `Result`s and own transactions. IDs are `Guid` v7, assigned in the constructor
(`ValueGeneratedNever`).

### 2.1 `B2BNegotiation` (aggregate root)

| Field | Type | Notes |
|---|---|---|
| `Id` | Guid | v7 |
| `ListingId` | Guid | FK → `Listing`, `Restrict` |
| `SellingMerchantProfileId` | Guid | FK → `MerchantProfile`, `Restrict` |
| `BuyingMerchantProfileId` | Guid | FK → `MerchantProfile`, `Restrict` |
| `Status` | `B2BNegotiationStatus` | stored as string, max 32 |
| `CurrentRevisionNumber` | int | 0 only transiently during construction, then ≥ 1 |
| `CreatedAtUtc` / `UpdatedAtUtc` | DateTime | UTC |
| `RowVersion` | byte[] | `IsRowVersion()` — concurrency token |
| `_revisions` | `List<B2BOfferRevision>` | append-only, exposed as `Revisions` |

Computed members (all `[NotMapped]` / `builder.Ignore`): `CurrentRevision` (highest
revision number), `IsOpen`, `IsParticipant(id)`, `CounterpartyOf(id)`,
`AwaitingResponseFrom` (the participant who did **not** propose the current revision, or
`null` when closed), `CanBeRespondedToBy(id)`, `CurrentOfferHasExpired(now)`.

Indexes: `(SellingMerchantProfileId, Status)`, `(BuyingMerchantProfileId, Status)`,
`(ListingId)`.

### 2.2 `B2BOfferRevision` (owned by the negotiation, immutable)

| Field | Type | Notes |
|---|---|---|
| `Id` | Guid | v7 |
| `B2BNegotiationId` | Guid | FK → `B2BNegotiation`, **`Cascade`** |
| `RevisionNumber` | int | strictly increasing from 1, **unique per negotiation** (DB unique index `(B2BNegotiationId, RevisionNumber)`) |
| `ProposedByMerchantProfileId` | Guid | FK → `MerchantProfile`, `Restrict` |
| `ProposedUnitPrice` | decimal(18,3) | JOD, > 0, ≤ 3 dp |
| `ProposedTotal` | decimal(18,3) | **server-derived**: `ProposedUnitPrice × TotalQuantity` |
| `Message` | string? | trimmed, max 2000, null when blank |
| `OfferExpiresAtUtc` | DateTime | must be in the future at creation |
| `CreatedAtUtc` | DateTime | |
| `_lines` | `List<B2BOfferLine>` | exposed as `Lines` |

Check constraint `CK_B2BOfferRevisions_NonNegativeMoney`:
`ProposedUnitPrice >= 0 AND ProposedTotal >= 0`.

The constructor is `internal` — **only `B2BNegotiation` can create a revision.** There are
no mutators of any kind. `TotalQuantity` is computed (`Ignore`d).

### 2.3 `B2BOfferLine` (owned by the revision, immutable)

`Id`, `B2BOfferRevisionId` (FK → `B2BOfferRevision`, `Cascade`),
`ListingVariantId` (FK → `ListingVariant`, `Restrict`), `Quantity` (int, > 0).
Unique index `(B2BOfferRevisionId, ListingVariantId)` — one line per variant per revision.
Check constraint `CK_B2BOfferLines_PositiveQuantity`: `Quantity > 0`. `internal`
constructor.

### 2.4 `B2BDeal` (aggregate root)

| Field | Type | Notes |
|---|---|---|
| `Id` | Guid | v7 |
| `B2BNegotiationId` | Guid | FK → `B2BNegotiation`, `Restrict`; **DB unique index** — an accepted negotiation creates **at most one** deal |
| `AcceptedRevisionId` | Guid | FK → `B2BOfferRevision`, `Restrict` — the exact revision both sides agreed on |
| `SellingMerchantProfileId` / `BuyingMerchantProfileId` | Guid | FK → `MerchantProfile`, `Restrict` |
| `Status` | `B2BDealStatus` | string, max 32 |
| `FulfillmentType` | `B2BFulfillmentType` | string, max 32 |
| `ShipmentReference` | string? | seller-entered, trimmed, max 200 |
| `AcceptedUnitPriceSnapshot` | decimal(18,3) | copied from the accepted revision |
| `ShippingCostSnapshot` | decimal(18,3)? | always `null` in the MVP (see §6) |
| `SubtotalSnapshot` | decimal(18,3) | the accepted revision's `ProposedTotal` |
| `TotalSnapshot` | decimal(18,3) | `Subtotal + (ShippingCost ?? 0)` — computed in ctor, never supplied |
| `ReservationExpiresAtUtc` | DateTime? | when the stock hold lapses while still `AwaitingFulfillment`; **cleared (`null`) once fulfilment starts or the deal ends** |
| `StatusReason` | string? | max 500 — set on cancel |
| `CreatedAtUtc` / `UpdatedAtUtc` / `CompletedAtUtc?` / `CancelledAtUtc?` | DateTime | |
| `RowVersion` | byte[] | `IsRowVersion()` |
| `_lines` | `List<B2BDealLine>` | exposed as `Lines` |

Computed / ignored: `TotalUnits`, `HoldsReservation` (true for `AwaitingFulfillment`,
`ReadyForPickup`, `Shipped`, `Delivered`), `IsTerminal`, `IsParticipant(id)`.

Indexes: unique `(B2BNegotiationId)`; `(SellingMerchantProfileId, Status)`;
`(BuyingMerchantProfileId, Status)`; `(Status, ReservationExpiresAtUtc)` — drives the
expiry sweep. Check constraint `CK_B2BDeals_NonNegativeMoney` on all four money columns.

**Fulfilment-data invariant (constructor):** a `Pickup` deal must carry no
`ShipmentReference` and no `ShippingCostSnapshot` — a pickup with shipping data is
contradictory and throws `DomainException`.

### 2.5 `B2BDealLine` (owned by the deal, immutable)

`Id`, `B2BDealId` (FK → `B2BDeal`, `Cascade`), `ListingVariantId` (FK → `ListingVariant`,
`Restrict`), `Quantity` (> 0), `UnitPriceSnapshot` (decimal(18,3), ≥ 0),
`LineTotalSnapshot` (= `UnitPriceSnapshot × Quantity`, computed), `VariantSnapshot`
(string, required, max 400 — a human-readable `"Colour: Black · Size: M"` rendered at
acceptance time so the deal never reads variant text back from the mutable listing).
Unique index `(B2BDealId, ListingVariantId)`. Check constraint
`CK_B2BDealLines_PositiveQuantityAndMoney`. `internal` constructor; lines can only be added
while `Status == AwaitingFulfillment` (i.e. during creation).

### 2.6 Value objects (in `B2BNegotiation.cs`)

- `ProposedOfferLine(Guid ListingVariantId, int Quantity)`
- `ProposedOffer(decimal UnitPrice, IReadOnlyList<ProposedOfferLine> Lines, string? Message, DateTime OfferExpiresAtUtc)`

The service builds a `ProposedOffer` from validated request input **plus a server-resolved
expiry timestamp**, and hands it to the aggregate. No monetary value or timestamp is
trusted straight from the request.

---

## 3. The negotiation state machine

### States (`B2BNegotiationStatus`)

| State | Meaning | Terminal? |
|---|---|---|
| `Open` | An offer is on the table; the other merchant can accept / reject / counter. | no |
| `Accepted` | Both sides agreed on the current revision. The deal + stock reservation are created in the **same transaction** as this transition. | yes |
| `Rejected` | The addressed merchant turned the current offer down. | yes |
| `Expired` | The current revision's `OfferExpiresAtUtc` passed while `Open`. | yes |
| `Cancelled` | A participant withdrew before acceptance. | yes |

### Transitions

```
                         ┌──────────── Counter (new revision, turn flips) ───────────┐
                         ▼                                                           │
   (create) ─────────►  Open  ──────────────────────────────────────────────────────┘
                         │
                         │  Accept   (responder only, offer active)   ─────────►  Accepted   [+ B2BDeal created, stock reserved]
                         │  Reject   (responder only, offer active)   ─────────►  Rejected
                         │  Cancel   (either participant, offer active) ───────►  Cancelled
                         │  ExpireIfLapsed / RequireCurrentOfferActive ────────►  Expired
```

| Command | Method | Who may call | Preconditions | Effect |
|---|---|---|---|---|
| **Open** | `new B2BNegotiation(...)` | buying merchant (via `StartNegotiationAsync`) | selling ≠ buying; listing `Live`, `AllowB2B`, seller approved; offer valid | creates the negotiation with **revision 1**, proposed by the buying merchant, `Status = Open` |
| **Counter** | `Counter(proposer, moq, mixed, offer, now)` | the participant whose **turn** it is (`AwaitingResponseFrom`) | `Open`; caller is the responder; current offer **not expired**; new offer valid | appends **revision N+1**; `CurrentRevisionNumber` advances; turn flips to the other merchant; still `Open` |
| **Accept** | `Accept(acceptor, now)` | the responder only | `Open`; caller is responder; current offer not expired | `Status = Accepted`. Driven from `B2BDealService.AcceptOfferAsync` inside the acceptance transaction — see §5. |
| **Reject** | `Reject(rejector, now)` | the responder only | `Open`; caller is responder; current offer not expired | `Status = Rejected` |
| **Cancel** | `Cancel(canceller, now)` | **either** participant | `Open`; current offer not expired | `Status = Cancelled` |
| **Expire** | `ExpireIfLapsed(now)` | system (sweep) or lazily on any participant action | `Open` **and** `CurrentOfferHasExpired(now)` | `Status = Expired`; returns `false` and changes nothing otherwise (idempotent) |

### Turn alternation

The sides **strictly alternate**. `AwaitingResponseFrom` is derived, not stored:
`CounterpartyOf(CurrentRevision.ProposedByMerchantProfileId)`. A merchant cannot counter or
accept its own offer; `RequireResponder` throws `DomainException`
("It is the other merchant's turn…") otherwise. Revision 1 is always the buying merchant's,
so the seller always moves next.

### Lazy expiry on every action

Every participant command calls `RequireCurrentOfferActive(now)` first. If the offer has
lapsed, that method **sets `Status = Expired`, touches `UpdatedAtUtc`, and throws** — so a
stale offer can never be accepted/countered even if the background sweep has not run yet.
`B2BNegotiationService` additionally calls `ExpireBeforeParticipantActionAsync` which
persists the expiry and returns a `Conflict` result with a friendly message.

### Guard rails in the service layer (`B2BNegotiationService`)

- `RequireEligibleMerchantAsync` — caller must be an **approved, non-admin** merchant; admins
  are explicitly `Forbidden`.
- Participation is re-checked from the database on **every** call (`IsParticipant`). A
  guessed negotiation id returns `NotFound` — a non-participant "learns nothing".
- `GuardOfferListingAsync` (for new offers / counters): listing exists, not the caller's
  own, `Status == Live`, `AllowB2B`, and the **seller is still `Approved`**.
- Offer building (`BuildProposedOffer`) enforces: ≥ 1 line, ≤ `MaxOfferLines` (50), no
  duplicate variant, each quantity 1..`MaxOfferLineQuantity` (100 000), every variant is an
  **active** variant of the listing, unit price > 0 with ≤ 3 dp, and clamps the chosen
  validity into `[MinOfferValidity, MaxOfferValidity]` = `[1 h, 30 d]`, defaulting to
  `DefaultOfferValidity` = 3 d.
- MOQ is enforced in the **entity** (`ValidateOffer`): with `AllowMixedVariantB2B`, the
  summed quantity must reach `WholesaleMinQuantity`; without it, **each line** must reach it.

### Persisting a transition

`SaveTransitionAsync` catches `DbUpdateConcurrencyException` → `Conflict`
("This negotiation changed a moment ago. Reload it…"). The `RowVersion` token makes a buyer
and a seller acting simultaneously safe: one wins, the other re-reads.

---

## 4. Why offer revisions are immutable

`B2BOfferRevision` and `B2BOfferLine` have **no mutators, `internal` constructors, and
private setters**. A counter-offer is a brand-new row with `RevisionNumber = N+1`, never an
edit of revision `N`. Reasons:

1. **The negotiation *is* its history.** Both merchants — and later an admin, a review, or a
   dispute — need to see exactly what was proposed at each step and by whom. An editable
   "current offer" would destroy that record. This mirrors invariant 4 in `CLAUDE.md`
   (moderation history is append-only) and the "order snapshots" invariant (2): agreed
   terms are captured, not recomputed.
2. **No race on "what are we agreeing to".** Acceptance records `AcceptedRevisionId`. Because
   that revision can never change, the accepting merchant and the deal that results are
   provably bound to the terms that were on screen. If revisions were mutable, a
   counterparty could alter the price between the moment the other side reads it and the
   moment they click Accept.
3. **The totals are derived, once.** `ProposedTotal` is computed in the constructor from
   `ProposedUnitPrice × TotalQuantity` and frozen. Nothing downstream needs to trust or
   re-derive it, and it cannot drift.
4. **Expiry is per-revision.** Each revision has its own `OfferExpiresAtUtc`. "The current
   offer" is just "the highest-numbered revision"; expiry, acceptance and counter logic all
   reduce to reading one immutable row.
5. **Cheap, ordered storage.** Revisions are only ever appended, so their natural order is
   `RevisionNumber` order and no reordering or soft-delete machinery is needed. The DB
   enforces uniqueness and monotonicity with one unique index.

The database backs the invariant up: `(B2BNegotiationId, RevisionNumber)` is unique, so even
a buggy service cannot write two revision 3s.

---

## 5. How stock is held on acceptance

**Acceptance is the only place the negotiation half touches inventory, and it does so
atomically.** It lives in `B2BDealService.AcceptOfferAsync`, not in the negotiation service,
precisely because it must reserve stock and create the deal in one transaction.

### The acceptance transaction (`AcceptOfferAsync`)

1. Resolve the caller as an approved non-admin merchant (`RequireEligibleMerchantAsync`).
2. Validate `FulfillmentType` is a defined enum value.
3. `db.BeginTransactionAsync(...)`.
4. Load the negotiation **tracked**, with `Revisions.Lines`. 404 if not a participant.
5. `CounterpartyIneligibleAsync` — **both** merchants must still be `Approved` and neither
   may have become an admin since the negotiation opened. Creating a deal is a fresh trade
   commitment, so eligibility is re-checked for both sides, not just the caller.
6. `negotiation.ExpireIfLapsed(now)` — if the offer lapsed, persist `Expired`, commit, and
   return `Conflict` ("This offer has expired…"). No stock touched.
7. Load the `Listing` **tracked** with `Options.Values` and `Variants.OptionValues`
   (split query).
8. `negotiation.Accept(caller, now)` — moves the negotiation to `Accepted` **first**. If any
   later step throws, the whole transaction rolls back and the negotiation is still `Open`
   in the database.
9. Construct the `B2BDeal` from the **accepted revision's** snapshots:
   `AcceptedUnitPriceSnapshot = revision.ProposedUnitPrice`,
   `SubtotalSnapshot = revision.ProposedTotal`, `ShippingCostSnapshot = null`,
   `ReservationExpiresAtUtc = now + B2BDealOptions.ReservationWindow` (default **7 days**).
10. For each revision line:
    - `deal.AddLine(variantId, qty, revision.ProposedUnitPrice, describeVariant(variant))`
      — captures the human-readable variant text now.
    - `variant.Reserve(qty, now)` — moves `qty` from `AvailableQuantity` to
      `ReservedQuantity` on the `ListingVariant`. Throws `DomainException` if the variant is
      inactive or `qty > AvailableQuantity`.
11. `listing.RefreshAvailability(now)` — flips the listing to `SoldOut` if nothing is
    sellable any more.
12. `listing.RegisterStockReservation(now)` — **forces the listing row into the write set**
    (advances its `RowVersion`) even if the status did not change. This makes a B2B
    acceptance and a competing B2C order (or another acceptance) that deplete *different
    variants of the same listing* serialize on the listing row: the loser gets a
    concurrency conflict and re-reads.
13. `db.SaveChangesAsync()` + `transaction.CommitAsync()`.
    - `DbUpdateConcurrencyException` → `Conflict` with `StockConflictMessage`
      ("Some of the offered stock is no longer available… Reload the negotiation and try
      again."). This is the `rowversion` protection on `ListingVariant` (invariant 1 in
      `CLAUDE.md`) doing its job: a stale token or insufficient stock rolls the **entire**
      acceptance back — no partial reservation, negotiation stays `Open`.

Result: the new `B2BDeal.Id`. The negotiation is now `Accepted` and permanently linked to
exactly one deal (`B2BDeal.B2BNegotiationId` unique index).

### The three stock movements

`ListingVariant` exposes exactly three transitions, each used once per unit over its
lifecycle, each protected by the variant `RowVersion` and always run inside a service
transaction:

| Method | Movement | Used by the deal on… |
|---|---|---|
| `Reserve(qty)` | `Available → Reserved` | acceptance (§5 step 10) |
| `ReleaseReservation(qty)` | `Reserved → Available` | `Cancel`, and the expiry sweep |
| `ConfirmSale(qty)` | `Reserved → Sold` | `Complete` |

Reserved stock never returns to available once sold. Each reservation is released **exactly
once** — the deal state machine guarantees exactly one of `ConfirmSale` / `ReleaseReservation`
runs per line.

### How long the hold lasts

- While `AwaitingFulfillment`: the hold is time-boxed by `ReservationExpiresAtUtc`
  (`now + ReservationWindow`, default 7 days — longer than a B2C reservation because
  arranging wholesale pickup or a carrier takes longer).
- The moment the **seller starts fulfilment** (`MarkReadyForPickup` / `MarkShipped`),
  `ReservationExpiresAtUtc` is set to `null`: the stock is now held unconditionally until
  the deal is delivered-and-completed or cancelled.
- `RequireReservationNotExpired(now)` guards `MarkReadyForPickup` and `MarkShipped`: a
  fulfilment step must not advance a deal whose reservation has already lapsed (that would
  clear the deadline and hold stock forever on the strength of a passed deadline). The deal
  stays `AwaitingFulfillment` so the sweep releases it.

---

## 6. The deal (fulfilment) state machine

### States (`B2BDealStatus`)

| State | Holds reserved stock? | Meaning |
|---|---|---|
| `AwaitingFulfillment` | yes (time-boxed) | Stock reserved; seller has not started. |
| `ReadyForPickup` | yes (unbounded) | `Pickup` deal the seller has prepared for collection. |
| `Shipped` | yes (unbounded) | `SellerArrangedShipping` deal the seller has dispatched. |
| `Delivered` | yes (unbounded) | Buyer has taken delivery; awaiting final confirmation. |
| `Completed` | no — became **Sold** | Fulfilment finished. Terminal. |
| `Cancelled` | no — **Released** | Withdrawn by a participant or the sweep. Terminal. |

There is deliberately **no `Disputed` status** — a dispute was a separate `Dispute`
aggregate with its own lifecycle that never mutated the deal or its stock.

### `B2BFulfillmentType`

- `Pickup` — buyer collects from the seller. No shipment reference, no shipping cost.
- `SellerArrangedShipping` — seller arranges a carrier and may record a **reference string**.
  Faed never books or prices shipping; `ShippingCostSnapshot` was modelled but always left
  `null` in the MVP.

### Transitions

```
 AwaitingFulfillment ──MarkReadyForPickup (seller, Pickup, not expired)──► ReadyForPickup ─┐
        │                                                                                  │
        ├──────────MarkShipped (seller, SellerArrangedShipping, not expired)──► Shipped ────┤
        │                                                                                  │
        │                                                            MarkDelivered (either)│
        │                                                                                  ▼
        │                                                                              Delivered
        │                                                                                  │
        │                                                                   Complete (either)
        │                                                                                  ▼
        │                                                                             Completed  [Reserved → Sold]
        │
        └── Cancel (either participant; not Delivered/terminal) ──► Cancelled  [Reserved → Available]
                 ▲
                 └── reservation-expiry sweep, while still AwaitingFulfillment
```

| Command | Method | Caller | From → To | Stock effect (same txn) |
|---|---|---|---|---|
| Mark ready for pickup | `MarkReadyForPickup(now)` | **seller only** | `AwaitingFulfillment` → `ReadyForPickup` (must be `Pickup`, reservation not expired) | none; clears `ReservationExpiresAtUtc` |
| Mark shipped | `MarkShipped(ref?, now)` | **seller only** | `AwaitingFulfillment` → `Shipped` (must be `SellerArrangedShipping`, not expired) | none; clears `ReservationExpiresAtUtc`; stores `ref` if given |
| Set shipment reference | `SetShipmentReference(ref, now)` | seller only | any non-terminal `SellerArrangedShipping` | none |
| Mark delivered | `MarkDelivered(now)` | **either** participant | `ReadyForPickup` \| `Shipped` → `Delivered` | none |
| Complete | `Complete(now)` | **either** participant | `Delivered` → `Completed` | `ConfirmSale` on every line (`Reserved → Sold`); sets `CompletedAtUtc` |
| Cancel | `Cancel(reason, now)` | **either** participant | `AwaitingFulfillment` \| `ReadyForPickup` \| `Shipped` → `Cancelled` (**not** `Delivered`, not terminal) | `ReleaseReservation` on every line (`Reserved → Available`); sets `StatusReason`, `CancelledAtUtc` |

A `Delivered` deal **cannot be cancelled** — "It can only be completed now." This prevents
releasing stock the buyer physically holds.

### Service orchestration (`B2BDealService`)

- `SellerTransitionAsync` — resolves caller, loads deal with `Lines`, 404 if not a
  participant, `Forbidden` if not the selling merchant, then `ApplyTransitionAsync`.
- `ParticipantTransitionAsync` — same without the seller check.
- `ApplyTransitionAsync(deal, transition, StockEffect, ct)`:
  1. Run the entity transition (catches `DomainException` → `Conflict`).
  2. If `StockEffect != None`: group lines by variant, load the affected `Listing`s with
     `Variants`, apply `ReleaseReservation` or `ConfirmSale` per variant, then
     `RefreshAvailability` and — for a **release** — `RegisterStockRelease` (same listing-row
     serialization argument as acceptance, so a release and a competing B2C reservation on
     different variants don't both commit against a stale view).
  3. `BeginTransactionAsync` → `SaveChangesAsync` → `CommitAsync`.
     `DbUpdateConcurrencyException` → `Conflict` ("This deal changed a moment ago…").
- `StockEffect` enum: `None`, `Release`, `ConfirmSale`. `Complete` → `ConfirmSale`;
  `Cancel` → `Release`; every other transition → `None`.

---

## 7. The two expiry services

Both are `BackgroundService`s registered as hosted services **only outside the `Testing`
environment** (tests drive expiry deterministically through the service with a fake clock).
Both use a `PeriodicTimer`, each sweep runs in its **own DI scope**, a failing sweep is
logged and never crashes the host, and each underlying operation is **idempotent** — a
double-run or a run that races a merchant action cannot mis-close or double-release.

This implements invariant 6 in `CLAUDE.md`: *nothing waits on a human forever.*

### 7.1 `B2BOfferExpiryService` → `ExpireLapsedNegotiationsAsync`

- **Cadence:** `B2BNegotiationOptions.ExpirySweepInterval`, default **15 minutes**.
- **What it closes:** open negotiations whose current revision's `OfferExpiresAtUtc` has
  passed.
- **Sweep logic:**
  1. Read the ids of all `Open` negotiations (`AsNoTracking`).
  2. For each: clear the change tracker (`DetachTrackedGraph` — one negotiation's graph must
     not bleed into the next save), re-load it tracked **filtered on `Status == Open`**,
     call `negotiation.ExpireIfLapsed(now)`.
  3. If it returned `true`, `SaveChangesAsync`. `DbUpdateConcurrencyException` is swallowed
     with an info log — "a participant accepted/rejected/countered it in the same moment;
     nothing to do."
- Returns the count expired. **No stock is involved** — a negotiation holds none.

### 7.2 `B2BDealExpiryService` → `ReleaseExpiredDealReservationsAsync`

- **Cadence:** `B2BDealOptions.ExpirySweepInterval`, default **15 minutes**.
- **What it releases:** deals still `AwaitingFulfillment` whose `ReservationExpiresAtUtc`
  is non-null and in the past — i.e. the seller never started fulfilment within the window.
- **Sweep logic:**
  1. Read due deal ids: `Status == AwaitingFulfillment && ReservationExpiresAtUtc != null
     && ReservationExpiresAtUtc < now` (backed by the `(Status, ReservationExpiresAtUtc)`
     index).
  2. For each: clear the tracker, re-load tracked with `Lines`, filtered on
     `Status == AwaitingFulfillment`. Skip if fulfilled/cancelled since the id list was
     taken, or if the deadline is no longer in the past.
  3. `ApplyTransitionAsync(deal, d => d.Cancel("The reservation expired before the deal was
     fulfilled.", now), StockEffect.Release, ct)` — **cancels the deal and releases every
     line's reserved stock back to available in one transaction.**
  4. Outcome handling: success → count + info log; `Conflict` → "superseded" info log
     (a participant acted in the same moment); anything else → warning log.
- Returns the count released.

Once fulfilment has started (`ReservationExpiresAtUtc == null`), this sweep ignores the deal
entirely — the stock is then held until a human (or `Complete`/`Cancel`) resolves it. There
is **no** automatic outcome for a deal stuck in `ReadyForPickup` / `Shipped` / `Delivered`;
that was a known gap the trust phase was to address.

---

## 8. Concurrency model summary

| Row | Token | Protects against |
|---|---|---|
| `B2BNegotiation.RowVersion` | `IsRowVersion` | buyer and seller acting on the same negotiation simultaneously |
| `B2BDeal.RowVersion` | `IsRowVersion` | the two merchants acting on the same deal simultaneously |
| `ListingVariant.RowVersion` | `[Timestamp]` (invariant 1) | two reservations overselling the same unit — B2B vs B2B, **and B2B vs B2C** |
| `Listing.RowVersion` | forced-touch via `RegisterStockReservation` / `RegisterStockRelease` | two transactions depleting/releasing *different variants of one listing* both committing against a stale availability view (wrong `SoldOut` / `Live`) |

The acceptance and every stock-moving deal transition wrap `SaveChanges` in an explicit
`BeginTransactionAsync` so the status change and the stock movement commit together or not
at all.

---

## 9. Configuration

`B2BNegotiationOptions` (section `"B2BNegotiation"`):

| Key | Default | Purpose |
|---|---|---|
| `DefaultOfferValidity` | 3 d | offer validity when the proposer doesn't choose |
| `MinOfferValidity` | 1 h | floor |
| `MaxOfferValidity` | 30 d | ceiling |
| `ExpirySweepInterval` | 15 min | offer-expiry sweep cadence |
| `MaxOfferLineQuantity` | 100 000 | per-line quantity cap |
| `MaxOfferLines` | 50 | distinct variant lines per offer |

`B2BDealOptions` (section `"B2BDeal"`):

| Key | Default | Purpose |
|---|---|---|
| `ReservationWindow` | 7 d | how long an accepted deal holds stock before the seller must start fulfilment |
| `ExpirySweepInterval` | 15 min | deal-reservation-expiry sweep cadence |

All durations are configuration, never domain constants.

---

## 10. Service API surface

### `IB2BNegotiationService`

- `GetListingForOfferAsync(userId, listingSlug)` → `Result<OfferListingView>`
- `StartNegotiationAsync(userId, StartNegotiationInput)` → `Result<Guid>` (negotiation id)
- `CounterOfferAsync(userId, negotiationId, CounterOfferInput)` → `Result`
- `RejectAsync(userId, negotiationId)` → `Result`
- `CancelAsync(userId, negotiationId)` → `Result`
- `GetMyNegotiationsAsync(userId, B2BNegotiationFilter, page)` → `PagedResult<B2BNegotiationSummaryView>`
- `GetAwaitingResponseCountAsync(userId)` → `int` (queue badge)
- `GetNegotiationAsync(userId, negotiationId)` → `B2BNegotiationDetailView?`
- `ExpireLapsedNegotiationsAsync()` → `int` (sweep entry point)

Acceptance is **not** here — it is `IB2BDealService.AcceptOfferAsync`, because it must
reserve stock and create the deal atomically.

### `IB2BDealService`

- `AcceptOfferAsync(userId, negotiationId, AcceptOfferInput{FulfillmentType})` → `Result<Guid>` (deal id)
- `GetMyDealsAsync(userId, B2BDealFilter, page)` → `PagedResult<B2BDealSummaryView>`
- `GetActionableDealCountAsync(userId)` → `int`
- `GetDealAsync(userId, dealId)` → `B2BDealDetailView?`
- `MarkReadyForPickupAsync` / `MarkShippedAsync(…, shipmentReference?)` / `SetShipmentReferenceAsync` / `MarkDeliveredAsync` / `CompleteAsync` / `CancelAsync(…, reason)` → `Result`
- `ReleaseExpiredDealReservationsAsync()` → `int` (sweep entry point)

Filters: `B2BNegotiationFilter` = `AwaitingMe | AwaitingThem | Open | Closed | All`;
`B2BDealFilter` = `Active | AwaitingFulfillment | InFulfillment | Completed | Cancelled | All`.
Party enum: `B2BNegotiationParty` = `SellingMerchant | BuyingMerchant`.

---

## 11. HTTP surface, authorization, UI

Both controllers are in `Areas/Merchant/Controllers/`, `[Authorize(Policy =
FaedPolicies.CanNegotiateB2B)]`. The **`CanNegotiateB2B`** policy = authenticated **AND not
an admin** AND `ApprovedMerchantRequirement`. Admins may monitor B2B activity elsewhere but
cannot participate.

**`OffersController`** (`/Merchant/Offers`): `Index` (queue, default filter `AwaitingMe`),
`Details/{id}`, `GET/POST Create?listingSlug=`, `POST Counter/{id}`, `POST Accept/{id}`
(takes `B2BFulfillmentType`, delegates to `deals.AcceptOfferAsync`, redirects to the new
deal), `POST Reject/{id}`, `POST Cancel/{id}`. The public listing details page links to
`Create` when the listing is `AllowB2B`.

**`DealsController`** (`/Merchant/Deals`): `Index` (default filter `Active`),
`Details/{id}` (also shows review eligibility + dispute state), `POST Review/{id}`,
`POST ReadyForPickup` / `Shipped` / `ShipmentReference` / `Delivered` / `Complete` /
`Cancel`.

View models live in `Areas/Merchant/ViewModels/{B2BOfferModels,B2BDealModels}.cs`; views in
`Areas/Merchant/Views/{Offers,Deals}/`. Admin read-only views in
`Areas/Admin/Views/Transactions/{Deals,DealDetails}.cshtml`. Display helpers
`Rendering/{B2BDealStatusDisplay,B2BNegotiationStatusDisplay}.cs`. Subnav links in
`_MerchantSubnav.cshtml` / `_AdminSubnav.cshtml`.

DI registration is in `DependencyInjection.cs`: options binding for both sections, scoped
`IB2BNegotiationService` / `IB2BDealService`, and — outside `Testing` — the two hosted
expiry services.

---

## 12. Cross-entity couplings to restore on a rebuild

- `Listing`: `AllowB2B`, `AllowB2C`, `AllowMixedVariantB2B`, `WholesaleIndicativeUnitPrice`,
  `WholesaleMinQuantity`, and the submission blockers for B2C/B2B channels
  (`DescribeSubmissionBlockers`).
- `Review` referenced a deal via `B2BDealId` (nullable) with an "exactly one of
  `OrderId` / `B2BDealId`" constraint; `TrustTransactionType.B2BDeal` fed review and
  dispute eligibility.
- `Dispute` referenced `B2BDealId`.
- Enums to recreate: `B2BNegotiationStatus`, `B2BDealStatus`, `B2BFulfillmentType`,
  and `TrustTransactionType.B2BDeal`.
- `AdminOperationsService`, `MerchantAnalyticsService`, `ReviewService`, `DisputeService`
  and `DemoDataSeeder` all had B2B branches.

---

## 13. Rebuild checklist

1. Recreate the two enums (`B2BNegotiationStatus`, `B2BDealStatus`) and `B2BFulfillmentType`.
2. Recreate `B2BNegotiation` (root) + immutable `B2BOfferRevision` / `B2BOfferLine`; keep
   the `internal` constructors and the append-only `_revisions`.
3. Recreate `B2BDeal` (root) + immutable `B2BDealLine` with the fulfilment-data invariant.
4. EF configs: string enums, `decimal(18,3)`, `IsRowVersion` on both roots, the unique
   indexes (`(negotiation, revisionNumber)`, `(revision, variant)`, `B2BDeal.negotiationId`,
   `(deal, variant)`), the `(Status, ReservationExpiresAtUtc)` index, all `Restrict` FKs
   except the owned-collection cascades, and the check constraints.
5. `ApplicationDbContext` / `IApplicationDbContext` DbSets.
6. `IB2BNegotiationService` + impl — offer building, guards, turn alternation, lazy expiry.
7. `IB2BDealService` + impl — the atomic `AcceptOfferAsync` transaction, the fulfilment
   transitions with `StockEffect`, `ReleaseExpiredDealReservationsAsync`.
8. `B2BOfferExpiryService`, `B2BDealExpiryService` — hosted, scoped-per-sweep, idempotent.
9. `B2BNegotiationOptions`, `B2BDealOptions` + config sections + DI wiring.
10. `CanNegotiateB2B` policy, the two controllers, view models, views, rendering helpers,
    subnav links.
11. Re-add the `Listing` wholesale fields, `Review` / `Dispute` deal links and
    `TrustTransactionType.B2BDeal`.
12. Tests: negotiation state machine + turn alternation; revision immutability / uniqueness;
    the atomic reserve-on-accept (all-or-nothing) against a `rowversion` conflict;
    both expiry sweeps.
