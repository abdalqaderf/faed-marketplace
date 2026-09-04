# Deployment

Faed is a single deployable ASP.NET Core application (`src/Faed.Web`) with a SQL Server
database. This document covers production configuration, the manual steps that must be
completed before a public deployment, and a release checklist.

---

## 1. Environments

| Environment | Connection string | `IFileStorage` | Demo seed | Dev admin seed | Error pages |
|---|---|---|---|---|---|
| `Development` | `appsettings.Development.json` (LocalDB) | `LocalFileStorage` (disk, outside `wwwroot`) | opt-in | opt-in | developer exception page |
| anything else | `ConnectionStrings__DefaultConnection` **required** (startup rejects missing / LocalDB) | **must be registered** (throws until then) | never runs | never runs | `/Home/Error` + HSTS |

The environment is selected by `ASPNETCORE_ENVIRONMENT`. Anything that is not `Development`
gets production error handling, HSTS and HTTPS redirection (`Program.cs`), requires its own
database connection string, and requires a real private `IFileStorage`. The `Testing`
environment (the integration-test host only) is exempt from the connection-string guard.

---

## 2. Configuration and secrets

Non-secret settings live in `appsettings.json` (committed). **Secrets are never committed** —
supply them through environment variables or the host's secret store. ASP.NET Core maps `__`
in an environment variable name to a config section separator.

**No production database connection string is committed.** The repository does commit a
passwordless LocalDB connection string in `appsettings.Development.json` for local work;
`appsettings.json` has no `ConnectionStrings` section. On startup in any non-`Development`,
non-`Testing` environment the app **fails fast** (throws before serving a request) if
`ConnectionStrings__DefaultConnection` is missing, or if it still points at SQL Server
LocalDB — a copy-pasted development string cannot reach production silently
(`DependencyInjection.ResolveConnectionString`).

### Required in Production

| Setting | Environment variable | Notes |
|---|---|---|
| Database connection | `ConnectionStrings__DefaultConnection` | SQL Server; a least-privilege application login, not `sa`. Must **not** be a LocalDB string (startup rejects it). |
| ASP.NET Data Protection key ring | host-specific | Persist keys (e.g. to blob storage / a mounted volume) so cookies and antiforgery tokens survive restarts and scale-out |
| HTTPS certificate | host-specific | Terminate TLS at the host / reverse proxy; the app also issues HSTS |

### Provided by code, override as needed

All of these have safe defaults in `appsettings.json` and are documented there:

| Section | Purpose |
|---|---|
| `MerchantVerification` | verification-document size limit, count limit |
| `Listings` | image size/count limits, option/variant caps |
| `Ordering` | B2C reservation window, expiry sweep interval, max units per line |
| `B2BNegotiation` | offer validity window (min/default/max), sweep interval, line caps |
| `B2BDeal` | accepted-deal reservation window, sweep interval |
| `Trust` | dispute evidence file count / size limits |
| `Analytics` | stale-listing threshold (validated positive at startup) |
| `FileStorage:LocalRootPath` | Development disk path for private files; must resolve **outside** `wwwroot` (enforced) |

### Must NOT be set in Production

| Setting | Why |
|---|---|
| `Faed:DemoSeed:Enabled` / `Faed:DemoSeed:Password` | The demo seeder is inert outside `Development`, but do not ship the values |
| `Faed:AdminSeed:Email` / `Faed:AdminSeed:Password` | Development-only bootstrap admin |

---

## 3. Manual delivery steps (not bundled with the MVP)

These are deliberately out of scope for the application code because they depend on
unresolved infrastructure decisions. Each has an interface in place; production must supply
an implementation.

1. **Cloud object storage for private files.** `IFileStorage` (`Services/Abstractions`) is
   used for verification documents, listing photos, reference-price evidence and dispute
   evidence. `LocalFileStorage` (local disk) is registered **only in the `Development`
   environment**; in every other environment `DependencyInjection.AddPrivateFileStorage`
   registers a stub that **throws on first use** until a real private object-store
   implementation is registered. Requirements: private bucket/container, randomized object
   keys (already generated server-side), no public URL, allowed content types only.
2. **Email provider.** No `IEmailSender` is registered, so ASP.NET Core Identity uses its
   no-op sender. Identity is configured with `RequireConfirmedAccount = true`; in
   `Development` the Identity UI shows the confirmation link on screen, but **in Production
   account-confirmation and password-reset emails will not be delivered** until an
   `IEmailSender` is registered.
3. **Background expiry sweeps run in-process.** `ReservationExpiryService`,
   `B2BOfferExpiryService` and `B2BDealExpiryService` are hosted `BackgroundService`s. They
   are idempotent and safe, but on a multi-node deployment you must ensure a single runner
   (run the sweeps on one instance, or use a leader-election / scheduled-job mechanism) so
   the same lapsed reservation is not processed by two nodes at once.
4. **Legal/policy content** (Terms of Use, Privacy Policy, seller agreement, dispute policy,
   tax/invoice responsibilities) is unresolved.
5. **Payments, escrow, platform shipping/logistics, warehousing** are explicitly deferred.
   The MVP models pickup and merchant-arranged delivery only; Faed neither books nor prices
   shipping.

---

## 4. Database

- Migrations live in `src/Faed.Web/Data/Migrations`. The application **does not migrate on
  startup**.
- Apply migrations as an explicit, gated release step:
  `dotnet ef database update --project src/Faed.Web` (with the production connection string
  in the environment), or generate a script with
  `dotnet ef migrations script --idempotent --project src/Faed.Web` and run it through your
  DBA process.
- `dotnet ef migrations has-pending-model-changes` must report no drift before release.
- The role seed and catalog reference-data seed run on every startup and are idempotent.

---

## 5. Release checklist

- [ ] `dotnet build Faed.slnx -c Release` — 0 warnings, 0 errors
- [ ] `dotnet ef migrations has-pending-model-changes` — no drift
- [ ] Migrations applied to the target database (explicit step, verified on a clean catalog)
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (or `Staging` etc. — anything but `Development`)
- [ ] `ConnectionStrings__DefaultConnection` set to a least-privilege SQL login, **not** a LocalDB string (startup rejects a missing or LocalDB connection outside Development)
- [ ] A production `IFileStorage` implementation registered and smoke-tested (upload + private download) — `LocalFileStorage` is Development-only and the non-Development stub throws on use
- [ ] An `IEmailSender` registered, or a conscious decision recorded to launch without confirmation email
- [ ] Data Protection key ring persisted (survives restart / scale-out)
- [ ] HTTPS enforced at the edge; HSTS confirmed
- [ ] `Faed:DemoSeed:*` and `Faed:AdminSeed:*` **absent** from the production configuration
- [ ] No secret present in any tracked file (`git grep` for connection strings / keys)
- [ ] Background sweeps confined to a single runner if deploying more than one instance
- [ ] A real administrator account provisioned (assign the `Admin` role to a confirmed user)
- [ ] Structured logs shipped to a log sink; verify no private document content or secret is logged
- [ ] Backup/restore verified for the SQL Server database and the private object store
