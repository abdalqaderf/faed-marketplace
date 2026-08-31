# TASK-007 — B2B Negotiation

## Objective
Implement structured merchant-to-merchant offer and counter-offer history.

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
- `B2BNegotiation`
- immutable `B2BOfferRevision`
- variant quantity lines
- MOQ validation
- offer expiry
- accept/reject/counter commands
- seller/buyer views

## Critical rules
- Old revisions are never overwritten.
- Active revision expiry blocks acceptance.
- Buying merchant cannot be the seller merchant.
- No stock is permanently consumed by negotiation alone.

## Exit criteria
A complete offer/counter-offer history is auditable and permission-safe.
