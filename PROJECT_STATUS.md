# Project Status

## Current state

**Phase 0 — Foundation complete (TASK-001).**

The Visual Studio-generated `Faed.Web` baseline was audited and adopted. The clean modular
monolith solution structure has been completed around it.

### Phase 0 baseline audit — result: `PASS`

The Visual Studio baseline (commit `afe6003`, "chore: create MVC Identity baseline") was
correct and adoptable with no blocking issues. The structural changes made afterwards are
the expected TASK-001 Phase 2–3 foundation work, not corrections to a defective baseline.

| Audit item | Finding |
|---|---|
| Project template | ASP.NET Core Web App (Model-View-Controller), `Microsoft.NET.Sdk.Web` |
| Target framework | `net10.0`, nullable + implicit usings enabled |
| Authentication / Identity | Individual Accounts — `AddDefaultIdentity<IdentityUser>` + `Microsoft.AspNetCore.Identity.UI`; `Areas/Identity` present; Register/Login reachable |
| Generated database provider | **SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.11), LocalDB connection string — no SQLite, so no provider migration was needed |
| Solution structure | Repository root = solution root; `src/Faed.Web/`; no nested `Faed/Faed/` |
| Template migration | Legacy `00000000000000_CreateIdentitySchema` in `Faed.Web/Data/Migrations` |
| Build before restructuring | `dotnet build Faed.slnx` — succeeded, 0 warnings, 0 errors |
| Run before restructuring | App started; Home, `/Identity/Account/Register`, `/Identity/Account/Login` all HTTP 200; Identity schema applied to LocalDB |
| Git safety | `bin/`, `obj/`, `.vs/`, `*.user` correctly ignored; no secrets tracked |
| Blocking baseline issues | **None** |

### Post-audit foundation work (TASK-001 Phases 2–5)

EF/Identity moved from `Faed.Web` into `Faed.Infrastructure`; user type extended to
`ApplicationUser` (`CreatedAtUtc`, `IsActive`); roles enabled and seeded idempotently;
template migration regenerated in Infrastructure as `InitialIdentity`; `IClock` added;
unit + SQL Server integration test projects added. No product/marketplace features were
implemented.

## Active task

None. TASK-001 is closed.

Next: `tasks/TASK-002-MERCHANT-VERIFICATION.md` (do not start until explicitly requested).

## Solution structure

```text
Faed.slnx
src/
├── Faed.Domain/          # FaedRoles (role name constants)
├── Faed.Application/     # Abstractions/IClock
├── Faed.Infrastructure/  # ApplicationDbContext, ApplicationUser, Identity role seeder,
│                         # SystemClock, EF migrations, DI composition
└── Faed.Web/             # MVC + Identity UI (adopted Visual Studio baseline)
tests/
├── Faed.UnitTests/         # foundation smoke tests
└── Faed.IntegrationTests/  # SQL Server persistence / migration test
```

Dependencies: Domain ← Application ← Infrastructure; Web → Application + Infrastructure.

## Migrations

- `20260831174908_InitialIdentity` (Faed.Infrastructure) — ASP.NET Core Identity schema
  for `ApplicationUser` (adds `CreatedAtUtc` default `SYSUTCDATETIME()`, `IsActive` default
  `true`). Replaces the Visual Studio template's `00000000000000_CreateIdentitySchema`.
  Scaffolded from the `Faed.Web` host model, so `dotnet ef` and the running app resolve
  the same connection string and the same model (`dotnet ef migrations
  has-pending-model-changes` reports clean).

## Persistence

- One application `DbContext` (`ApplicationDbContext`) in `Faed.Infrastructure`, shared
  with Identity.
- SQL Server; local development uses LocalDB database `Faed` (non-secret connection
  string in `appsettings.json`).
- EF Core `rowversion` concurrency is introduced with the inventory model in a later task.

## Identity

- Individual Accounts (ASP.NET Core Identity) preserved from the Visual Studio baseline.
- `AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>()`.
- Roles `Buyer`, `Merchant`, `Admin` seeded idempotently at startup.
- Merchant verification remains a separate future domain state (not an Identity role).

## Locked product choices

- English MVP website
- Amman
- Fashion Overstock launch
- Clothing / Shoes / Bags & Accessories
- Verified merchants only as sellers
- B2C + B2B
- no real online payment
- no platform shipping
- no warehouse/fleet
- no used goods
- no Grade E

## Validation (TASK-001)

- `dotnet build Faed.slnx` — succeeds, 0 warnings.
- `dotnet test Faed.slnx` — 3 passed (2 unit, 1 SQL Server integration against LocalDB
  `Faed_IntegrationTests`, which the test creates and drops via its own
  `Faed_TEST_CONNECTION` variable — never the app connection string).
- `dotnet ef database update` — `InitialIdentity` applies from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs; Home renders; Register/Login reachable; registration creates a user with
  `CreatedAtUtc`/`IsActive` populated; role seeding is idempotent across restarts.
