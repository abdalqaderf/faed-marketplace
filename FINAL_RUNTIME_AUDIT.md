# Faed Final Runtime Audit

**Task:** TASK-014  
**Date:** 2026-09-04  
**Agent:** Codex  
**Scope:** Review only; application and test source were not modified.

## Executive summary

**Overall result: FAIL**

Restore, Release build, unit tests, migration drift, a fresh migration chain, Development startup, public/static route probes, anonymous authorization boundaries, and the production connection-string guard passed. The required full SQL Server integration run did not pass: 189 tests passed and the demo-data integration test timed out. That same test passed by itself in approximately 2 minutes 30 seconds, including its first seed, idempotent second invocation, and interrupted-seed recovery assertions. This is a repeatability/performance defect, not proof of a deterministic seed-data logic failure.

No P0 issue was found. Three P1 findings and one P2 finding require the fix phase. Browser-driven viewport and console inspection could not be completed because the in-app browser reported `No browser is available`; equivalent HTTP, authentication, route, and asset checks were completed where possible.

**Disposition: READY FOR FIX PHASE**

## Prerequisites and repository state

- `tasks/FINALIZATION_PROGRESS.md` listed no prerequisite reports or files for TASK-014.
- The expected solution and projects exist: `Faed.slnx`, `src/Faed.Web`, `tests/Faed.UnitTests`, and `tests/Faed.IntegrationTests`.
- The repository targets .NET 10 and retains the required single-project MVC production architecture.
- Before this report, `git status --short` showed only the already-present untracked finalization plan/tracker/task files. No application/source change was present or made by this audit.

## Commands run

The substantive verification commands were:

```powershell
dotnet --info
dotnet restore Faed.slnx
dotnet build Faed.slnx -c Release --no-restore

dotnet test tests/Faed.UnitTests/Faed.UnitTests.csproj -c Release --no-build --no-restore --logger "console;verbosity=normal"

dotnet test tests/Faed.IntegrationTests/Faed.IntegrationTests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"

dotnet test tests/Faed.IntegrationTests/Faed.IntegrationTests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~DemoDataSeederTests.Seed_BuildsTheDocumentedDemoDataSet_IsIdempotent_AndResumesAfterAnInterruption" --logger "console;verbosity=normal"

dotnet ef migrations has-pending-model-changes --project src/Faed.Web --startup-project src/Faed.Web --configuration Release --no-build
```

Fresh database migration and verification used this isolated LocalDB catalog:

```powershell
$env:ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=Faed_FinalRuntimeAudit_20260904;Trusted_Connection=True;MultipleActiveResultSets=true'
dotnet ef database update --project src/Faed.Web --startup-project src/Faed.Web --configuration Release --no-build
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "Faed_FinalRuntimeAudit_20260904" -Q "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;"
```

Development startup used the migrated isolated database with demo seeding disabled so startup could be assessed independently of the already-tested demo seed:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:5099'
$env:ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=Faed_FinalRuntimeAudit_20260904;Trusted_Connection=True;MultipleActiveResultSets=true'
$env:Faed__DemoSeed__Enabled='false'
dotnet run --project src/Faed.Web -c Release --no-build --no-launch-profile
```

HTTP probes were issued against `http://127.0.0.1:5099` for `/`, `/Shop`, `/Home/Privacy`, the Login/Register/AccessDenied Identity pages, representative missing listing/store slugs, all three role areas, and the CSS/JavaScript/favicon assets listed in the startup result below. The real Identity UI was also used to register, confirm, and sign in an audit user; a SQL role query and authenticated route probes then checked the resulting authorization behavior.

