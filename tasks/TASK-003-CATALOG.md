# TASK-003 — Catalog Foundations

## Objective
Create the DB-driven taxonomy and disclosure reference data required by Fashion Overstock.

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
- hierarchical `Category`
- `ConditionGrade`
- `DiscountReason`
- optional `Brand`
- idempotent seed
- basic admin management where needed

## Required seed
- Fashion Overstock
  - Clothing
  - Shoes
  - Bags & Accessories
- Grades A-D
- approved discount reasons from the PRD

## Critical rule
Condition and discount reason remain separate.

## Exit criteria
- [ ] Seed runs repeatedly without duplication.
- [ ] No category/condition business values are hard-coded into public views.
- [ ] Grade E is absent from MVP.
- [ ] Catalog unit/integration tests pass.
