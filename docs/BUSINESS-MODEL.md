# Business Model

How Faed makes money, how money moves, and why each choice was made.
Companion document: `CORE.md` (scope), `PHASE-PLAN.md` (execution).

---

## 1. In one sentence

> Faed lets appliance and power-tool merchants in Amman sell **open-box and ex-display**
> stock — opened, returned, showroom or cosmetically imperfect — through listings that must
> disclose the item's condition and why the price is reduced. Merchants pay a monthly
> subscription for the right to publish.

---

## 2. The problem

A merchant has stock that cannot go on the shelf at full price:

- an item returned after the box was opened
- a showroom unit with a scratch
- a carton torn in the warehouse, the appliance inside untouched
- a model superseded by a newer one

Today the options are: sell it at a loss to a liquidator, post it on Facebook with no
credibility, or let it take up space.

The buyer's problem is the mirror image: they see a low price and cannot tell **why**. Used?
Broken? That missing information is what kills the sale.

## 3. The solution

Every listing carries, mandatorily:

1. A **disclosed condition** from four fixed options — no free-text description
2. A **discount reason**, derived from the condition
3. A **defect photo** when the condition discloses a physical imperfection
4. **Admin review** before it becomes publicly visible

The difference from a classifieds site: disclosure is **mandatory and structured**, not
optional and free-form.

## 4. Target market

| | |
|---|---|
| Geography | Amman — Phase 1 |
| Categories | Small Kitchen Appliances · Home & Cleaning · Power Tools & Workshop |
| Price band | 20 – 150 JOD |
| Seller | Verified, subscribed merchants only — individuals cannot sell |
| Buyer | Individuals |

**Why this price band:** items in it are **carryable**, so pickup is a realistic fulfilment
method — which keeps logistics honestly out of scope rather than hand-waved. Large appliances
would force delivery and installation we do not have.

---

## 5. Money flow — the platform never touches it

```
Buyer ──── cash at pickup ────► Merchant
  │                                │
  └──── reserves on the platform   └──── pays a monthly subscription
                                              │
                                              ▼
                                            Faed
```

Faed never receives buyer money.

**Why this is a decision, not a shortcoming:**

- Holding other people's funds pulls the platform into payment-services regulation — a burden
  a pre-launch project cannot carry
- Payment gateways require a registered company and a merchant bank account
- Inspection before payment removes the need for escrow entirely

> ⚠️ **Needs legal confirmation.** Jordanian regulatory requirements for online platforms must
> be reviewed with an accountant or lawyer before any commercial launch. Nothing here is legal
> advice.

---

## 6. Revenue — subscription plans

### 6.1 The rule

**An active subscription is required to publish.** A merchant without one cannot make any
listing live.

### 6.2 Plans

| | **Basic** | **Standard** ⭐ | **Pro** |
|---|---|---|---|
| Monthly | **35 JOD** | **50 JOD** | **80 JOD** |
| Active listings | **15** | **40** | **120** |
| Cost per slot | 2.33 JOD | 1.25 JOD | 0.67 JOD |
| Verified badge | ✓ | ✓ | ✓ |
| Storefront page | ✓ | ✓ | ✓ |
| Analytics | ✓ | ✓ | ✓ |
| Featured placement | — | — | ✓ |

**"Active listings" means live at the same time**, not cumulative. A merchant at the limit
pauses an old listing to free a slot.

**Monthly only.** No prepayment beyond one month, no term discounts, no trial, no free tier —
deliberate simplicity.

**Why these quotas:** the per-slot progression (2.33 → 1.25 → 0.67 JOD) makes Standard the
obvious choice. The quotas are deliberately generous because **at launch the enemy is an empty
catalogue, not a merchant listing too much** — a buyer who opens a site with 30 items does not
come back. The quota should bind the large merchant at the top, not the small one at the door.

Prices and quotas are **seeded reference data**, editable from the admin console without a
deployment — exactly like condition grades.

### 6.3 Why subscription and not commission

> **You cannot charge commission on a transaction you cannot see.**

The platform holds no inventory the goods pass through and no payment rail to deduct from. At
a 7% commission every merchant has an obvious escape: *"come to the shop directly, I'll take
5 JOD off."* There would be **no visibility and no enforcement**.

Marketplaces that succeed on commission hold either the money or the goods. Faed holds
neither.

Subscription inverts this: the merchant pays for **the right to publish and be found**. If
they close a deal off-platform, they still needed the platform to find that buyer, and they
still pay next month.

### 6.4 Collection

**Manual activation:**

1. The merchant transfers the amount (CliQ, bank transfer, or cash)
2. An admin opens their page and clicks **Activate — 1 month**
3. The subscription becomes `Active` with an expiry date and a recorded payment reference
4. A background service moves it to `Expired` on that date, and the merchant's listings stop
   appearing

**Zero payment-gateway integration.** The logic sits behind `ISubscriptionBilling` so a real
provider can be plugged in later without touching application code — the same pattern as the
existing `IFileStorage` and `IEmailSender`.