The production missing-configuration guard was checked with:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
Remove-Item Env:FAED_SQLSERVER_CONNECTION -ErrorAction SilentlyContinue
dotnet run --project src/Faed.Web -c Release --no-build --no-launch-profile
```

The isolated audit database was removed after verification:

```powershell
$env:ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=Faed_FinalRuntimeAudit_20260904;Trusted_Connection=True;MultipleActiveResultSets=true'
dotnet ef database drop --force --project src/Faed.Web --startup-project src/Faed.Web --configuration Release --no-build
```

Two commands initially encountered sandbox-only host access restrictions: restore could not read the user NuGet configuration, and the production startup probe could not write to Windows Event Log. Both were rerun with the required host access; the authoritative results are recorded below. The first LocalDB cleanup attempt similarly could not create/connect to the automatic LocalDB instance inside the sandbox, then succeeded with host access.

## Verification results

| Check | Result | Evidence |
|---|---|---|
| SDK/runtime | PASS | SDK `10.0.400`; ASP.NET Core and .NET runtimes `10.0.11`. |
| Restore | PASS | All projects were up to date for restore. |
| Release build | PASS | 0 warnings, 0 errors; elapsed 23.88 seconds. |
| Unit tests | PASS | 270 passed, 0 failed, 0 skipped. |
| Full SQL Server integration run | **FAIL** | 189 passed, 1 failed, 0 skipped, 190 total; elapsed 11 minutes 47 seconds. The demo-data test failed with `SqlException: Execution Timeout Expired`. |
| Demo-data test in isolation | PASS WITH RELIABILITY NOTE | 1 passed, 0 failed, 0 skipped in approximately 2 minutes 30 seconds. Its assertions cover a clean first seed, an idempotent no-op second seed, and recovery after deleting the completion marker. |
| Pending EF model changes | PASS | `No changes have been made to the model since the last migration.` |
| Fresh migration chain | PASS | A new isolated catalog was created and all 10 migrations applied successfully. |
| Development startup | PASS | The app completed startup and listened on `http://127.0.0.1:5099` without an unhandled application exception. |
| Production missing-connection guard | PASS | Startup failed fast with the intended `InvalidOperationException` from `DependencyInjection.ResolveDatabaseConnectionString` at `src/Faed.Web/DependencyInjection.cs:159`. |
| Route/asset smoke checks | PASS | Expected 200/302/404 responses and static content types were observed; details below. |
| Browser viewport/console inspection | NOT AVAILABLE | The browser runtime reported `No browser is available`. This is a verification-environment limitation, not a discovered product defect. |

### Integration failure detail

The failing test was:

`DemoDataSeederTests.Seed_BuildsTheDocumentedDemoDataSet_IsIdempotent_AndResumesAfterAnInterruption`

The timeout occurred after approximately 8 minutes 4 seconds while `DemoDataSeeder` was placing a B2C order. The relevant stack path was:

- `src/Faed.Web/Services/Ordering/OrderService.cs:139-144`
- `src/Faed.Web/Data/Seed/DemoDataSeeder.cs:590-598`
- `src/Faed.Web/Data/Seed/DemoDataSeeder.cs:268-307`
- `tests/Faed.IntegrationTests/DemoDataSeederTests.cs:27-58`

The isolated passing run logged several listing-aggregate queries taking about 25 seconds even though most other statements completed in milliseconds. This makes the full-suite failure credible as a load-sensitive query/reliability problem despite the isolated pass.

### Migration result

The clean catalog contained these 10 migration history rows, in order:

1. `20260831174908_InitialIdentity`
2. `20260831192035_AddMerchantVerification`
3. `20260831205644_AddCatalog`
4. `20260901141629_AddListingsAndInventory`
5. `20260901153402_AddListingHiddenByAdmin`
6. `20260903113500_AddB2COrders`
7. `20260903121629_AddB2BNegotiation`
8. `20260903141524_AddB2BDeal`
9. `20260903152034_AddDisputesAndReviews`
10. `20260903162224_HardenDisputeInvariants`

The model snapshot matched the migrations. The catalog was successfully dropped after the audit.

### Startup, routes, roles, and assets

Development startup seeded the fixed roles/catalog and the configured Development administrator, skipped demo data as requested, and began serving HTTP. Because this probe explicitly bound only an HTTP URL, HTTPS redirection logged the expected test-harness warning that no HTTPS port could be determined; the normal launch profile includes HTTPS.

Anonymous route results:

| Route | Result |
|---|---|
| `/`, `/Shop`, `/Home/Privacy` | 200 |
| `/Identity/Account/Login`, `/Identity/Account/Register`, `/Identity/Account/AccessDenied` | 200 |
| `/listing/not-a-real-listing`, `/store/not-a-real-store` | 404 |
| `/Admin`, `/Merchant/Verification`, `/Buyer/Orders` | 302 to Login |

Static assets returned 200 with appropriate content types:

