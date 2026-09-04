# Faed — Surplus Inventory Marketplace

**Faed** is a specialized marketplace for verified merchants in Jordan to recover value from
surplus and non-perfect inventory by selling the same stock to individual buyers (`B2C`) or
to other verified merchants (`B2B`) through structured, trusted workflows.

Faed is **not** a general classifieds platform. Its product identity is built around
structured condition disclosure, verified business sellers, quantity integrity, trusted
transactions and inventory-recovery analytics.

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

## Tech

- ASP.NET Core MVC on **.NET 10 LTS**
- Entity Framework Core + **SQL Server** (SQL Server `rowversion` for stock concurrency)
- ASP.NET Core Identity (roles: Buyer, Merchant, Admin)
- Razor Views + Bootstrap 5 + vanilla JavaScript
- **Single-project organized MVC** — all application code lives in `src/Faed.Web`
  (`Models`, `Data`, `Services`, `Areas`, `ViewModels`); see
  `docs/adr/0006-SINGLE-PROJECT-MVC.md`
- Cloud object storage and email are behind interfaces (`IFileStorage`, `IEmailSender`)

```text
Faed.slnx
src/Faed.Web/
  Areas/{Admin,Merchant,Buyer,Identity}/
  Controllers/                 public MVC endpoints
  Models/{Entities,Enums,Identity}/
  ViewModels/
  Data/{ApplicationDbContext.cs,Configurations/,Migrations/,Seed/}
  Services/                    business logic (may use ApplicationDbContext directly)
  Authorization/               policy names + handlers
  Rendering/                   view-only display helpers
tests/
  Faed.UnitTests/              references Faed.Web
  Faed.IntegrationTests/       references Faed.Web; needs SQL Server
```

---

## Prerequisites

- **.NET 10 SDK**
- **SQL Server** — SQL Server LocalDB (`(localdb)\MSSQLLocalDB`, installed with Visual
  Studio or the standalone SqlLocalDB installer) is enough for local development. Any
  reachable SQL Server instance (including a container) also works.
- `dotnet-ef` for migrations: `dotnet tool install --global dotnet-ef`

---

## Run from a clean environment

```bash
# 1. restore + build
dotnet build Faed.slnx

# 2. create the database from scratch (applies every migration to an empty catalog)
dotnet ef database update --project src/Faed.Web

# 3. run
dotnet run --project src/Faed.Web
```

On startup the app **idempotently** seeds the fixed Identity roles (`Buyer`, `Merchant`,
`Admin`) and the catalog reference data (condition grades A–D, the eight approved discount
reasons, and the `Fashion Overstock` launch taxonomy). It does **not** apply migrations,
create the database, or drop anything on startup — run step 2 whenever migrations change.
Migrations apply cleanly to a completely empty catalog (step 2 is exactly that path, and
the integration-test host re-proves it on every run by dropping and re-migrating its
databases).

The development connection string lives **only** in
`src/Faed.Web/appsettings.Development.json` (a passwordless LocalDB database named `Faed`).
The committed `appsettings.json` has **no** connection string. Every non-`Development`
environment must supply its own via `ConnectionStrings__DefaultConnection`; the app
**fails fast at startup** if a Production/Staging environment has none, or is still pointed
at the local LocalDB database (`DependencyInjection.ResolveDatabaseConnectionString`,
`DEPLOYMENT.md` §2). A non-Development environment also requires a real private
`IFileStorage` — `LocalFileStorage` is Development-only.