### 6.5 Subscription states

| State | Meaning |
|---|---|
| `PendingActivation` | Plan chosen, payment not yet recorded. Cannot publish. |
| `Active` | Paid and within the period. Can publish up to the quota. |
| `Expired` | Period ended. Listings are hidden, not deleted; they return on renewal. |
| `Cancelled` | Ended early by admin. Same effect as expired. |

Expiry hides listings rather than archiving them, so renewal is one click and a lapsed
merchant is not punished for coming back.

### 6.6 Plan downgrade

A merchant on Pro with 120 live listings who moves to Basic (quota 15) exceeds the new quota.
The rule:

> The **15 most recently published** listings stay live. The rest are automatically paused
> and the merchant is notified. Nothing is deleted, and they can choose which to unpause.

### 6.7 The arithmetic

| Assumption | Value |
|---|---|
| Average subscription (most on Standard) | 50 JOD |
| **20 merchants** | **1,000 JOD / month** |
| 40 merchants | 2,000 JOD / month |
| 60 merchants | 3,000 JOD / month |

**Revenue is independent of order volume** — it is unaffected by transactions leaking
off-platform.

---

## 7. Ordering: verify first, then charge

### 7.1 The rule

Two independent conditions gate publishing:

1. **Approved verification**
2. **Active subscription**

**And the order is: verification before payment.**

### 7.2 The merchant's path

```
Register ──► Upload documents ──► Build listing drafts (while waiting)
                                          │
                                   Admin approves ✓
                                          │
                        "Your store is approved — choose a plan to publish"
                                          │
                              Pay ──► Activated ──► Publish
```

### 7.3 Why this order

| # | Reason |
|---|---|
| 1 | **Never take money from someone you might reject.** Paying before rejection forces a manual refund flow that does not exist and damages trust. |
| 2 | **Approval is a positive moment to ask for money** — far stronger than asking a stranger to pay at signup. |
| 3 | **It qualifies merchants for free.** Reviewing documents costs nothing and filters out the non-serious before any money conversation. |
| 4 | **The commitment ladder is correct**: small commitment → medium → **reward** → ask. |
| 5 | **It removes refunds from the project entirely.** |

### 7.4 Two properties of this path

1. **Drafts are available immediately after registration** — before approval and before
   payment. The merchant photographs stock and prepares listings while waiting. No dead time,
   no empty screen.
2. **The two conditions are technically independent.** An approved merchant with no
   subscription is a valid state, and the publish gate checks both — cleaner than coupling
   them.

### 7.5 The known risk

Requiring payment to publish means **the first merchant pays for a platform with no buyers.**
This is the hardest sale in the project's life and its biggest risk.

Mitigations:

- Verification before payment — zero friction at the door
- Drafts immediately — the merchant sees the product's value before paying
- **Recruit the first 10 merchants by hand and in person**, not through self-service signup

---

## 8. Handoff and communication

### 8.1 The governing principle

> **Nothing in the system waits on a human forever.**

The failure is not the order statuses — it is assuming the merchant sits at a computer. He
does not; he is in his shop selling, and he will not open a dashboard.

Left alone, this is what happens: the buyer reserves, nobody confirms, they stay in limbo,
they leave and do not return. **This is the single most dangerous operational defect in any
marketplace built on small merchants.**

### 8.2 Layer one — deadlines with automatic outcomes

| Situation | Deadline | Automatic outcome |
|---|---|---|
| Reserved, merchant has not confirmed | **12 h** | Cancel · release stock · notify buyer: *"The shop did not respond — your reservation was cancelled and you lost nothing"* |
| Confirmed, buyer has not collected | **48 h** | `NoShow` · stock returns to sale |
| Ready for pickup, no movement | **72 h** | Auto-close |

**Why 12 hours and not 6:** a reservation placed at 11 pm with a 6-hour deadline expires at
5 am with the shop closed — cancelling a perfectly good order. Twelve hours covers the
late-night reservation and still resolves within half a day.

**The buyer learns the outcome within 12 hours no matter what**, and moves on to another item
instead of waiting.

The pattern already exists in `ReservationExpiryService` — it needs extending, not inventing.

### 8.3 Layer two — WhatsApp deep link

A button on the order page:

> `📱 Message the shop on WhatsApp`

generates a `wa.me` link with a pre-written message:

> *"Hi, I reserved [item] on Faed. Order #FA7K2M"*

**No API, no business account, no cost, no approvals.** An ordinary link that opens WhatsApp
on the merchant's phone — the one app he actually has open all day. The same button exists on
the merchant's side to reach the buyer.

Both parties would move to WhatsApp regardless. This turns that leakage into a feature: **the
platform becomes the thing that starts the conversation correctly.**

### 8.4 Layer three — merchant response rate

On every storefront and under every listing:

> ⚡ **Usually responds within 2 hours** · 94% response rate

