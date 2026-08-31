# TASK-011 — Hardening, Demo Data and Delivery

## Objective
Prepare the MVP for real field validation and academic/portfolio demonstration.

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

## Deliverables
- full authorization audit
- validation audit
- upload security review
- concurrency regression
- responsive QA
- accessibility pass
- performance/paging review
- production configuration documentation
- deterministic demo seed
- final README
- clean database setup instructions
- deployment checklist

## Exit criteria
All checks in `docs/11-ACCEPTANCE-CRITERIA.md` are completed or explicitly documented as deferred with product-owner approval.