- `/css/site.css`
- `/css/faed.css`
- `/lib/bootstrap/dist/css/bootstrap.min.css`
- `/lib/jquery/dist/jquery.min.js`
- `/lib/bootstrap/dist/js/bootstrap.bundle.min.js`
- `/js/listing-detail.js`
- `/favicon.ico`

The registered and confirmed audit account had no Identity role. While signed in, it received:

| Probe | Result |
|---|---|
| `/` | 200; navigation showed My Orders and Merchant Center, not Admin Workspace |
| `/Buyer/Orders` | 200 |
| `/Merchant/Verification` | 200 |
| `/Merchant/Listings` | 302 to AccessDenied |
| `/Admin` | 302 to AccessDenied |

Static inspection found consistent server-side authorization on Admin controllers, approved-merchant operations, buyer order/dispute routes, and participant-scoped trust data. The 189 passing integration tests also exercised authorization, IDOR prevention, workflow transitions, and SQL Server concurrency paths; the one failure was the seed timeout described above.

## Findings

### P0

None found.

### P1

#### P1-01 — Demo seeding is not reliable in the complete integration run

**Evidence**

- The complete integration command failed 1 of 190 tests with a SQL execution timeout; the same test passed alone in about 2 minutes 30 seconds.
- The failure occurred in the multi-collection listing load at `src/Faed.Web/Services/Ordering/OrderService.cs:139-144`, called by `src/Faed.Web/Data/Seed/DemoDataSeeder.cs:590-598`.
- `src/Faed.Web/Data/Seed/DemoDataSeeder.cs:261-265` already raises the database command timeout to five minutes specifically to tolerate a busy full-suite environment. Increasing that timeout did not make the full run reliable.
- `tests/Faed.IntegrationTests/DemoDataSeederTests.cs:27-58` applies a ten-minute outer timeout and verifies all three seed modes in one test.

**Impact**

The required integration gate is red and the demonstration bootstrap is load-sensitive. A clean isolated pass does not satisfy repeatable full-suite verification.

**Recommended fix**

Profile and reduce the expensive aggregate queries instead of raising timeouts again. Start with the order listing load at `OrderService.cs:139-144`: use a bounded projection or appropriate split-query loading while preserving transactional stock and rowversion behavior. Keep the idempotence/recovery assertions, then run the full 190-test suite repeatedly against isolated SQL Server databases and require all runs to pass without skips.

#### P1-02 — Normal registration creates a roleless account while the documented model says it creates a Buyer

**Evidence**

- `src/Faed.Web/Program.cs:39-43` uses the default Identity UI and adds role support, but there is no application Register PageModel that assigns `FaedRoles.Buyer`.
- `src/Faed.Web/Data/Seed/IdentityDataSeeder.cs:20-29` creates the fixed role definitions; demo users are explicitly assigned Buyer in `src/Faed.Web/Data/Seed/DemoDataSeeder.cs:270-272`. Neither path assigns Buyer to a normal registration.
- A user registered, confirmed, and signed in through the real Identity UI during this audit had no `AspNetUserRoles` row.
- `src/Faed.Web/Program.cs:68-73` defines `CanPlaceB2COrder` as authenticated and not Admin, so the roleless account can use Buyer routes.
- `src/Faed.Web/Views/Shared/_Layout.cshtml:61-76` similarly presents Buyer and merchant-onboarding links to every authenticated non-admin user.
- This conflicts with the role model stated in `README.md:27` and with the `Buyer` role defined at `src/Faed.Web/Models/Identity/FaedRoles.cs:11-15`.

**Impact**

Current buying works, but the stored authorization identity disagrees with the documented domain model. Any future check that correctly asks for the Buyer role will reject real registered customers, while current policies silently treat an unclassified account as a buyer.

**Recommended fix**

Scaffold/customize only the Identity Register page and assign `FaedRoles.Buyer` after successful user creation, handling role-assignment failure consistently so a half-created account is not left behind. Keep Merchant as an additive role for verified sellers. Make the B2C policy match the decided role model (Buyer and/or verified Merchant, never Admin), and add an end-to-end integration test proving a normally registered account receives Buyer and has the intended route access.

#### P1-03 — Merchant review history is silently inaccessible after the newest 50 records

**Evidence**

