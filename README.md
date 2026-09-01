# Faed — Surplus Inventory Marketplace

**Faed** is a specialized marketplace for surplus and non-perfect merchant inventory in Jordan.

A verified merchant lists inventory once and can sell from the same stock through:

- `B2C` — individual buyers purchase units.
- `B2B` — verified merchants negotiate and buy quantities/lots.

Faed is not a general classifieds platform. Its product identity is built around structured condition disclosure, verified business sellers, quantity integrity, trusted transactions, and inventory-recovery analytics.

## MVP

- Market: Amman, Jordan
- UI: English
- Currency: JOD
- Sellers: verified merchants only
- Buyers: individuals + verified merchants
- Launch sector: Fashion Overstock
- Launch categories:
  - Clothing
  - Shoes
  - Bags & Accessories

## Tech

- ASP.NET Core MVC / .NET 10 LTS
- Entity Framework Core + SQL Server
- ASP.NET Core Identity
- Razor Views + Bootstrap 5 + JavaScript
- Single-project organized MVC: all app code in `src/Faed.Web`
  (`Models`, `Data`, `Services`, `Areas`, `ViewModels`) — see `docs/adr/0006-SINGLE-PROJECT-MVC.md`
- SQL Server `rowversion` for stock concurrency

## Local development

Prerequisites: .NET 10 SDK and SQL Server LocalDB (`(localdb)\MSSQLLocalDB`, installed with
Visual Studio or the standalone SqlLocalDB installer).

```bash
# restore + build the whole solution
dotnet build Faed.slnx

# create / update the local database
# (migrations live in src/Faed.Web/Data/Migrations; the connection string is resolved
#  from appsettings + user secrets + environment variables)
dotnet ef database update --project src/Faed.Web

# run the web app
dotnet run --project src/Faed.Web

# run all tests (unit + SQL Server integration)
dotnet test Faed.slnx
```

The development connection string in `src/Faed.Web/appsettings.json` targets a local
LocalDB database named `Faed` and contains no secrets. Override it with user secrets
(`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>"` in
`src/Faed.Web`) or the `ConnectionStrings__DefaultConnection` environment variable — both
the app and the `dotnet ef` command above honour the override because they share the
`Faed.Web` configuration. The Identity roles (`Buyer`, `Merchant`, `Admin`) are seeded
automatically and idempotently on startup.

An optional confirmed development admin can also be seeded from user secrets. It is created
only while the app runs in the `Development` environment:

```bash
dotnet user-secrets set "Faed:AdminSeed:Email" "admin@faed.local" --project src/Faed.Web
dotnet user-secrets set "Faed:AdminSeed:Password" "<development-password>" --project src/Faed.Web
```

The password stays outside the repository. Re-running the app is safe: an existing account
is reused and assigned the `Admin` role if needed.

The SQL Server integration tests create, use and drop only the explicitly allow-listed
databases `Faed_IntegrationTests` and `Faed_WebTests`. They take the server and credentials
from a separate `Faed_TEST_CONNECTION` environment variable (default: LocalDB), replace any
configured catalog with one of those two fixed test catalogs, and never write to the
application database — `TestHostDatabaseTargetTests` asserts that the hosted application's
`DbContext` really does target `Faed_WebTests`.

These integration tests need a reachable SQL Server. When none is reachable and none was
configured they **skip** on a developer workstation (the unit tests still run and
`dotnet test` is green with a lower executed count). They **fail** when `Faed_TEST_CONNECTION`
is set but unreachable, and on CI — the runner is detected by `CI=true` — so a green pipeline
always means the SQL Server exit criteria actually executed. To run them where LocalDB is
unavailable, point `Faed_TEST_CONNECTION` at any SQL Server instance (a container is fine):

```bash
export Faed_TEST_CONNECTION="Server=localhost,1433;User Id=sa;Password=<pw>;TrustServerCertificate=true"
dotnet test Faed.slnx
```

CI runs restore, build, unit tests and integration tests against a SQL Server service
container on every push and pull request (`.github/workflows/ci.yml`,
`docs/09-TEST-STRATEGY.md` §6).

## Read before coding

1. `AGENTS.md` — engineering contract and precedence.
2. `docs/00-SPEC-MAP.md` — map of every specification file.
3. All files under `/docs` in numeric order.
4. `tasks/TASK-001-FOUNDATION.md` — first executable task.

## Start

Give your coding agent the contents of `START_PROMPT.md`, or simply tell it:

> Read `AGENTS.md` and execute `tasks/TASK-001-FOUNDATION.md`.

The full implementation task queue (`TASK-001` through `TASK-011`) is included under `/tasks`.

The `/reference` directory is historical context only.


## Claude skills

This repository includes project-specific Claude skills under:

```text
.claude/skills/
```

Use them together with any relevant built-in/workspace Claude skills available in your account.
See:
- `docs/21-CLAUDE-SKILLS-USAGE.md`

## Visual Studio-first foundation workflow

The initial `Faed.Web` project is intentionally created manually in Visual Studio using:

- ASP.NET Core MVC
- .NET 10
- Individual Accounts / Identity
- HTTPS

Then the coding agent executes TASK-001.

TASK-001 does **not** recreate the Web project. It begins with a mandatory baseline audit and adopts the generated project.

See:
- `docs/22-VISUAL-STUDIO-BASELINE.md`
- `docs/23-GITHUB-REPOSITORY-POLICY.md`
- `tasks/TASK-001-FOUNDATION.md`
