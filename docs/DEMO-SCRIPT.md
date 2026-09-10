# Faed — defence demo script

A 10–12 minute walkthrough of the three role journeys in the acceptance sentence:

> A verified, subscribed merchant publishes an open-box or ex-display item with a disclosed
> condition, discount reason and defect photo; an admin reviews it; a buyer reserves it, then
> inspects and pays cash at pickup — with no overselling when two orders arrive at once.

Run it top to bottom. Every account and every order below is created by the demo seed
(`DemoDataSeeder`), so the state is the same on every fresh run.

---

## 0. Before the panel arrives

```bash
dotnet ef database drop -f --project src/Faed.Web
dotnet ef database update --project src/Faed.Web
dotnet run --project src/Faed.Web
```

Requires, in user secrets or environment:

```
Faed:DemoSeed:Enabled = true
Faed:DemoSeed:Password = Demo!Pass123      # the shared password for every demo account
```

Wait for the log line **`Demo data set seeded.`**, then open <http://localhost:5074>.

Open three browser profiles (or one normal + two private windows) so you can stay logged in
as the buyer, the merchant and the admin at once. Every account's password is `Demo!Pass123`.

| Role | Email | What they are |
|---|---|---|
| Admin | `demo-admin@faed.local` | Reviews verification, moderation, subscriptions |
| Merchant — Amman Kitchen Co. | `merchant-a@faed.local` | Verified, **Standard** plan (40 listings), full order history |
| Merchant — Petra Power Tools | `merchant-b@faed.local` | Verified, **Basic** plan (15), one listing still in moderation |
| Merchant — Sahara Appliance Outlet | `unsubscribed-merchant@faed.local` | Verified but **no subscription** — sees the plan chooser |
| Merchant — Rainbow Home Essentials | `pending-merchant@faed.local` | Documents submitted, **awaiting admin approval** |
| Buyer — Layla Haddad | `buyer-a@faed.local` | Orders in Pending, Ready, Cancelled, Completed |
| Buyer — Omar Nasser | `buyer-b@faed.local` | Orders in Confirmed, No-show, plus the one 5★ review |

---

## Act 1 — The buyer reserves (≈4 min)

Start logged out, on the home page.

1. **Home** (`/`). Point out the three category cards and the "Newest on Faed" grid. Every
   card carries a **condition stamp** in the corner of the photo — one rust hue, four fill
   levels (`GRADE A`–`GRADE D`), a battery meter, not a traffic light. Nothing here is faulty;
   only its history differs.
2. **Shop** (`/shop`). Four controls — text search, Category, Condition, Price — and a **Sort**
   control pushed to the far right (Newest · Price low–high · Price high–low). No advanced
   drawer. Filter to **Condition → Ex-display** to show the meter change across the grid.
3. Open **Upright Vacuum Cleaner** (Amman Kitchen Co., `GRADE D`, 45.000 JD, was 79.000).
   On the detail page:
   - The **condition sentence** next to the title — plain language, no grade codes.
   - The merchant mini-card: square ink avatar, **Verified** badge, *"Usually responds within
     an hour · 67% response rate"* (Amman Kitchen Co. has ≥ 5 recent orders, so a percentage
     shows; a newer shop shows a **New seller** badge instead — see Petra Power Tools).
   - **Disclosed condition** — the defect photo, tagged `DEFECT` in rust, on a real background.
     The clean product cutout vs. the rough defect photo is the whole point: *this exact unit,
     not a catalogue picture.*
   - The price block: **Reserve**, never "checkout". The line underneath — held for 12 hours,
     cancelled if the shop doesn't confirm, **pay cash at the shop**.
   - The grey notice: the shop's **full address and phone appear only after it confirms**.
4. Log in as **Layla Haddad** (`buyer-a@faed.local`) and click **Reserve**. The confirmation
   is three lines: where to collect (area only), the 12-hour window, cash at pickup.
5. **Buyer → Orders** (`/Buyer/Orders`). The new reservation sits at **Awaiting merchant
   confirmation**. Open it: the buyer sees the shop's **area** (Abdali), and *"Full address —
   revealed once the shop confirms your reservation."* No phone, no WhatsApp button yet.

Leave this order pending — Act 2 confirms it.

---

## Act 2 — The merchant publishes and fulfils (≈4 min)

Switch to the **Amman Kitchen Co.** window (`merchant-a@faed.local`).

### 2a. Publish an item — one page