- `src/Faed.Web/Services/Trust/IReviewService.cs:20-26` exposes a `take`-based public method and a merchant-owner method with no page input.
- `src/Faed.Web/Services/Trust/ReviewService.cs:126-161` calculates statistics over every review but clamps the returned collection to 50.
- `src/Faed.Web/Services/Trust/ReviewService.cs:164-175` always requests 50 for the owner dashboard.
- `src/Faed.Web/Areas/Merchant/Controllers/ReviewsController.cs:18-23` accepts no page parameter.
- `src/Faed.Web/Areas/Merchant/ViewModels/MerchantTrustModels.cs:62-65` carries an unpaged `MerchantReviewsView`, and `src/Faed.Web/Areas/Merchant/Views/Reviews/Index.cshtml:21-26,50-56` displays the all-time count but only iterates the truncated collection with no pagination control.

**Impact**

After a merchant receives more than 50 reviews, the page reports the larger total but offers no way to access older review records. This is a growing business-history collection and violates the repository's bounded paging expectation.

**Recommended fix**

Add database-backed page/page-size input for the merchant-owner collection, return the existing paged-result shape (or the same shared paging contract), and render the shared pagination component. Preserve the all-time aggregate query separately. Keep the storefront's intentionally small recent-review preview distinct from the owner history and add a regression test with more than one page of reviews.

### P2

#### P2-01 — Multi-collection listing queries trigger EF Core's cartesian-explosion warning

**Evidence**

- A live listing-detail request logged EF Core event `RelationalEventId.MultipleCollectionIncludeWarning`.
- `src/Faed.Web/Services/Marketplace/PublicMarketplaceService.cs:265-272` includes Options/Values, Variants/OptionValues, Media, and DiscountReasons in one query.
- `src/Faed.Web/Services/Listings/ListingQueries.cs:16-23` includes six collection branches in the reusable aggregate loader.
- `src/Faed.Web/Services/Ordering/OrderService.cs:139-144` uses a similar multi-collection load at the P1-01 timeout location.
- No global `UseQuerySplittingBehavior` or local `AsSplitQuery` configuration was found.

**Impact**

Rows multiply as related collections grow, increasing query time, data transfer, materialization cost, and the chance of another timeout. The live warning confirms this is not only theoretical.

**Recommended fix**

Evaluate each aggregate read and apply `AsSplitQuery()` where it preserves semantics, or replace it with a purpose-built projection. Do not set a global behavior without checking transactional reads and mutation workflows. Add query-count/result-equivalence coverage and remeasure the demo-seed and listing-detail paths.

### Documentation

#### DOC-01 — README test count is stale

`README.md:185` says 456 tests (270 unit + 186 integration). The current discovered totals are 460 (270 + 190), and this audit's actual full run was 459 passing plus 1 failure.

**Recommended fix:** update the README only after TASK-015 produces a fresh green full-suite run; report the exact command, totals, skips, and date without treating an older run as current proof.

#### DOC-02 — PROJECT_STATUS overstates the current runtime result and no longer names the active phase

`PROJECT_STATUS.md:50-52` and `PROJECT_STATUS.md:154-158` state all 190 integration tests pass, but the current required run timed out. `PROJECT_STATUS.md:516-520` says there is no active task, while finalization is now active under `tasks/FINALIZATION_PROGRESS.md`.

**Recommended fix:** after runtime fixes are verified, reconcile the status document with the finalization tracker and the new evidence. Keep the tracker as the coordination source of truth.

#### DOC-03 — Deployment connection-string wording is internally inconsistent

`DEPLOYMENT.md:34-38` says the database connection string is not committed and immediately says it lives in `appsettings.Development.json`; `src/Faed.Web/appsettings.Development.json:2-4` does commit the non-secret LocalDB development string.

**Recommended fix:** say explicitly that no production connection string is committed, while the non-secret Development LocalDB string is committed for local setup.

## Checks with no actionable defect found

- Restore and Release compilation are clean.
- All 270 unit tests pass.
- No migration/model drift exists.
- The complete migration chain applies to a fresh SQL Server LocalDB catalog.
- Development startup completes normally.
- Non-Development startup rejects a missing connection string with a clear deployment-directed error.
- Anonymous role-area access redirects to sign-in; a normal authenticated account cannot enter approved-merchant or Admin routes.
- Missing listing and store slugs return 404.
- The checked routes have corresponding views and the checked static assets resolve.
- No direct public exposure of private verification/evidence storage was found in the reviewed authorization and service paths.

## Final statement

`READY FOR FIX PHASE`
