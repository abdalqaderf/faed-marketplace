# Faed — Open-Box & Ex-Display Marketplace

Faed is a web marketplace where verified, subscribed merchants in Amman sell **open-box and
ex-display stock** — appliances and power tools that were returned, opened, dented, scratched
or used as showroom units — instead of writing them off or dumping them on classifieds.

It is not a classifieds site. Every listing carries a **disclosed condition**, a **reason the
price is reduced**, and a **photo of the defect** when one exists, so a buyer always knows
*why* an item is cheap before reserving it. Every listing is reviewed by an administrator
before it goes live.

Faed never handles money. Buyers reserve online and pay the merchant in cash at pickup. The
platform earns from **monthly merchant subscriptions**, not commission.

## MVP scope

| | |
|---|---|
| Market | Amman, Jordan |
| UI language | English only |
| Currency | JOD, stored with 3 decimal places |
| Launch sector | Open-Box & Ex-Display |
| Launch categories | Small Kitchen Appliances · Home & Cleaning · Power Tools & Workshop |
| Price band | 20 – 150 JOD (carryable items, so pickup is realistic) |
| Sellers | Verified **and** subscribed merchants only |
| Buyers | Individuals |
| Payment | Cash on pickup, merchant to buyer — the platform never touches it |
| Revenue | Monthly merchant subscription with an active-listing quota |

## Main features

- **Merchant onboarding** — register, upload business documents, build listing drafts while
  waiting for review. Verification is approved *before* any payment is asked for.
- **Subscriptions** — three monthly plans, each with an active-listing quota. An active
  subscription plus approved verification are the two independent conditions for publishing.
- **Listings** — a single-page form with eight fields. One condition question fills both the
  condition grade and the discount reason; picking a condition that discloses a physical
  imperfection reveals the defect-photo upload inline. Warranty is a structured field, not
  free text — for a 120 JOD appliance it is the second question every buyer asks.
- **Admin moderation** — every listing is reviewed before it goes live, and rejection history
  is preserved rather than overwritten.
- **Public storefront** — browsing, search, four filters, per-merchant storefronts.
- **Reservations** — buyers reserve rather than check out; stock is held, contact details are
  revealed after the merchant confirms, and payment happens in person.
- **Nothing waits on a human forever** — unconfirmed reservations, uncollected orders and
  expired subscriptions are all resolved automatically by background services.
- **WhatsApp handoff** — a `wa.me` deep link with a pre-filled message replaces in-app chat.
- **Merchant response rate** — publicly displayed and factored into catalogue ranking, so
  ignoring orders has a visible cost.
- **Reviews** — only after a completed order, one per order.

## Roles

- **Buyer** — browses, reserves, tracks orders, reviews after collection. Cannot sell.
- **Merchant** — submits verification, subscribes, creates drafts immediately, publishes once
  approved and subscribed, manages stock and orders, sees analytics. Cannot buy.
- **Admin** — approves verification, activates subscriptions, moderates listings, manages
  reference data, monitors orders.

A user holds exactly one role. There is no storefront login separate from the merchant
account that owns it.

## The condition question

Merchants are asked one question instead of two overlapping taxonomies:

| Card | What the merchant sees | Stored as |
|---|---|---|
| **Sealed** | New, unopened box | Grade A + Overstock |
| **Box opened or damaged** | Item is new and unused | Grade B + PackagingDamage |
| **Customer return** | Opened and checked, never used | Grade C + CustomerReturn |
| **Ex-display** | Light scratches or marks from showroom use | Grade D + DisplayItem |

One click fills both database fields. Cards 2 and 4 disclose a physical imperfection, so
selecting either reveals a required defect-photo upload.

The full form is: photos · title · category · condition · warranty · price · original price
(optional) · quantity. Eight fields, one page, one Publish button.

## Subscription plans

| | Basic | Standard | Pro |
|---|---|---|---|
| Monthly | 35 JOD | 50 JOD | 80 JOD |
| Active listings | 15 | 40 | 120 |
| Verified badge · storefront · analytics | ✓ | ✓ | ✓ |
| Featured placement | — | — | ✓ |

