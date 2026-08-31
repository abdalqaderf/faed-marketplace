# Project Status

## Current state

**Architecture restructure complete — single-project organized MVC.**

The former four-project solution (`Faed.Domain` + `Faed.Application` + `Faed.Infrastructure`
+ `Faed.Web`) was consolidated into a single `src/Faed.Web` project organized by folder
(`Models/{Entities,Enums,Identity}`, `Data/{Configurations,Migrations,Seed}`, `Services`,
`Areas`, `ViewModels`, `Authorization`). No behavior, schema, migration IDs or tests
changed — only namespaces and file locations moved (`Faed.Domain.* → Faed.Web.Models.*`,
`Faed.Application.* → Faed.Web.Services.*`, `Faed.Infrastructure.Persistence.* →
Faed.Web.Data.*`). See `docs/adr/0006-SINGLE-PROJECT-MVC.md`. `Faed.Domain`,
`Faed.Application` and `Faed.Infrastructure` were removed from the solution and repository.

**Phase 1 — Roles and Merchant Verification complete (TASK-002).**

Merchant application, private verification document handling, admin approval/rejection/
suspension, admin audit logging, and the `ApprovedMerchant` / `AdminOnly` authorization
policies are implemented on top of the TASK-001 foundation.

**Phase 0 — Foundation complete (TASK-001).**

The Visual Studio-generated `Faed.Web` baseline was audited and adopted, then the solution
foundation was completed around it.

### Phase 0 baseline audit — result: `PASS`

The Visual Studio baseline (commit `afe6003`, "chore: create MVC Identity baseline") was
correct and adoptable with no blocking issues. The structural changes made afterwards are
the expected TASK-001 Phase 2–3 foundation work, not corrections to a defective baseline.

| Audit item | Finding |
|---|---|
| Project template | ASP.NET Core Web App (Model-View-Controller), `Microsoft.NET.Sdk.Web` |
| Target framework | `net10.0`, nullable + implicit usings enabled |
| Authentication / Identity | Individual Accounts — `AddDefaultIdentity<IdentityUser>` + `Microsoft.AspNetCore.Identity.UI`; `Areas/Identity` present; Register/Login reachable |
| Generated database provider | **SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.11), LocalDB connection string — no SQLite, so no provider migration was needed |
| Solution structure | Repository root = solution root; `src/Faed.Web/`; no nested `Faed/Faed/` |
| Template migration | Legacy `00000000000000_CreateIdentitySchema` in `Faed.Web/Data/Migrations` |
| Build before restructuring | `dotnet build Faed.slnx` — succeeded, 0 warnings, 0 errors |
| Run before restructuring | App started; Home, `/Identity/Account/Register`, `/Identity/Account/Login` all HTTP 200; Identity schema applied to LocalDB |
| Git safety | `bin/`, `obj/`, `.vs/`, `*.user` correctly ignored; no secrets tracked |
| Blocking baseline issues | **None** |

### Post-audit foundation work (TASK-001 Phases 2–5)

> History note: TASK-001/002 used a four-project split; that structure was later
> consolidated into the single `src/Faed.Web` project (see "Current state" and
> `docs/adr/0006-SINGLE-PROJECT-MVC.md`). Paths below reflect the original layout.

EF/Identity moved out of the generated `Faed.Web` root into a persistence layer; user type
extended to `ApplicationUser` (`CreatedAtUtc`, `IsActive`); roles enabled and seeded
idempotently; template migration regenerated as `InitialIdentity`; `IClock` added;
unit + SQL Server integration test projects added. No product/marketplace features were
implemented.

## Active task

None. TASK-002 is closed.

Next: `tasks/TASK-003-CATALOG.md` (do not start until explicitly requested).

## TASK-002 — Merchant Verification

### Behaviour implemented

- `MerchantProfile` aggregate (1:1 with the Identity user) with the verification state
  machine `Draft → PendingReview → Approved / Rejected / Suspended` and
  `Suspended → Approved` (reinstate). A user can never self-assign `Approved`; every
  decision is a guarded domain transition that records the reviewing admin, timestamp and
  reason.
- `MerchantVerificationDocument` — private evidence files. Only a storage object key +
  metadata are stored; there is no public URL. Removing a document soft-deactivates it so
  history is retained (safe reversible default for open question item 3).
- `AdminActionLog` — append-only audit of merchant approve/reject/suspend/reinstate and
  every verification-document access.
- `IFileStorage` abstraction + `LocalFileStorage` development implementation: writes to a
  private directory outside `wwwroot` (`{ContentRoot}/App_Data/private-storage` by
  default), generates the object key server-side, validates keys on read against path
  traversal.
- Service layer: `IMerchantVerificationService` with use-case methods, a `Result`
  pattern (Validation / NotFound / Forbidden / Conflict), server-side upload validation
  (size, content type, extension), and `IApplicationDbContext` as the persistence seam
  (not a generic repository).
