# TASK-009 — Disputes and Reviews

## Objective
Implement post-transaction trust controls.

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
- dispute + evidence
- admin dispute workflow
- review
- review eligibility
- duplicate prevention
- audit logging

## Exit criteria
- [ ] Only participants can dispute.
- [ ] Review requires Completed transaction.
- [ ] Duplicate review is blocked.
- [ ] Admin resolution is audited.
- [ ] Public/private evidence permissions are correct.
