# Deployment

Faed is a single deployable ASP.NET Core application (`src/Faed.Web`) on .NET 10 with a SQL
Server database. This document covers production configuration, the infrastructure the app
talks to, and a release checklist. It is written from what the code does — regenerated in
Phase 14 against `Program.cs` and `DependencyInjection.cs`.

---

## 1. Environments

| Environment | Connection string | `IFileStorage` | Email | Demo seed | Dev admin seed | Error pages |
|---|---|---|---|---|---|---|
| `Development` | `appsettings.Development.json` (LocalDB) | `LocalFileStorage` (disk, outside `wwwroot`) | Brevo (needs `Email:Brevo`) | opt-in | opt-in | developer exception page |
| `Testing` (integration-test host only) | disposable catalog injected by the host | `LocalFileStorage` | Brevo | never | never | — |
| anything else | `ConnectionStrings__DefaultConnection` **required** (startup rejects missing / LocalDB) | `R2FileStorage` (needs `FileStorage:R2`) | Brevo (needs `Email:Brevo`) | never | never | `/Home/Error` + HSTS |

The environment is selected by `ASPNETCORE_ENVIRONMENT`. Anything that is not `Development`
gets production error handling, HSTS and HTTPS redirection, requires its own database
connection string, and gets the Cloudflare R2 file store. The two background sweeps are hosted
in every environment **except `Testing`**, where the integration tests drive the deadlines
deterministically through the services and a fake clock.

---

## 2. Database and connection string

- Migrations live in `src/Faed.Web/Data/Migrations` — one migration
  (`20260909112228_InitialCreate`) plus the model snapshot. The application **does not migrate
  on startup**.
- Apply migrations as an explicit, gated release step:
  `dotnet ef database update --project src/Faed.Web` with the production connection string in
  the environment, or `dotnet ef migrations script --idempotent --project src/Faed.Web` run
  through your DBA process.
- `dotnet ef migrations has-pending-model-changes` must report no drift before release.
- The role seed and the catalog reference-data seed (3 subscription plans, 4 condition grades,
  8 discount reasons, 4 categories) run on every startup and are idempotent.

**No production connection string is committed.** `appsettings.json` has no `ConnectionStrings`
section; `appsettings.Development.json` carries a passwordless LocalDB string for local work
only. On startup in any non-`Development`, non-`Testing` environment the app **fails fast**
(`DependencyInjection.ResolveDatabaseConnectionString`) if `ConnectionStrings__DefaultConnection`
is missing or still points at SQL Server LocalDB — a copy-pasted development string cannot
reach production silently.

---

## 3. Infrastructure the app talks to

Two external services have real implementations behind interfaces. Both need configuration
supplied by environment variable (`__` maps to a config section separator); neither secret is
committed.

### 3.1 Cloudflare R2 — private file storage

`IFileStorage` (`Services/Abstractions`) backs verification documents, listing photos and
reference-price evidence. `DependencyInjection.AddPrivateFileStorage` registers
**`R2FileStorage`** (backed by the `AWSSDK.S3` package, S3-compatible) in every environment
except `Development`, which uses `LocalFileStorage` on local disk. Object keys are random and
generated server-side; no public URL is ever stored.

| Setting | Environment variable |
|---|---|
| Account id | `FileStorage__R2__AccountId` |
| Access key id | `FileStorage__R2__AccessKeyId` |
| Secret access key | `FileStorage__R2__SecretAccessKey` |
| Bucket name | `FileStorage__R2__BucketName` |

Requirements: a **private** bucket, allowed content types only. In `Development`,
`FileStorage:LocalRootPath` must resolve **outside** `wwwroot` (startup enforces this).

### 3.2 Brevo — transactional email

`BrevoEmailSender` implements ASP.NET Core Identity's `IEmailSender` and is registered in
`Program.cs` via `AddHttpClient` in **every** environment. It uses Brevo's HTTPS API
(`api.brevo.com`), not SMTP, so it works on hosts that block outbound SMTP. Identity is
configured with `RequireConfirmedAccount = true`, so account-confirmation and password-reset
email will not be delivered until this is configured wherever registration is used.

