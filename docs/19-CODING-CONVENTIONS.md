# 19 — .NET Coding Conventions

These conventions exist to keep agent-generated code consistent.

## Project organization

Faed uses a single-project organized ASP.NET Core MVC architecture. All production
application code lives inside `src/Faed.Web` (see `docs/adr/0006-SINGLE-PROJECT-MVC.md`).
Do not create separate Domain, Application, or Infrastructure projects.

- `Models/Entities` — persisted entities. `Models/Enums` — enums. `Models/Identity` —
  `ApplicationUser` and role name constants.
- `Data` — EF Core: `ApplicationDbContext`, `Configurations/`, `Migrations/`, `Seed/`.
- `Services` — business logic; use-case-oriented methods; may use `ApplicationDbContext`
  directly. `Services/Abstractions` holds interfaces for external services.
- `Controllers` — public MVC endpoints. `Areas/Admin`, `Areas/Merchant`, `Areas/Buyer` —
  role-specific functionality.
- `ViewModels` (and per-area `ViewModels/` folders) — UI/input models. Keep entities
  separate from ViewModels.
- `Authorization` — policy name constants and authorization handlers.

Namespaces mirror the folder path (`Faed.Web.Models.Entities`, `Faed.Web.Services.Merchants`,
`Faed.Web.Data`, …).

Do not introduce Repository Pattern, UnitOfWork, CQRS, or MediatR unless a future
requirement explicitly justifies it.

## C#

- File-scoped namespaces.
- Nullable enabled.
- Prefer explicit domain names over abbreviations.
- `Async` suffix for asynchronous methods.
- CancellationToken on application/infrastructure async operations where meaningful.
- Date/time properties end with `Utc` when stored as UTC.
- Money names state purpose: `RetailPrice`, `Subtotal`, `DeliveryFeeSnapshot`.
- IDs use `Id` suffix.
- Enums are singular.

## Controllers

Controllers:
- validate HTTP/input concerns;
- call application service;
- translate result to View/Redirect/Error.

Controllers do not:
- contain pricing rules;
- mutate stock directly;
- query arbitrary DbSets for business decisions;
- assign authorization-sensitive IDs from form posts.

## ViewModels

Use separate:
- Input models for POST;
- Display models for views.

Never render EF entity directly from controller to Razor.

## Services

Prefer use-case-oriented service methods.

Examples:
- `SubmitMerchantApplicationAsync`
- `SubmitListingForReviewAsync`
- `PlaceOrderAsync`
- `AcceptB2BOfferAsync`

Avoid generic service classes with dozens of unrelated methods.

## EF Core

- Configure entity mappings with `IEntityTypeConfiguration<T>`.
- Explicitly configure decimal precision.
- Explicitly configure important delete behaviors.
- Add check/unique constraints where domain invariants benefit.
- Use `AsNoTracking()` for read-only queries.
- Avoid lazy loading.
- Use projections for list pages.

## Enums vs tables

Use enums for stable workflow states:
- order status;
- negotiation status;
- deal status;
- verification status;
- fulfillment type.

Use DB reference tables for admin-manageable product concepts:
- category;
- condition grade;
- discount reason;
- brand if controlled.

## Exceptions/results

Do not use exceptions for routine validation.

Use a consistent application result pattern for:
- validation/business conflict;
- not found;
- forbidden;
- concurrency.

Translate concurrency into a clear customer-facing error.

## Razor

- Partial views/components for repeated display patterns.
- Tag Helpers.
- No business logic in Razor.
- Keep accessibility labels and validation messages.

## JavaScript

Use vanilla JS only for progressive interaction:
- dependent options;
- image previews;
- confirmation dialogs;
- filter UX.

The server remains authoritative.

## Comments

Comment why, not what.

Use ADR/spec references when a rule is non-obvious:
> Inventory is variant-level; see ADR 0002.

## Naming

Canonical project namespace: `Faed`.

Do not use any legacy transliteration/working-name variants in new code.
