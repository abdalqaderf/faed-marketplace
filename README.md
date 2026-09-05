# Faed — Surplus Inventory Marketplace

Faed is a web marketplace that lets verified merchants in Jordan recover value from
surplus and non-perfect inventory — overstock, past-season stock, open-box items, display
units, minor cosmetic defects, damaged packaging — instead of writing it off or dumping it
through unstructured channels. Merchants sell the same inventory either to individual
buyers (B2C) or to other verified merchants for resale (B2B), through one structured
workflow instead of ad-hoc social media posts or liquidators.

It is not a general classifieds site. Every listing carries a disclosed condition grade
and, where relevant, a specific discount reason and evidence photo, so a buyer always knows
*why* an item is discounted before ordering.

## MVP scope

| | |
|---|---|
| Market | Amman, Jordan |
| UI language | English only |
| Currency | JOD, stored with 3 decimal places |
| Sellers | Verified merchants only (individuals can buy but cannot sell) |
| Buyers | Individuals and verified merchants |
| Launch sector | Fashion Overstock |
| Launch categories | Clothing · Shoes · Bags & Accessories |

## Main features

- Merchant business verification with admin review of submitted documents.
- Listings with condition grading (A–D), a fixed set of discount reasons, evidence photos,
  variants and per-variant stock, with admin moderation before a listing goes live.
- Public storefront: browsing, search, category filters, per-merchant storefronts.
- B2C ordering with a reservation window, order-state tracking, cancellation, delivery vs.
  pickup fulfillment, receipt confirmation, disputes and post-purchase reviews.
- B2B flow: merchants submit offers on another merchant's stock, counter-offer negotiation,
  accepted deals with their own reservation and fulfillment tracking.
- Dispute handling and an admin resolution queue.
- Admin console: merchant verification, listing moderation, catalog/reference-data
  management, order/deal monitoring, dispute resolution, review moderation, audit log.
- Merchant analytics for listing performance and recovered inventory value.

## Roles

- **Buyer** — registers, browses, orders B2C, tracks orders, opens disputes, leaves reviews.
  Cannot create listings or sell.
- **Merchant** — registers and submits business verification; once approved, creates and
  manages listings/stock, sells B2C, and both buys and sells B2B through negotiated offers
  and deals. A merchant awaiting approval can manage their verification only.
- **Admin** — reviews merchant verification, moderates listings, manages catalog reference
  data, monitors orders/deals, resolves disputes, moderates reviews, and has audit visibility
  across the platform.

A user can hold at most one of these roles at a time; there is no separate storefront login
distinct from the merchant account that owns it.

## Technology stack

- ASP.NET Core MVC on **.NET 10**
- Entity Framework Core (Code First) + **SQL Server**
- ASP.NET Core Identity for authentication and role management
- Razor Views + Bootstrap 5 + vanilla JavaScript
- Cloud object storage and outbound email are behind interfaces (`IFileStorage`,
  `IEmailSender`) so a real provider can be plugged in without touching application code

## Architecture

Faed is a single ASP.NET Core MVC project (`src/Faed.Web`) rather than a layered multi-project
solution. Application code is organized by concern inside that project:

- `Controllers/` — public MVC endpoints; role-specific controllers live under `Areas/`
- `Areas/{Buyer,Merchant,Admin,Identity}/` — role-scoped controllers and views
- `Models/{Entities,Enums,Identity}/` — the EF Core domain model
- `Data/` — `ApplicationDbContext`, entity configurations, EF Core migrations, and the
  startup data seeders
- `Services/` — business logic, organized by domain area (`Ordering`, `B2B`, `Listings`,
  `Trust`, `Analytics`, `Merchants`, `Catalog`, `Storage`, …)
- `Authorization/` — named authorization policies and handlers enforcing the role rules above
- `ViewModels/`, `Rendering/` — view-facing shaping and display helpers

Concurrency on stock quantities uses SQL Server `rowversion` so two simultaneous orders
cannot oversell the same stock. Reservation and offer expiry are handled by hosted background
services rather than a request-time check.

## Database

- **SQL Server**, accessed through **Entity Framework Core** in **Code First** mode — the
  schema is generated from the C# entity classes in `Models/Entities` and
  `Data/Configurations`, not written by hand.
- Migrations live in `src/Faed.Web/Data/Migrations` and are the only way the schema changes;
  the application does not migrate the database automatically on startup.