Monthly only — no annual terms, no prepayment, no trial. Prices and quotas are **seeded
reference data**, editable from the admin console without a deployment.

Payment is collected out of band (bank transfer, CliQ, cash) and an administrator activates
the subscription. The logic sits behind `ISubscriptionBilling` so a real payment provider can
be plugged in later without touching application code — the same pattern as `IFileStorage`
and `IEmailSender`.

## Order lifecycle

| Step | Actor | Status |
|---|---|---|
| Reserve, stock held, timer starts | Buyer | `Pending` |
| **Confirm** (within 12 hours) | Merchant | `Confirmed` |
| ↳ contact details revealed to both sides | — | — |
| **Ready for pickup** | Merchant | `ReadyForPickup` |
| Inspect · pay cash · collect | both | — |
| **Complete** | Merchant | `Completed` |
| Cancel with a fixed reason | either | `Cancelled` |
| Not collected within 48 hours | automatic | `NoShow` |

Automatic timeouts: **12 h** to confirm (chosen over a shorter window so a late-night
reservation does not expire while the shop is closed), **48 h** to collect, **72 h** to close
a stale ready-for-pickup order. There is no in-app chat — every interaction is a button that
changes a status, and anything beyond that moves to WhatsApp.

## Technology stack

- ASP.NET Core MVC on **.NET 10**
- Entity Framework Core (Code First) + **SQL Server**
- ASP.NET Core Identity for authentication and role management
- Razor Views + Bootstrap 5 + vanilla JavaScript
- Object storage, email and billing sit behind interfaces (`IFileStorage`, `IEmailSender`,
  `ISubscriptionBilling`) so real providers plug in without touching application code

## Architecture

A single ASP.NET Core MVC project (`src/Faed.Web`), organized by concern:

- `Controllers/` — public MVC endpoints; role-specific controllers live under `Areas/`
- `Areas/{Buyer,Merchant,Admin,Identity}/` — role-scoped controllers and views
- `Models/{Entities,Enums,Identity}/` — the EF Core domain model
- `Data/` — `ApplicationDbContext`, entity configurations, migrations, seeders
- `Services/` — business logic by domain area (`Ordering`, `Listings`, `Subscriptions`,
  `Merchants`, `Catalog`, `Analytics`, `Storage`, …)
- `Authorization/` — named policies and handlers enforcing the role rules above
- `ViewModels/`, `Rendering/` — view-facing shaping and display helpers

Stock quantities use SQL Server `rowversion` so two simultaneous reservations cannot oversell
the same unit — which matters more here than in a normal shop, because most listings are a
single physical item. Reservation, no-show and subscription expiry are handled by hosted
background services rather than request-time checks.

## Domain model

Seventeen entities:

**Identity & merchant** — `ApplicationUser` · `MerchantProfile` ·
`MerchantVerificationDocument` · `MerchantLocation`

**Subscriptions** — `SubscriptionPlan` · `MerchantSubscription`

**Catalog** — `Category` · `ConditionGrade` · `DiscountReason`

**Listing** — `Listing` · `ListingDiscountReason` · `ListingMedia` · `ListingVariant` ·
`ListingReferencePriceEvidence` · `ListingModeration`

**Transactions** — `Order` · `OrderItem` · `Review`

Merchant response rate needs no entity — it is computed from order statuses and timestamps.

The option/variant tables remain in the schema but are hidden from the UI: an item is one
physical unit, so a single variant with a generated SKU is created automatically and the
merchant never sees the words "variant" or "SKU".

Transactional history is never hard-deleted. "Pause" hides a listing and frees a quota slot;
"Delete" archives it, and the record stays in the database so past orders remain meaningful.

The conceptual entity/relationship model is documented in `docs/04-DOMAIN-MODEL.md`. The
scope and business reasoning behind it live in `docs/CORE.md` and `docs/BUSINESS-MODEL.md`.

## Database

- **SQL Server** via **EF Core Code First** — the schema is generated from the entity classes
  in `Models/Entities` and `Data/Configurations`, not written by hand.
- Migrations in `src/Faed.Web/Data/Migrations` are the only way the schema changes; the
  application does not migrate the database on startup.

## Prerequisites

