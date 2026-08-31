# 06 — Architecture

## 1. Style

Use a **pragmatic clean modular monolith**.

Why:
- one deployable application is appropriate for MVP;
- clear domain boundaries improve maintainability;
- portfolio/academic quality remains high;
- avoids microservice complexity.

---

## 2. Solution structure

```text
Faed.sln

src/
├── Faed.Domain/
│   ├── Entities/
│   ├── Enums/
│   ├── ValueObjects/
│   └── Exceptions/
│
├── Faed.Application/
│   ├── Abstractions/
│   ├── Merchants/
│   ├── Catalog/
│   ├── Listings/
│   ├── Inventory/
│   ├── Orders/
│   ├── B2B/
│   ├── Reviews/
│   ├── Disputes/
│   └── Analytics/
│
├── Faed.Infrastructure/
│   ├── Persistence/
│   ├── Identity/
│   ├── Storage/
│   ├── Email/
│   ├── BackgroundJobs/
│   └── DependencyInjection.cs
│
└── Faed.Web/
    ├── Controllers/
    ├── Areas/
    │   ├── Buyer/
    │   ├── Merchant/
    │   └── Admin/
    ├── ViewModels/
    ├── Views/
    ├── wwwroot/
    └── Program.cs

tests/
├── Faed.UnitTests/
└── Faed.IntegrationTests/
```

Use Areas to keep role-specific MVC screens organized.

---

## 3. Dependency rules

`Domain`:
- no EF;
- no ASP.NET;
- no Infrastructure dependency.

`Application`:
- use cases/business orchestration;
- depends on Domain;
- defines interfaces for external services.

`Infrastructure`:
- EF Core;
- Identity;
- SQL Server;
- file storage;
- email;
- hosted/background services.

`Web`:
- MVC controllers;
- ViewModels;
- Razor views;
- HTTP concerns;
- authentication wiring;
- dependency injection composition.

---

## 4. Do not add generic repository pattern

EF Core already provides unit-of-work/repository-like behavior.

Do not create:
- `IGenericRepository<T>`;
- repository methods mirroring every DbSet action.

Prefer purposeful application services/queries.

---

## 5. Persistence

One application DbContext in Infrastructure.

Identity may share the same DbContext if practical.

Migrations live in Infrastructure.

Schema changes:
- model + migration in same change;
- migration reviewed;
- no manual production schema drift.

---

## 6. Transactions

Explicit transaction boundaries are required for:
- B2C order creation + inventory reservation;
- B2C cancellation/release;
- B2C completion;
- B2B accept + reservation + deal creation;
- B2B cancellation/expiry release;
- B2B completion.

---

## 7. Background expiration

For MVP use an ASP.NET Core hosted/background service or an explicit expiration service invoked on schedule/startup.

It should:
- find expired active reservations/deals/orders;
- release stock idempotently;
- avoid double release;
- log failures.

Do not add distributed job infrastructure unless deployment requirements demand it.

---

## 8. External services

Define interfaces in Application, implement in Infrastructure:

- `IFileStorage`
- `IEmailSender`
- `IClock` (recommended for testable expiry logic)

Do not couple domain code to a specific cloud storage provider.

---

## 9. Error strategy

Use predictable application errors:
- validation error;
- forbidden;
- not found;
- concurrency conflict;
- business-rule conflict.

For stock conflict, return a user-friendly message such as:
> Some items are no longer available in the requested quantity. Review your cart and try again.

Do not expose raw DB exceptions.

---

## 10. Observability

Use structured logs.

Important events:
- merchant verification decision;
- listing moderation;
- stock concurrency conflict;
- stock adjustment;
- order cancellation/completion;
- B2B acceptance/expiry;
- dispute resolution;
- security-sensitive admin action.

Do not log private document contents or secrets.

---

## 11. Configuration

Environment-specific settings:
- DB connection;
- storage;
- email;
- base URL;
- reservation durations;
- upload limits.

Use:
- `appsettings.json`;
- `appsettings.Development.json`;
- user secrets/environment variables for secrets.

No credentials in Git.

---

## 12. Slugs and public identifiers

Use human-readable slugs for:
- categories;
- listings;
- merchant storefronts.

Do not use slugs as authorization identifiers.

Server authorization always relies on actual database ownership/IDs.

---

## 13. Performance philosophy

MVP optimizations:
- async DB calls;
- paging;
- indexed browse queries;
- select only needed fields for list pages;
- avoid N+1;
- image thumbnails.

Do not add caches/search engines before measuring a real bottleneck.