| Setting | Environment variable |
|---|---|
| API key | `Email__Brevo__ApiKey` |
| From address | `Email__Brevo__FromEmail` |
| From name | `Email__Brevo__FromName` (defaults to `Faed`) |

`BrevoEmailSender` throws at construction if `ApiKey` or `FromEmail` is missing — the first
email send fails loudly rather than silently dropping mail.

### 3.3 Background services (in-process)

Two hosted `BackgroundService`s run the deadlines from `CORE.md` §8:

- **`OrderDeadlineService`** — the 12 h reservation-confirm sweep, the 48 h collection sweep
  (`NoShow`), and the 72 h stale-pickup sweep.
- **`SubscriptionExpiryService`** — moves `Active` subscriptions past `ExpiresAtUtc` to
  `Expired` and hides that merchant's listings; renewal restores them.

Both are idempotent. On a multi-node deployment, confine them to a single runner (one
instance, or leader election) so a lapsed reservation is not processed twice.

---

## 4. Configuration reference

Non-secret settings live in `appsettings.json` (committed) with safe defaults:

| Section | Purpose |
|---|---|
| `MerchantVerification` | verification-document size and count limits |
| `Listings` | image size/count limits, option/variant caps |
| `Ordering` | reservation window, no-show window, auto-close window, sweep interval, max units per line |
| `Subscriptions` | expiry sweep interval and related windows |
| `Analytics` | stale-listing threshold (validated positive at startup) |
| `FileStorage:LocalRootPath` | Development disk path for private files; must resolve outside `wwwroot` |

### Required in Production

| Setting | Environment variable |
|---|---|
| Database connection | `ConnectionStrings__DefaultConnection` — least-privilege SQL login, not LocalDB |
| R2 credentials | `FileStorage__R2__*` (§3.1) |
| Brevo credentials | `Email__Brevo__ApiKey`, `Email__Brevo__FromEmail` (§3.2) |
| Data Protection key ring | host-specific — persist so cookies and antiforgery tokens survive restart / scale-out |
| HTTPS certificate | terminate TLS at the host / reverse proxy; the app also issues HSTS |

### Must NOT be set in Production

| Setting | Why |
|---|---|
| `Faed:DemoSeed:Enabled` / `Faed:DemoSeed:Password` | the demo seeder is inert outside `Development`, but do not ship the values |
| `Faed:AdminSeed:Email` / `Faed:AdminSeed:Password` | Development-only bootstrap admin |

---

## 5. Manual steps still out of scope

- **Legal / policy content** — Terms of Use, Privacy Policy, seller agreement, tax and invoice
  responsibilities.
- **Payments, escrow, platform shipping / logistics, warehousing** — deliberately deferred.
  The platform never handles money; it models pickup and merchant-arranged delivery only.

---

## 6. Release checklist

- [ ] `dotnet build Faed.slnx -warnaserror` — 0 warnings, 0 errors
- [ ] `dotnet test` — green
- [ ] `dotnet ef migrations has-pending-model-changes` — no drift
- [ ] Migrations applied to the target database (explicit step, verified on a clean catalog)
- [ ] `ASPNETCORE_ENVIRONMENT` set to anything but `Development`
- [ ] `ConnectionStrings__DefaultConnection` — least-privilege SQL login, not a LocalDB string
- [ ] `FileStorage__R2__*` set; upload + private download smoke-tested
- [ ] `Email__Brevo__ApiKey` and `Email__Brevo__FromEmail` set; a confirmation email received
- [ ] Data Protection key ring persisted (survives restart / scale-out)
- [ ] HTTPS enforced at the edge; HSTS confirmed
- [ ] `Faed:DemoSeed:*` and `Faed:AdminSeed:*` absent from production configuration
- [ ] No secret in any tracked file
- [ ] Background sweeps confined to a single runner if deploying more than one instance
- [ ] A real administrator account provisioned (assign `Admin` to a confirmed user)
- [ ] Structured logs shipped to a sink; verify no private document content or secret is logged
- [ ] Backup/restore verified for the SQL Server database and the R2 bucket
