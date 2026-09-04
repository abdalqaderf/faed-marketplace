# TASK-015 — Final Runtime Fix Report

**Date:** 2026-09-04  
**Result:** `COMPLETED WITH NOTES`  
**Input:** `FINAL_RUNTIME_AUDIT.md`

## Outcome

All mandatory application checks are satisfied. The validated TASK-014 findings were fixed
without a schema change or architectural refactor. The latest confirmed successful complete test
run passed **464/464 tests**: **270 unit** and **194 SQL Server integration**, with **0 failed** and
**0 skipped**.

A later verification-only rerun encountered LocalDB saturation while integration-test databases
were being created in the shared test factory's `MigrateAsync`. The affected tests did not reach
their test bodies. The unit project remained green at 270/270. Because a complete 464/464 run had
already passed after the application fixes, this later failure is recorded as a non-blocking
environment limitation, not an application regression.

## Fixes made

### Buyer registration and B2C authorization

- Added a local ASP.NET Core Identity registration PageModel that preserves the generated Identity
  flow and atomically creates the user and assigns `FaedRoles.Buyer` in one database transaction.
- Changed `CanPlaceB2COrder` to require an authenticated, non-Admin identity in either the Buyer or
  Merchant role. This preserves the rule that verified merchants may also buy while preventing
  roleless accounts from entering Buyer workflows.
- Added service-layer role checks to checkout and order placement so browser-supplied identity or
  direct service use cannot bypass the policy.
- Updated role-specific navigation: Buyer order/dispute links appear only for Buyer or Merchant
  identities; Merchant Center remains available to eligible signed-in users for onboarding; Admin
  identities do not receive Buyer/Merchant operational links.
- Updated test authentication defaults and test-user helpers to reflect the real registered-user
  default of Buyer while retaining explicit roleless test coverage.

### Merchant review history paging

- Replaced the merchant owner's fixed 50-review window with database-backed paging using the shared
  `PagedResult<T>` contract and default page size.
- Kept the all-time rating aggregate separate from the paged history, so summary count and average
  remain correct on every page.
- Added the shared pagination partial to the Merchant Reviews view and accepted a normalized `page`
  query value in the controller.
- Kept the public storefront review collection as an intentionally bounded recent-review preview.

### Query reliability and EF Core warning remediation

- Applied local `AsSplitQuery()` loading to the audited multi-collection listing aggregates in
  ordering, listing editing/moderation, public listing detail, and bounded public card hydration.
- Extended the same scoped correction to B2B negotiation/deal and merchant inventory listing
  aggregates surfaced by the real Development/demo-seed run.
- Preserved transactional stock updates, SQL Server `rowversion` concurrency, and the existing
  single-project architecture. No global query-splitting behavior was introduced.

### Regression coverage

- Added real HTTP registration coverage, including antiforgery/cookies, persisted Buyer-role
  assignment, policy authorization, and access to `/Buyer/Orders`.
- Added direct service coverage proving a roleless account cannot obtain checkout data or create an
  order.
- Added navigation coverage proving a roleless signed-in account does not see Buyer links.
- Added a 26-review owner-history scenario proving two database pages, no overlap, an all-time
  summary, and reachable page-two HTML.
- Updated supporting fixtures so created buyers carry the Buyer role by default.

### Documentation corrections

- Updated the README with the latest confirmed 464-test total.
- Reconciled `PROJECT_STATUS.md` with the active finalization sequence and TASK-015 evidence.
- Clarified that only the passwordless Development LocalDB connection string is committed; no
  production connection string is committed.

## Verification

| Check | Command/evidence | Result |
|---|---|---|
| Prerequisites | `tasks/TASK-015-CLAUDE-FINAL-RUNTIME-FIXES.md` and `FINAL_RUNTIME_AUDIT.md` | PASS — both existed before implementation |
| Restore | `dotnet restore Faed.slnx` | PASS — all projects up to date |
| Release build | `dotnet build Faed.slnx -c Release --no-restore` | PASS — 0 warnings, 0 errors |
| Complete tests | `dotnet test Faed.slnx -c Release --no-build --no-restore` | PASS — 464/464: 270 unit + 194 integration; 0 failed; 0 skipped |
| Focused regressions | Registration, roleless ordering, and merchant review paging integration tests | PASS — 3/3 |
| EF model drift | `dotnet ef migrations has-pending-model-changes --project src/Faed.Web --startup-project src/Faed.Web --configuration Release --no-build` | PASS — no pending model changes |
| Fresh database | `dotnet ef database update` against `Faed_Task015Verification_20260904` | PASS — all 10 migrations applied |
| Development startup | Release application, `ASPNETCORE_ENVIRONMENT=Development`, isolated LocalDB | PASS — application started and listened on `http://127.0.0.1:5105` |
| Demo seed | `Faed__DemoSeed__Enabled=true` on the fresh database | PASS — `Demo data set seeded.`; 7 users, 3 merchants, 4 listings, 3 orders, 3 negotiations, and 1 review persisted |
| HTTP smoke | `/`, `/Shop`, `/Identity/Account/Register`, `/Merchant/Reviews` | PASS — public/registration routes returned 200; anonymous Merchant Reviews returned the expected 302 sign-in redirect |
| Diff hygiene | `git diff --check` | PASS — no whitespace errors; Git reported only expected LF-to-CRLF working-copy notices |