Override the development connection string with either:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>" --project src/Faed.Web
# or
export ConnectionStrings__DefaultConnection="<value>"
```

Both the app and `dotnet ef` honour the override because they share the `Faed.Web`
configuration. `dotnet ef` runs in the `Development` environment by default, so with no
override it uses the `appsettings.Development.json` database.

### Optional: a development administrator

```bash
dotnet user-secrets set "Faed:AdminSeed:Email" "admin@faed.local" --project src/Faed.Web
dotnet user-secrets set "Faed:AdminSeed:Password" "<development-password>" --project src/Faed.Web
```

Seeded only in the `Development` environment. The password stays outside the repository
(`docs/08-SECURITY-AND-PRIVACY.md` §12). Re-running is safe.

---

## Demo / field-validation data set

A deterministic demo data set (two approved merchants, one pending merchant, two buyers,
four listings including a sold-out one, and one of every transaction scenario — active and
completed B2C orders, an open B2B negotiation, a counter-offer chain, a completed B2B deal,
a dispute and a review) is available for demonstrations and field validation.

It is **Development-only**, **opt-in**, and **password-gated**, and it builds every record by
calling the same application services a real user would — nothing bypasses moderation,
authorization, price integrity or stock concurrency (`docs/12-SEED-DATA.md`,
`docs/24-DELIVERY-AND-HARDENING.md` §10).

```bash
# enable it and set the shared password for every demo account (never committed)
dotnet user-secrets set "Faed:DemoSeed:Enabled" "true"        --project src/Faed.Web
dotnet user-secrets set "Faed:DemoSeed:Password" "<demo-password>" --project src/Faed.Web

dotnet ef database update --project src/Faed.Web   # start from a clean database
dotnet run --project src/Faed.Web                  # seeds on first startup, idempotent
```

Demo accounts (all share the password above):

| Email | Role |
|---|---|
| `demo-admin@faed.local` | Administrator |
| `merchant-a@faed.local` | Approved merchant — *Amman Threads* |
| `merchant-b@faed.local` | Approved merchant — *Petra Footwear* |
| `pending-merchant@faed.local` | Merchant awaiting verification |
| `buyer-a@faed.local`, `buyer-b@faed.local` | Individual buyers |

Re-running the app never duplicates the data. If a previous seed was interrupted, the next
start detects the partial data, removes it, and rebuilds the set — so a restart is enough
to recover. For a clean rebuild from nothing, drop and re-create the database
(`dotnet ef database drop --project src/Faed.Web` then `database update`).

---

## Tests

```bash
dotnet test Faed.slnx
```

- **Unit tests** run everywhere.
- **Integration tests** need a reachable SQL Server (never InMemory or SQLite — SQL Server
  `rowversion` concurrency is tested against SQL Server, `docs/09-TEST-STRATEGY.md` §2). They
  take the server from a separate `Faed_TEST_CONNECTION` variable (default: LocalDB) and
  **destructively manage** — create, `EnsureDeleted` + migrate at the start of each run, and
  drop on dispose — only these three explicitly allow-listed catalogs (the application's own
  database is never touched, and `TestSqlServer.AssertSafeTestDatabase` re-checks the target
  immediately before every destructive operation):
    - `Faed_IntegrationTests` — the persistence / EF-mapping tests
    - `Faed_WebTests` — the hosted-app (`WebApplicationFactory`) tests
    - `Faed_DemoSeedTests` — the demo-seed end-to-end test, isolated from the shared web
      catalog
  They **skip** on a workstation with no SQL Server and no `Faed_TEST_CONNECTION`; they
  **fail** when `Faed_TEST_CONNECTION` is set but unreachable, and on CI (`CI=true`), so a
  green pipeline always means the SQL Server exit criteria actually executed.

```bash
# run the integration tests against any SQL Server (e.g. a container)
export Faed_TEST_CONNECTION="Server=localhost,1433;User Id=sa;Password=<pw>;TrustServerCertificate=true"
dotnet test Faed.slnx
```

CI runs restore, build, unit tests and integration tests against a SQL Server service
container on every push and pull request (`.github/workflows/ci.yml`).

Latest local run: **456 passed** (270 unit + 186 SQL Server integration), 0 failed, 0 skipped.

---

## Deployment

Production configuration, the required manual delivery steps (cloud object storage, email
provider) and the deployment checklist are in **[`DEPLOYMENT.md`](DEPLOYMENT.md)**.

The TASK-011 hardening pass (authorization, validation, upload, concurrency, responsive,
accessibility and paging audits) and the acceptance-criteria verification are in
**[`docs/24-DELIVERY-AND-HARDENING.md`](docs/24-DELIVERY-AND-HARDENING.md)**.

---

## Specification

Faed is spec-driven. Read in this order before changing anything:

1. `AGENTS.md` — engineering contract and source-of-truth precedence
2. `docs/00-SPEC-MAP.md` — map of every specification file
3. All files under `/docs` in numeric order
4. `PROJECT_STATUS.md` — current implementation state and task history

The `/reference` directory (if present) is historical context only and is not authoritative.