- Authorization policies `ApprovedMerchant` (DB-checked verification state, not a role)
  and `AdminOnly`, plus policy name constants (`Authorization/FaedPolicies`) and role name
  constants (`Models/Identity/FaedRoles`).
- Merchant self-service UI (`/Merchant/Verification`) and admin queue/detail/decision UI
  (`/Admin/MerchantVerification`) using a new Faed design-token CSS layer.
- The `Merchant` role is granted on approval for nav/UI convenience; selling capability
  is always governed by verification state.

### Open questions handled with reversible defaults

- Accepted Jordanian document types (item 1): flexible `MerchantVerificationDocumentType`
  enum — `CommercialRegistration`, `TaxRegistration`, `Other`. No personal/national
  identity document is offered (docs/08-SECURITY-AND-PRIVACY.md §14).
- Email confirmation before application (item 2): none added beyond the existing
  `RequireConfirmedAccount`; any authenticated user may start an application.
- Rejected-document retention (item 3): documents are retained (soft-deactivated), never
  auto-deleted.

### Post-review hardening (TASK-002 code review)

- Uploaded document contents are now validated by byte signature (real `%PDF-`, JPEG,
  PNG magic bytes), the content type must pair with the file extension, and admin
  downloads are served as attachments (`Content-Disposition: attachment`, `nosniff`),
  never inline.
- `MerchantProfile` carries a SQL Server `rowversion` concurrency token; competing admin
  decisions surface as a `Conflict` result instead of a silent last-write-wins.
- Rejection/suspension reason length is validated (≤ `MerchantProfile.MaxDecisionReasonLength`
  = 1000) before persistence in both the domain and the application service.
- `LocalFileStorage` is registered only outside Production (Production resolves a
  fail-fast `IFileStorage`); the configured storage root is rejected if it resolves
  inside the web root.
- Undefined document-type enum values are rejected (`Enum.IsDefined` in the validator,
  `[EnumDataType]` on the input model).
- Verification timestamps render in Asia/Amman via `AmmanTime`, not raw UTC.
- The multipart upload ceiling tracks `MerchantVerification:MaxDocumentBytes`
  (`FormOptions.MultipartBodyLengthLimit`) and the merchant view shows the configured
  limit instead of a hard-coded "10 MB".

### Not implemented (correctly deferred)

Categories, listings, variants, orders, B2B, storefront pages, email provider, cloud
storage provider. `MerchantLocation` / `MerchantDeliveryZone` are in the domain doc but
belong to fulfilment phases and were not modelled.

## Solution structure

```text
Faed.slnx
src/
└── Faed.Web/
    ├── Models/
    │   ├── Entities/       # MerchantProfile, MerchantVerificationDocument, AdminActionLog
    │   ├── Enums/          # MerchantVerificationStatus, *DocumentType, AdminActionType
    │   ├── Identity/       # ApplicationUser, FaedRoles
    │   └── DomainException.cs
    ├── Data/
    │   ├── ApplicationDbContext.cs   # + IApplicationDbContext, shared with Identity
    │   ├── Configurations/           # IEntityTypeConfiguration<T> for each entity
    │   ├── Migrations/               # EF Core migrations
    │   └── Seed/IdentityDataSeeder.cs
    ├── Services/
    │   ├── Abstractions/   # IApplicationDbContext, IFileStorage, IUserRoleService, IClock
    │   ├── Common/Result.cs
    │   ├── Merchants/      # IMerchantVerificationService + implementation, models, validator, slug
    │   ├── Storage/        # LocalFileStorage
    │   ├── UserRoleService.cs
    │   └── SystemClock.cs
    ├── Authorization/      # FaedPolicies, ApprovedMerchant handler, ClaimsPrincipal ext.
    ├── Areas/{Admin,Merchant,Identity}/
    ├── ViewModels/         # ErrorViewModel (area-local view models under each Area/ViewModels)
    ├── Rendering/          # AmmanTime, MerchantStatusDisplay (view-only helpers)
    ├── DependencyInjection.cs   # AddFaedPlatform composition helper
    └── Program.cs
tests/
├── Faed.UnitTests/         # MerchantProfile state machine, upload validator, slug, foundation
└── Faed.IntegrationTests/  # SQL Server persistence; merchant-verification service + MVC
                            # authorization (WebApplicationFactory + test auth scheme)
```

Both test projects reference `src/Faed.Web` directly. There are no other production
projects and no project-reference layering.

## Migrations

- `20260831174908_InitialIdentity` (`src/Faed.Web/Data/Migrations`) — ASP.NET Core Identity
  schema for `ApplicationUser` (adds `CreatedAtUtc` default `SYSUTCDATETIME()`, `IsActive`
  default `true`). Replaces the Visual Studio template's `00000000000000_CreateIdentitySchema`.
