# ADR 0006 — Single-Project Organized ASP.NET Core MVC

## Status
Accepted. Supersedes the multi-project structure in ADR 0001.

## Context
The foundation (TASK-001) and merchant verification (TASK-002) were built on a four-project
solution: `Faed.Domain`, `Faed.Application`, `Faed.Infrastructure`, `Faed.Web`. For an
MVP of this size the project boundaries added navigation, build and refactoring overhead
without a proportional benefit: there is one deployable, one database, one team, and no
plan to reuse the domain or application layer from a second host.

## Decision
Faed uses a **single-project organized ASP.NET Core MVC architecture**.

All production application code lives inside `src/Faed.Web`. There are no separate Domain,
Application, or Infrastructure projects.

Organize the single project by folder:

- `Models/Entities` — persisted entities
- `Models/Enums` — enums
- `Models/Identity` — `ApplicationUser` and role name constants
- `Data` — EF Core, `ApplicationDbContext`, configurations, migrations, and seed data
- `Services` — business logic (with `Services/Abstractions` for external-service interfaces)
- `Controllers` — public MVC endpoints
- `Areas/Admin`, `Areas/Merchant`, `Areas/Buyer` — role-specific functionality
- `Areas/Identity` — ASP.NET Core Identity UI
- `ViewModels` — UI/input models
- `Authorization` — policy names and authorization handlers

Rules:

- Controllers must remain thin: validate input, call a service, translate the result.
- Services may use `ApplicationDbContext` directly.
- Keep entities separate from ViewModels; never render an EF entity to Razor.
- Do not introduce Repository Pattern, UnitOfWork, CQRS, or MediatR unless a future
  requirement explicitly justifies it.
- One EF Core `DbContext`; Identity shares it; migrations live in `Data/Migrations`.

## Consequences
- Tests (`Faed.UnitTests`, `Faed.IntegrationTests`) reference `Faed.Web` directly.
- `dotnet ef` uses `--project src/Faed.Web` as both migrations and startup project.
- Layering is maintained by folder discipline and code review, not by project references.
- Migration IDs and the database schema are unchanged from the multi-project version; only
  namespaces and file locations moved (`Faed.Domain.* → Faed.Web.Models.*`,
  `Faed.Application.* → Faed.Web.Services.*`, `Faed.Infrastructure.Persistence.* →
  Faed.Web.Data.*`).
- If a future requirement genuinely needs an independently reusable or separately
  deployable component, that extraction gets its own ADR.
