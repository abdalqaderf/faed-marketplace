# 06 — Architecture

## 1. Style

Use a **single-project organized ASP.NET Core MVC** application with a service layer,
EF Core, Areas and ViewModels.

Faed uses a single-project organized ASP.NET Core MVC architecture. All production
application code lives inside `src/Faed.Web`. Do not create separate Domain, Application,
or Infrastructure projects (see `docs/adr/0006-SINGLE-PROJECT-MVC.md`).

Why:
- one deployable application is appropriate for MVP;
- one project keeps navigation, refactoring and build simple;
- folder boundaries (`Models`, `Data`, `Services`, `Areas`, `ViewModels`) keep the code
  organized without cross-project ceremony;
- portfolio/academic quality remains high;
- avoids microservice and multi-project complexity.

---

## 2. Solution structure

```text
Faed.slnx

src/
└── Faed.Web/
    ├── Areas/
    │   ├── Admin/          # admin-only screens (Controllers/, ViewModels/, Views/)
    │   ├── Merchant/       # merchant-only screens
    │   ├── Buyer/          # buyer-only screens
    │   └── Identity/       # ASP.NET Core Identity UI
    ├── Controllers/        # public MVC endpoints
    ├── Models/
    │   ├── Entities/       # persisted entities
    │   ├── Enums/          # workflow/state enums
    │   └── Identity/       # ApplicationUser, role name constants
    ├── ViewModels/         # UI/input models (never EF entities)
    ├── Data/
    │   ├── ApplicationDbContext.cs
    │   ├── Configurations/ # IEntityTypeConfiguration<T>
    │   ├── Migrations/     # EF Core migrations
    │   └── Seed/           # idempotent seed logic
    ├── Services/           # business logic; may use ApplicationDbContext directly
    │   ├── Abstractions/   # IFileStorage, IClock, IUserRoleService, IApplicationDbContext
    │   └── Storage/        # IFileStorage implementations
    ├── Authorization/      # policy names, authorization handlers
    ├── Rendering/          # view-only display helpers
    ├── Views/
    ├── wwwroot/
    ├── DependencyInjection.cs   # composition root helpers
    └── Program.cs

tests/
├── Faed.UnitTests/         # references Faed.Web
└── Faed.IntegrationTests/  # references Faed.Web
```

Use Areas to keep role-specific MVC screens organized. Role-specific functionality belongs
in `Areas/Admin`, `Areas/Merchant`, and `Areas/Buyer`; public endpoints stay in
`Controllers`.

---

## 3. Layering rules

There are no project references to enforce layering; keep the separation by folder and by
discipline instead.

- `Models/Entities` and `Models/Enums`: persisted state and workflow enums. No EF
  attributes needed beyond what a plain POCO requires; mapping lives in
  `Data/Configurations`.
- `Data`: EF Core, `ApplicationDbContext`, entity configurations, migrations and seed
  logic. Identity shares this `DbContext`.
- `Services`: use-case-oriented business logic. Services may use `ApplicationDbContext`
  directly (or the `IApplicationDbContext` seam) and depend on abstractions such as
  `IFileStorage` / `IClock`.
- `Controllers` / Areas: HTTP concerns only — validate input, call a service, translate the
  result to a View/Redirect/Error. Keep controllers thin.
- `ViewModels`: input models for POST and display models for views. Never render an EF
  entity directly to Razor.
- `Program.cs` / `DependencyInjection.cs`: composition root and HTTP pipeline.

---

## 4. Do not add generic repository pattern

EF Core already provides unit-of-work/repository-like behavior.

Do not create:
- `IGenericRepository<T>`;
- repository methods mirroring every DbSet action.

Prefer purposeful application services/queries.

---

## 5. Persistence

One application `DbContext` (`ApplicationDbContext`) in `src/Faed.Web/Data`.

Identity shares the same `DbContext`.

Migrations live in `src/Faed.Web/Data/Migrations`. Run `dotnet ef` with
`--project src/Faed.Web` (it is both the migrations project and the startup project).

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

Define interfaces in `Services/Abstractions`, implement them in `Services` (for example
`Services/Storage`):

- `IFileStorage`
- `IEmailSender`
- `IClock` (recommended for testable expiry logic)

Do not couple entities or services to a specific cloud storage provider.

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