- The conceptual entity/relationship model is documented in `docs/04-DOMAIN-MODEL.md`; there
  is no separate ERD diagram file in this repository.

## Prerequisites

- **.NET 10 SDK**
- **SQL Server** — SQL Server LocalDB (`(localdb)\MSSQLLocalDB`, installed with Visual Studio
  or the standalone SqlLocalDB installer) is enough for local development. Any reachable SQL
  Server instance, including a container, also works.
- The `dotnet-ef` tool for migrations: `dotnet tool install --global dotnet-ef`

## Local setup

```bash
# 1. restore + build
dotnet build Faed.slnx

# 2. create the database (applies every migration to an empty catalog)
dotnet ef database update --project src/Faed.Web

# 3. run
dotnet run --project src/Faed.Web
```

On startup the app idempotently seeds the fixed Identity roles (`Buyer`, `Merchant`, `Admin`)
and the catalog reference data (condition grades A–D, the eight approved discount reasons,
and the `Fashion Overstock` launch taxonomy). It does not create the database or apply
migrations itself — run step 2 whenever migrations change.

The development connection string lives only in
`src/Faed.Web/appsettings.Development.json` (a passwordless LocalDB database named `Faed`).
The committed `appsettings.json` has no connection string; any non-`Development` environment
must supply its own via `ConnectionStrings__DefaultConnection`, and the app fails fast at
startup if that environment has none, or is still pointed at the local LocalDB database. See
`DEPLOYMENT.md` for production configuration.

Override the development connection string with either:

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

Seeded only in the `Development` environment. Re-running is safe.

## Demo data

A deterministic demo data set is available for walkthroughs: an admin, two approved
merchants, one merchant pending approval, two buyers, a full product catalog across all
three launch categories with real generated product images, and one example of every
transaction scenario — active and completed B2C orders, an open B2B negotiation, a
counter-offer chain, a completed B2B deal, a dispute and a review.

It is Development-only, opt-in, and password-gated, and it is built by calling the same
application services a real user would, so nothing bypasses moderation, authorization or
stock concurrency.

```bash
# enable it and set the shared password for every demo account (never committed)
dotnet user-secrets set "Faed:DemoSeed:Enabled" "true"        --project src/Faed.Web
dotnet user-secrets set "Faed:DemoSeed:Password" "<demo-password>" --project src/Faed.Web

dotnet ef database update --project src/Faed.Web   # start from a clean database
dotnet run --project src/Faed.Web                  # seeds on first startup, idempotent
```

Demo accounts (all share the password set above):

| Email | Role |
|---|---|
| `demo-admin@faed.local` | Administrator |
| `merchant-a@faed.local` | Approved merchant — *Amman Threads* |
| `merchant-b@faed.local` | Approved merchant — *Petra Footwear* |
| `pending-merchant@faed.local` | Merchant awaiting verification |
| `buyer-a@faed.local`, `buyer-b@faed.local` | Individual buyers |

Re-running the app never duplicates the data. The password is only set when an account is
first created — if you change `Faed:DemoSeed:Password` after the accounts already exist, the
old password keeps working until you drop and recreate the database
(`dotnet ef database drop --project src/Faed.Web` then `database update`) so the accounts
are reseeded with the current secret.

## Project structure

```text
Faed.slnx
README.md
DEPLOYMENT.md
docs/                           domain model reference (docs/04-DOMAIN-MODEL.md)
src/Faed.Web/
  Areas/{Admin,Merchant,Buyer,Identity}/   role-scoped controllers and views
  Controllers/                  public MVC endpoints
  Models/{Entities,Enums,Identity}/
  ViewModels/
  Data/{ApplicationDbContext.cs,Configurations/,Migrations/,Seed/}
  Services/                     business logic, one folder per domain area
  Authorization/                policy names + handlers
  Rendering/                    view-only display helpers
  wwwroot/                      static assets (CSS, JS, images)
```

## Known scope limitations

- Payments, escrow, platform-arranged shipping/logistics and warehousing are out of scope
  for this MVP. Orders and deals model pickup and merchant-arranged delivery only; Faed does
  not process payment or book shipping.
- `/Identity/Account/Login`, `Register` and `Manage/*` use ASP.NET Core Identity's default
  page styling inside the shared Faed layout, rather than fully custom designs.
- Sending real confirmation/notification emails requires registering an `IEmailSender`; none
  is wired up by default, so Identity's account-confirmation email is a no-op locally.

