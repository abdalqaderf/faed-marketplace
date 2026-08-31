# TASK-004 — Listings, Variants, Inventory and Moderation

## Objective
Allow an approved merchant to create real Fashion Overstock listings with generic options/variants and safe inventory.

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
- Listing aggregate
- Listing options/values
- `ListingVariant`
- media and defect media
- discount reasons
- reference-price evidence metadata
- B2C/B2B flags
- MOQ
- `RowVersion`
- inventory adjustment audit
- moderation workflow
- merchant listing management
- admin moderation

## Mandatory examples
The model must represent:
- T-shirt: Black/M, Black/L, White/M
- Shoes: sizes 41, 42, 43

without separate hard-coded clothing/shoe entity designs.

## Exit criteria
- [ ] Variant combination is unique.
- [ ] Stock is variant-level.
- [ ] Quantities cannot become negative.
- [ ] Live listing material edit requires moderation.
- [ ] Public cannot see non-Live data.
- [ ] Defect media is distinguishable.
- [ ] Migration includes RowVersion from first variant creation.
