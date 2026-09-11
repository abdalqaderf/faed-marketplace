# Rehearsal checklist — authenticated three-role walkthrough

Run this by hand on the machine the defence will use, after the Step 9 clone-to-running
rehearsal in `docs/AUDIT-PHASE-14.md` has already passed on that machine. This is the
timed version of `docs/DEMO-SCRIPT.md` — same accounts, same order, one row per click, a
blank column for how long each step actually took. Steps marked **✓ script** are the ones
`docs/DEMO-SCRIPT.md` depends on; if one of those breaks, the demo script needs rewriting
before the defence, not just this checklist.

## Before you start

```bash
dotnet ef database drop -f --project src/Faed.Web
dotnet ef database update --project src/Faed.Web
dotnet run --project src/Faed.Web
```

With `Faed:DemoSeed:Enabled=true` and `Faed:DemoSeed:Password=Demo!Pass123` set. Wait for
**`Demo data set seeded.`** in the console, then open the app's URL. Every account's password
is `Demo!Pass123`. Open three browser profiles (or one normal window + two private windows) so
the buyer, the merchant and the admin can stay signed in at once.

| Role | Email |
|---|---|
| Admin | `demo-admin@faed.local` |
| Merchant — Amman Kitchen Co. | `merchant-a@faed.local` |
| Merchant — Petra Power Tools | `merchant-b@faed.local` |
| Merchant — Sahara Appliance Outlet | `unsubscribed-merchant@faed.local` |
| Merchant — Rainbow Home Essentials | `pending-merchant@faed.local` |
| Buyer — Layla Haddad | `buyer-a@faed.local` |
| Buyer — Omar Nasser | `buyer-b@faed.local` |

---

## Act 1 — Buyer reserves

| # | Account | URL / control | You should see | Time |
|---|---|---|---|---|
| 1 | Signed out | `/` | Three category cards, a listing grid, a condition stamp (`GRADE A`–`GRADE D`) in one rust hue on every card photo | ___ |
| 2 ✓ script | Signed out | `/shop` → set **Condition** to **Ex-display** | Search + Category/Condition/Price + a **Sort** control at the far right; grid updates to Grade D items; no advanced-filter drawer | ___ |
| 3 ✓ script | Signed out | Open **Upright Vacuum Cleaner** (`/listing/upright-vacuum-cleaner`) | `GRADE D` stamp; condition sentence in plain language; Amman Kitchen Co. mini-card with **Verified** badge and a response-rate line (not New seller — ≥5 orders); a `DEFECT` photo on a real background; price 45.000 JD, was 79.000; **Reserve** button; "held for 12 hours… cash at the shop"; a grey notice that the address appears after confirmation | ___ |
| 4 ✓ script | Sign in as `buyer-a@faed.local` | Click **Reserve** on that listing | A three-line confirmation: area to collect from, 12-hour window, cash at pickup | ___ |
| 5 ✓ script | Layla (buyer-a) | `/Buyer/Orders` → open the new order | Status **Awaiting merchant confirmation**; shop area only (Abdali); no phone, no WhatsApp button yet | ___ |

Leave this order pending — Act 2 confirms it.

---

## Act 2 — Merchant publishes and fulfils

| # | Account | URL / control | You should see | Time |
|---|---|---|---|---|
| 6 ✓ script | `merchant-a@faed.local` | `/Merchant/Listings/Create` | One page: photos, title, category (3 cards), **"What's the condition?"** (4 cards), warranty, price, optional original price, quantity, optional description — nine fields, eight decisions; no "variant", "SKU", "grade" or "discount reason" anywhere | ___ |
| 7 ✓ script | same page | Click the **Box opened or damaged** card | A **"Photo of the scratch or box"** upload appears inline immediately | ___ |
| 8 ✓ script | same page | Fill the required fields → **Publish** | The listing shows **Under review** — one button, no separate submit step | ___ |
| 9 ✓ script | `unsubscribed-merchant@faed.local` | `/Merchant/Subscription` | *"You're approved to sell. Choose a plan"* (Basic 35 / Standard 50 / Pro 80 JOD) — approved-but-unsubscribed is a normal screen, not an error | ___ |
| 10 ✓ script | `merchant-a@faed.local` | `/Merchant/Orders` → open the Upright Vacuum order → **Confirm** | Order moves to Confirmed; the page states the buyer's **collection deadline as a date and time** ("Collect by …"), not a countdown | ___ |
| 11 ✓ script | Layla (buyer-a) | Refresh `/Buyer/Orders` → the same order | Full address, pickup hours, phone and a **WhatsApp** button (`wa.me`, order reference pre-filled) are now visible | ___ |
| 12 | `merchant-b@faed.local` | Open **Handheld Vacuum** in the shop | Listing shows **Sold out** — a seeded buyer already took the last unit; the `RowVersion` concurrency token is what stops two reservations taking the same unit at once | ___ |

---

## Act 3 — Admin reviews

| # | Account | URL / control | You should see | Time |
|---|---|---|---|---|
| 13 ✓ script | `demo-admin@faed.local` | `/Admin` | A dashboard naming the queues that need attention | ___ |
| 14 ✓ script | admin | `/Admin/MerchantVerification` → open **Rainbow Home Essentials** → **Approve** | The verification document is visible; after approving, the merchant can choose a plan | ___ |
| 15 ✓ script | admin | `/Admin/ListingModeration` → open **Precision Screwdriver Set** → **Approve** | It appears in the shop immediately; a rejected-then-resubmitted listing would show every past decision, never an edited one | ___ |
| 16 ✓ script | admin | `/Admin/Subscriptions` → activate a month for a merchant | A payment reference is recorded; no payment form, no card details anywhere | ___ |
| 17 ✓ script | admin | `/Admin/Catalog` | Categories, condition grades, discount reasons and plans, all editable | ___ |

---

## Phase 14 addition — the no-show recovery (not in `docs/DEMO-SCRIPT.md`)

The seeded **Countertop Blender** order (Omar Nasser → Amman Kitchen Co.) is a `NoShow`: the
sale happened but the merchant never opened the dashboard in time. This is the dead end
HIGH-2 found and fixed — the panel can walk into it and out of it in under a minute.

| # | Account | URL / control | You should see | Time |
|---|---|---|---|---|
| 18 | `merchant-a@faed.local` | `/Merchant/Orders` → open the **Countertop Blender** order (status **No-show**) | A note explaining the order was closed as a no-show, and a button **"The buyer actually collected this"** | ___ |
| 19 | same order | Click **The buyer actually collected this** | Order status becomes **Completed**; the stock is booked as sold (not double-released) | ___ |
| 20 | Omar Nasser (buyer-b) | `/Buyer/Orders` → the same order | A **Leave a review** control is now available, exactly as after a normal completion | ___ |

---

## After the walkthrough

- [ ] No empty screen, broken image, 404, unhandled exception, or slow page anywhere above
- [ ] Every ✓ script row matched what `docs/DEMO-SCRIPT.md` says — if not, fix the script before the defence
- [ ] `dotnet ef database drop -f --project src/Faed.Web` to leave a clean slate
