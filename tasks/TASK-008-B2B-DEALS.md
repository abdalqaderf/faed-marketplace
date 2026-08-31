# TASK-008 — B2B Deal and Fulfillment

## Objective
Turn an accepted B2B offer revision into an atomic stock reservation and fulfillment deal.

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
- `B2BDeal`
- deal lines
- accepted-term snapshots
- reservation expiry
- Pickup / SellerArrangedShipping
- shipment reference
- fulfillment states
- expiry/release job
- completion/cancellation

## Mandatory tests
- all requested variants reserve atomically or none do;
- B2C vs B2B competition is safe;
- two B2B accept attempts cannot oversell;
- repeated expiry processing does not double-release;
- completion moves Reserved -> Sold.

## Exit criteria
End-to-end merchant-to-merchant deal works safely against SQL Server.