And a merchant who ignores orders **ranks lower in the catalogue**.

Now ignoring orders has a visible price — which is how large marketplaces solve this instead
of chasing merchants. **It needs no new entity:** it is computed from order statuses and
timestamps.

**Formula.** Rolling 30 days. Response rate = orders confirmed before the 12-hour deadline ÷
orders received. Response time = median minutes from reservation to confirmation, displayed in
buckets ("within an hour", "within 2 hours", "within a day"). Below **5 orders in the window**
neither number is shown; the storefront shows a neutral **"New seller"** badge instead, so a
merchant with one confirmed order cannot display "100%".

### 8.5 Order lifecycle

| # | Event | Actor | Status |
|---|---|---|---|
| 1 | Reserve, stock held, timer starts | Buyer | `Pending` |
| 2 | **Confirm** (within 12 h) | Merchant | `Confirmed` |
| 3 | ↳ contact details revealed to both | — | — |
| 4 | **Ready for pickup** | Merchant | `ReadyForPickup` |
| 5 | Inspect · pay cash · collect | both | — |
| 6 | **Complete** | Merchant | `Completed` |
| — | Cancel with a fixed reason | either | `Cancelled` |
| — | Not collected within 48 h | automatic | `NoShow` |

### 8.6 No in-app chat

Every interaction inside the platform is a button that changes a status. Anything beyond that
moves to WhatsApp.

**Result: no content moderation, no abuse reports, no notification infrastructure, no cost.**

### 8.7 Contact reveal rule

| | Before confirmation | After confirmation |
|---|---|---|
| Buyer sees | Shop area only | Full address · map · pickup hours · phone · WhatsApp button |
| Merchant sees | The order only | Buyer's phone · WhatsApp button |

This protects merchant contact details from scraping and gives the confirm button real meaning.

### 8.8 Pickup is the correct design, not a limitation

Selling a scratched appliance sight-unseen with no escrow is a dispute factory.

**Inspection before payment solves it at the root:** the buyer sees the scratch before parting
with a dinar. That alone justifies deferring the dispute system.

---

## 9. Out of scope — by decision

| Excluded | Reason |
|---|---|
| Payment processing, wallets | Requires a registered entity and licensing |
| Escrow | Triggers financial regulation — and inspection makes it unnecessary |
| Shipping, logistics, warehousing | No infrastructure and no capital for it |
| Auctions | Complexity with no validated demand |
| B2B merchant-to-merchant | Deferred — design archived in `B2B-DESIGN.md` |
| Disputes | Deferred — inspection before payment reduces the need |
| In-app messaging | WhatsApp does it free and better |
| Annual plans, discounts, trials | Deliberate simplicity — monthly only |
| Individual sellers | Verified, subscribed merchants only |
| Anywhere outside Amman | Later phase |

---

## 10. Success metrics — Phase 1

| Metric | 3-month target |
|---|---|
| Paying merchants | 15 |
| Published listings | 150 |
| Completed orders | 80 |
| **Subscription renewal rate** | **above 60%** |
| Orders confirmed within 12 h | above 80% |
| Median listing review time | under 24 h |

**The metric that matters is renewal.** A merchant who pays a second month is the only proof
the platform works. Every other number can be flattered.

---

## 11. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| No merchant pays for an empty platform | **Fatal** | Verification before payment · instant drafts · recruit first 10 by hand |
| Merchant does not check the site | **High** | 12 h auto-cancel · WhatsApp link · public response rate |
| Merchants subscribe then never publish | High | Analytics show the value of publishing · automated reminders |
| Misleading listings damage trust | High | Mandatory admin review · required defect photo |
| Thin catalogue, buyers do not return | High | Generous quotas · depth in one category over breadth |
| No visibility into actual sales | Medium | Accepted — revenue comes from subscriptions, not orders |

---

## 12. Decision log

| # | Decision |
|---|---|
| 1 | Sector: Open-Box & Ex-Display appliances and power tools |
| 2 | B2B module removed from Phase 1; design archived |
| 3 | Platform never handles buyer money — cash at pickup |
| 4 | Revenue: monthly subscription plans — no commission |
| 5 | Verification **and** active subscription both required to publish |
| 6 | No permanent free tier |
| 7 | No trial period |
| 8 | Plans: 35/15 · 50/40 · 80/120 |
| 9 | Monthly only — no prepayment beyond a month, no discounts |
| 10 | Verification and approval first, payment second |
| 11 | Automatic deadlines: 12 h confirm · 48 h collect · 72 h close |
| 12 | WhatsApp deep link instead of in-app messaging |
| 13 | Public response rate, factored into ranking; hidden below 5 orders |
| 14 | Contact details revealed after order confirmation |
| 15 | Manual collection behind `ISubscriptionBilling` |
| 16 | Dispute system deferred |
| 17 | Downgrade keeps the newest listings within quota, auto-pauses the rest |
| 18 | Database is dropped and reseeded — production data is not preserved |
