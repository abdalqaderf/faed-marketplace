# TASK-006 — B2C Orders

## Objective
Implement safe single-merchant consumer ordering with variant-level reservation.

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
- Order + OrderItems
- same-merchant cart/order builder
- server-calculated totals
- Pickup
- MerchantDelivery
- transactional stock reservation
- order status service
- cancellation/completion
- configurable reservation expiry
- buyer and merchant order views

## Mandatory tests
- forged price rejected/recomputed;
- multi-merchant order rejected;
- two buyers compete for last unit: one succeeds;
- cancellation releases;
- completion moves Reserved -> Sold;
- unauthorized order access blocked.

## Exit criteria
End-to-end B2C purchase can complete safely against SQL Server.
