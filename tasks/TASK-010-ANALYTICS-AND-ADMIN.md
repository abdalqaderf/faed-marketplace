# TASK-010 — Merchant Analytics and Admin Completion

## Objective
Complete merchant recovery analytics and consolidate admin operational screens.

## Architecture (do not deviate)

Faed is a **single-project organized ASP.NET Core MVC** application. All code for this task
goes inside `src/Faed.Web`:

- entities -> `Models/Entities`, enums -> `Models/Enums`
- EF Core configuration, migrations and seed -> `Data/` (`Configurations/`, `Migrations/`, `Seed/`)
- business logic -> `Services/` (use-case methods; may use `ApplicationDbContext` directly)
- public MVC endpoints -> `Controllers/`; role-specific screens -> `Areas/Admin`, `Areas/Merchant`, `Areas/Buyer`
- UI/input models -> `ViewModels/` (keep separate from entities)

Do not create separate Domain, Application, or Infrastructure projects. Do not introduce
Repository Pattern, UnitOfWork, CQRS, or MediatR. Keep controllers thin. See `AGENTS.md`
section 5 and `docs/adr/0006-SINGLE-PROJECT-MVC.md`.

## Merchant analytics
- recovered B2C value
- recovered B2B value
- sell-through
- units listed/sold
- average time-to-sale
- cancellation count
- active B2B negotiations
- stale listings

## Admin
- merchant queue
- listing queue
- order/deal monitoring
- dispute queue
- catalog management
- review moderation where needed
- audit log

## Exit criteria
Analytics reconcile with known seeded completed transactions and admin can operate all MVP review queues.