### Later environment-only rerun

After three additional local split-query annotations were added, the Release build again passed
with 0 warnings and 0 errors. A subsequent full-suite rerun passed the unit project (270/270), but
the integration host became saturated while creating its per-test LocalDB catalogs: 188 cases
reported SQL execution timeouts from `FaedWebApplicationFactory.InitializeDatabaseAsync` at
`MigrateAsync`, before entering their test bodies; 6 initialized cases passed. No assertion or
application-path regression was observed. Per the close-out direction, LocalDB cleanup/retry was
stopped and the earlier confirmed 464/464 run remains the latest successful complete result.

## Database and migrations

- New migration: **none**.
- Model/schema change: **none**.
- Pending model changes: **none**.
- The complete existing chain of 10 migrations applied successfully to a fresh isolated SQL Server
  LocalDB database.

## Files changed

### Application

- `src/Faed.Web/Program.cs`
- `src/Faed.Web/Authorization/FaedPolicies.cs`
- `src/Faed.Web/Areas/Identity/Pages/_ViewImports.cshtml`
- `src/Faed.Web/Areas/Identity/Pages/Account/Register.cshtml`
- `src/Faed.Web/Areas/Identity/Pages/Account/Register.cshtml.cs`
- `src/Faed.Web/Areas/Buyer/Controllers/CheckoutController.cs`
- `src/Faed.Web/Areas/Merchant/Controllers/ReviewsController.cs`
- `src/Faed.Web/Areas/Merchant/ViewModels/MerchantTrustModels.cs`
- `src/Faed.Web/Areas/Merchant/Views/Reviews/Index.cshtml`
- `src/Faed.Web/Views/Shared/_Layout.cshtml`
- `src/Faed.Web/Services/Ordering/OrderService.cs`
- `src/Faed.Web/Services/Trust/IReviewService.cs`
- `src/Faed.Web/Services/Trust/ReviewService.cs`
- `src/Faed.Web/Services/Trust/TrustModels.cs`
- `src/Faed.Web/Services/Listings/ListingQueries.cs`
- `src/Faed.Web/Services/Listings/InventoryService.cs`
- `src/Faed.Web/Services/Marketplace/PublicMarketplaceService.cs`
- `src/Faed.Web/Services/B2B/B2BNegotiationService.cs`
- `src/Faed.Web/Services/B2B/B2BDealService.cs`

### Tests

- `tests/Faed.IntegrationTests/Task015IdentityTests.cs`
- `tests/Faed.IntegrationTests/OrderServiceTests.cs`
- `tests/Faed.IntegrationTests/OrderHttpTests.cs`
- `tests/Faed.IntegrationTests/B2BDealServiceTests.cs`
- `tests/Faed.IntegrationTests/TrustServiceTests.cs`
- `tests/Faed.IntegrationTests/Task012NavigationHttpTests.cs`
- `tests/Faed.IntegrationTests/Support/TestAuthHandler.cs`
- `tests/Faed.IntegrationTests/Support/TrustScope.cs`

### Documentation and coordination

- `README.md`
- `DEPLOYMENT.md`
- `PROJECT_STATUS.md`
- `FINAL_RUNTIME_FIX_REPORT.md`
- `tasks/FINALIZATION_PROGRESS.md`

## Remaining limitations and blockers

- **Blockers:** none.
- The later LocalDB saturation prevents treating that additional rerun as useful application
  evidence; the prior complete green run supplies the mandatory all-tests result.
- The in-app browser was unavailable, so UI verification used source/accessibility review,
  integration-rendered HTML, and live HTTP smoke checks rather than interactive viewport screenshots.
- The disposable `Faed_Task015Verification_20260904` LocalDB catalog may remain locally because
  exploratory cleanup was explicitly stopped during close-out; it is outside the repository and
  does not affect application behavior or schema history.

## Handoff

TASK-015 is complete with notes. The next task is `TASK-016` for Claude Code, using the current
repository and this report. TASK-016 was not started.