- **.NET 10 SDK**
- **SQL Server** — LocalDB (`(localdb)\MSSQLLocalDB`) is enough for local development; any
  reachable instance, including a container, also works
- `dotnet tool install --global dotnet-ef`

## Local setup

```bash
# 1. restore + build
dotnet build Faed.slnx

# 2. create the database
dotnet ef database update --project src/Faed.Web

# 3. run
dotnet run --project src/Faed.Web
```

On startup the app idempotently seeds the Identity roles (`Buyer`, `Merchant`, `Admin`) and
the reference data — condition grades A–D, the approved discount reasons, the three
subscription plans, and the Open-Box & Ex-Display launch taxonomy. It does not create the
database or apply migrations itself.

The development connection string lives only in `src/Faed.Web/appsettings.Development.json`
(a passwordless LocalDB database named `Faed`). The committed `appsettings.json` has none;
any non-`Development` environment must supply `ConnectionStrings__DefaultConnection`, and the
app fails fast if it is missing or still points at LocalDB. See `DEPLOYMENT.md`.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>" --project src/Faed.Web
# or
export ConnectionStrings__DefaultConnection="<value>"
```

### Optional: a development administrator

```bash
dotnet user-secrets set "Faed:AdminSeed:Email" "admin@faed.local" --project src/Faed.Web
dotnet user-secrets set "Faed:AdminSeed:Password" "<development-password>" --project src/Faed.Web
```

Seeded only in `Development`. Re-running is safe.

## Demo data

A deterministic, Development-only, password-gated demo set: an admin, two approved and
subscribed merchants, one merchant awaiting verification, two buyers, a catalog across all
three launch categories with generated product images, and one example of every order state.
It is built by calling the same application services a real user would, so nothing bypasses
moderation, authorization, quota enforcement or stock concurrency.

```bash
dotnet user-secrets set "Faed:DemoSeed:Enabled" "true"        --project src/Faed.Web
dotnet user-secrets set "Faed:DemoSeed:Password" "<demo-password>" --project src/Faed.Web

dotnet ef database update --project src/Faed.Web
dotnet run --project src/Faed.Web
```

| Email | Role |
|---|---|
| `demo-admin@faed.local` | Administrator |
| `merchant-a@faed.local` | Approved + subscribed merchant |
| `merchant-b@faed.local` | Approved + subscribed merchant |
| `pending-merchant@faed.local` | Awaiting verification |
| `buyer-a@faed.local`, `buyer-b@faed.local` | Individual buyers |

Re-running never duplicates data. Passwords are set only at account creation — change
`Faed:DemoSeed:Password` after the fact and the old one keeps working until the database is
dropped and recreated.

## Project structure

```text
Faed.slnx
README.md
DEPLOYMENT.md
docs/
  CORE.md                       scope, roles, user journeys, entity list
  BUSINESS-MODEL.md             revenue, money flow, handoff protocol, decision log
  04-DOMAIN-MODEL.md            entity/relationship reference
  B2B-DESIGN.md                 archived design for the deferred B2B module
src/Faed.Web/
  Areas/{Admin,Merchant,Buyer,Identity}/
  Controllers/
  Models/{Entities,Enums,Identity}/
  ViewModels/
  Data/{ApplicationDbContext.cs,Configurations/,Migrations/,Seed/}
  Services/
  Authorization/
  Rendering/
  wwwroot/
```

## Out of scope — by decision

- **Payments, escrow, shipping, logistics, warehousing.** Orders model pickup and
  merchant-arranged delivery only. Not handling funds is a deliberate choice: it keeps the
  platform out of payment-services regulation, and inspection-before-payment removes the need
  for escrow.
- **B2B merchant-to-merchant trading.** Deferred; the design is archived in
  `docs/B2B-DESIGN.md`.
- **Disputes.** Deferred — the buyer inspects the item before paying.
- **In-app messaging.** WhatsApp deep links do the job with no moderation surface and no cost.
- **Annual plans, discounts, free tiers.** Monthly only, deliberately.
- **Individual sellers.** Verified, subscribed merchants only.
- **Anywhere outside Amman.**

Jordanian regulatory requirements for online platforms should be confirmed with an accountant
or lawyer before any commercial launch. Nothing here is legal advice.