- `AddMerchantVerification` (`src/Faed.Web/Data/Migrations`) — `MerchantProfiles` (unique
  `UserId`, unique `PublicSlug`, indexed `VerificationStatus`; enum stored as text;
  `rowversion` concurrency token; restricted delete from `AspNetUsers`),
  `MerchantVerificationDocuments` (cascade from profile), `AdminActionLogs`. All Guid keys
  are `ValueGeneratedNever` (assigned by the entity constructor). Migration IDs are
  unchanged by the restructure; `dotnet ef migrations has-pending-model-changes` reports
  clean.

## Persistence

- One application `DbContext` (`ApplicationDbContext`) in `src/Faed.Web/Data`, shared
  with Identity and exposed to services through `IApplicationDbContext`.
- SQL Server; local development uses LocalDB database `Faed` (non-secret connection
  string in `appsettings.json`).
- EF Core `rowversion` concurrency is introduced with the inventory model in a later task.

## Private file storage

- `IFileStorage` (`Services/Abstractions`) with `LocalFileStorage` (`Services/Storage`) for development.
- Root defaults to `{ContentRoot}/App_Data/private-storage` (gitignored) or
  `FileStorage:LocalRootPath` when set; startup fails if the resolved root is inside the
  web root. Object keys are server-generated and validated against traversal on read.
- `LocalFileStorage` is registered only outside Production. In Production `IFileStorage`
  resolves to a fail-fast stub until a cloud object store is bound to the interface
  (docs/06-ARCHITECTURE.md §8).

## Identity

- Individual Accounts (ASP.NET Core Identity) preserved from the Visual Studio baseline.
- `AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>()`.
- Roles `Buyer`, `Merchant`, `Admin` seeded idempotently at startup. The `Merchant` role
  is additionally granted to a user on verification approval (idempotent).
- Optional development admin seeding: only when `Faed:AdminSeed:Email` +
  `Faed:AdminSeed:Password` are supplied (user secrets / env) and the environment is not
  Production. No password in source control.
- Merchant verification is a domain state, not an Identity role. Policies: `AdminOnly`
  (role check), `ApprovedMerchant` (per-request DB check of verification status).

## Locked product choices

- English MVP website
- Amman
- Fashion Overstock launch
- Clothing / Shoes / Bags & Accessories
- Verified merchants only as sellers
- B2C + B2B
- no real online payment
- no platform shipping
- no warehouse/fleet
- no used goods
- no Grade E

## Current validation (TASK-002 + single-project architecture audit)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — 52 passed (30 unit, 22 integration), 0 failed. Integration
  tests run against allow-listed LocalDB test databases (`Faed_IntegrationTests`,
  `Faed_WebTests`) which they create and drop; they skip when no SQL Server is reachable,
  override any configured catalog, and never touch the app connection string. Coverage
  includes Home/Login/Register availability, seeded roles, destructive database guards,
  byte-signature rejection of renamed
  uploads, competing-admin concurrency conflict, over-length reason rejection, and a
  non-admin POST to every decision action (Approve/Reject/Suspend/Reinstate) returning
  403 through the real MVC pipeline with no state or audit change.
- `dotnet ef database update` — both migrations apply from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs (Development); Home renders; anonymous `/Merchant/Verification` and
  `/Admin/MerchantVerification` redirect to login; role seeding idempotent; admin seed
  correctly skipped when unconfigured.

### Exit-criteria coverage (tasks/TASK-002)

| Exit criterion | Covered by |
|---|---|
| Buyer cannot access merchant-only workflow | `AdminQueue_Buyer_IsForbidden`, `SellingProbe_*` (pending → 403) |
| Pending merchant cannot submit listings | `ApprovedMerchant` policy DB-checks status; `IsApprovedMerchant` false for pending; `SellingProbe_PendingMerchant_IsForbidden` |
| Admin can approve/reject | `Approve_MovesToApproved_*`, `Reject_RequiresReason_AndRecordsIt`, `AdminQueue_Admin_IsAllowed` |
| Non-admin cannot approve/reject/suspend/reinstate | `AdminDecisions_PostedByBuyer_AreForbidden_AndChangeNoStateOrAudit`, `Decisions_WithoutAnAdminUserId_AreForbidden_AndChangeNothing` |
| Private document URL is not public | no route exposes the storage key; `VerificationDocument_Anonymous_IsChallenged` |
| Unauthorized document request fails | `VerificationDocument_Buyer_IsForbidden` |
| Audit entry is persisted | `Approve_*_WritesAudit`, `OpenVerificationDocument_*_AuditsAccess` |
| Relevant tests pass | full suite green |

## Validation (TASK-001)

- `dotnet build Faed.slnx` — succeeds, 0 warnings.
- `dotnet test Faed.slnx` — 3 passed (2 unit, 1 SQL Server integration against LocalDB
  `Faed_IntegrationTests`, which the test creates and drops via its own
  `Faed_TEST_CONNECTION` variable — never the app connection string).
- `dotnet ef database update` — `InitialIdentity` applies from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs; Home renders; Register/Login reachable; registration creates a user with
  `CreatedAtUtc`/`IsActive` populated; role seeding is idempotent across restarts.