1. **Merchant Center → Listings → New listing** (`/Merchant/Listings/Create`).
2. Walk the single page: photos, title, category (three cards), **"What's the condition?"**
   (four cards), warranty, price, optional original price, quantity, optional description.
   **Nine fields, eight decisions.** The merchant never sees "variant", "SKU", "grade" or
   "discount reason".
3. Click the **Box opened or damaged** card — a **"Photo of the scratch or box"** upload
   appears inline, right there, not as an error later. (The **Ex-display** card does the same.)
4. Fill enough to publish, click **Publish**. The listing now reads **Under review** — one
   button, no separate "submit for review" step.
5. Point out the publish gate needs **two independent things**: approved verification **and**
   an active subscription. To show that concretely, open the **Sahara Appliance Outlet**
   window (`unsubscribed-merchant@faed.local`): it has a ready draft (**Espresso Machine**)
   but **Subscription** shows *"You're approved to sell. Choose a plan"* — Basic 35 / Standard
   50 / Pro 80 JOD. Approved, not subscribed, is a normal state — not an error page.

### 2b. Confirm the buyer's reservation

1. Back as **Amman Kitchen Co.**, go to **Orders**. The Upright Vacuum reservation from Act 1
   is under **Needs confirmation**. Open it and **Confirm**.
2. Note the order now shows the buyer's **collection deadline as a date and time** ("Collect
   by …"), not a countdown — the deadline is shown to the person it binds.
3. Flip back to **Layla's** order page and refresh: the **full address, pickup hours, phone
   and a WhatsApp button** (`wa.me` deep link with the order reference pre-filled) are now
   visible. Contact detail is revealed only on confirmation.
4. Optional — walk the rest of the lifecycle on other seeded orders: **Ready for pickup**
   (`#Z3ZVKV`), **Completed** with its 5★ review (Omar Nasser), **Cancelled**, **No-show**.
   Every state is already present.

### 2c. No overselling

Open **Handheld Vacuum** (Petra Power Tools) — it shows **Sold out**. A seeded buyer cleared
the last units; the `ListingVariant.RowVersion` concurrency token means two simultaneous
reservations can never both take the same physical unit. Most listings are a single item, so
this is the core correctness property.

---

## Act 3 — The admin reviews (≈3 min)

Switch to the **admin** window (`demo-admin@faed.local`) → `/Admin`.

1. The dashboard shows the **queues that need attention**.
2. **Merchant verification** (`/Admin/MerchantVerification`). **Rainbow Home Essentials** is
   awaiting review — open it, look at the submitted commercial-registration document, and
   **Approve**. Approval grants the Merchant role; the shop can now choose a plan.
3. **Listing moderation** (`/Admin/ListingModeration`). **Precision Screwdriver Set** (Petra
   Power Tools) is waiting. Approve it (or reject with a note) — every decision is a new
   `ListingModeration` row; past decisions are never edited. It appears in the shop
   immediately after approval.
4. **Subscriptions** (`/Admin/Subscriptions`). Show Amman Kitchen Co. on Standard and Petra on
   Basic. Activate a month for a merchant with a recorded payment reference — the platform
   records a manually-entered payment; **money never touches Faed**.
5. **Reference data** (`/Admin/Catalog`). Categories, condition grades, discount reasons and
   plans are all **seeded and admin-editable** — nothing is hard-coded in business logic.

---

## Closing line

Every screen you saw — the shop, the stamp, the reserve flow, the workspace — enforces one
sentence: a disclosed-condition item, reviewed by an admin, reserved by a buyer who inspects
and pays in cash, with the stock never oversold. No payments, no messaging, no disputes —
those were removed on purpose.

---

## Notes / known rough edges

- **Product photos** are real, openly-licensed stock (see `docs/CREDITS.md`), background-
  removed and dropped onto the card colour. The **circular saw** and **angle grinder** cut-outs
  are the weakest — free stock has almost no brand-free isolated power-tool shots. They read
  fine at card size.
- Some product photos carry a faint brand mark (Vitamix, Viking). A real marketplace shows real
  brands; this was a deliberate call over swapping half the catalogue.
- **Defect photos** are a pool of six, reused across listings and intentionally not matched to
  the product — exactly as `DESIGN-BRIEF.md` §8 allows.
- Workspaces (merchant / admin) got a **token pass only**, not a redesign (`DESIGN-BRIEF.md`
  §6). The five public screens are the fully-designed surface.
- The background services run their sweeps on a timer; you will not see a 12-hour auto-cancel
  during a live demo. The seeded **Cancelled** and **No-show** orders show the end states.
