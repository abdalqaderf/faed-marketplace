# TASK-005 — Public Marketplace

## Objective
Build the English, mobile-first discovery experience.

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
- Home
- Shop
- paging
- filters
- listing details
- merchant storefront
- empty/error states
- responsive UI

## Critical presentation
A listing detail must clearly show:
- verified merchant;
- condition + human-readable meaning;
- why discounted;
- defect evidence;
- reference price when valid;
- sell price;
- variant selection;
- availability;
- B2C/B2B availability;
- fulfillment/policy.

## Exit criteria
- [ ] Anonymous user can understand a listing without hidden critical information.
- [ ] Non-Live listings cannot be accessed publicly.
- [ ] Mobile layout checked.
- [ ] Accessibility baseline checked.
