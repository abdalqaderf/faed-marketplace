# 24 — Delivery and Hardening (TASK-011)

This document records the TASK-011 hardening pass and the delivery artefacts for the Faed
MVP. It is an operational/delivery record, not a product specification — the authoritative
specs remain the numbered files listed in `AGENTS.md` §2.

TASK-011 adds **no new product functionality**. It adds a deterministic development/demo
data set, delivery documentation, and — after the independent final review (§10a) — a set
of hardening fixes (fail-fast configuration guards, admin-cannot-sell enforcement, database
pagination of the previously unbounded list/queue surfaces, an IDOR tightening). No entity,
enum, migration, or state machine was changed; some read-side service signatures gained a
`page` parameter and a `PagedResult<T>` return type.

---

## 1. Scope

Per `tasks/TASK-011-HARDENING-AND-DEMO.md` and `docs/10-IMPLEMENTATION-PLAN.md` Phase 11:

- full authorization audit
- validation audit
- upload security review
- concurrency regression
- responsive QA
- accessibility pass
- performance / paging review
- production configuration documentation
- deterministic demo seed
- final README, clean database setup instructions, deployment checklist

Exit criterion: every check in `docs/11-ACCEPTANCE-CRITERIA.md` is completed or explicitly
deferred with product-owner approval.

---

## 2. Baseline (before TASK-011)

| Check | Result |
|---|---|
| `dotnet build Faed.slnx` | Succeeds, 0 warnings, 0 errors |
| `dotnet test Faed.slnx` | 428 passed (247 unit + 181 SQL Server integration), 0 failed, 0 skipped |
| `dotnet ef migrations has-pending-model-changes` | No model drift |

TASK-001 through TASK-010 were each closed with an independent review and post-review
fix pass (see `PROJECT_STATUS.md`). TASK-011 preserves all of that accepted behaviour.

---

## 3. Authorization audit

Faed enforces authorization in three layers, and this audit confirmed all three are in
place for every state-changing surface:

1. **MVC policy** — `Authorization/FaedPolicies.cs`: `AdminOnly`, `ApprovedMerchant`,
   `CanNegotiateB2B` (approved merchant **and** not an administrator),
   `CanPlaceB2COrder` (authenticated and not an administrator). Registered in
   `Program.cs`; areas/controllers carry `[Authorize(Policy = …)]`.
2. **Service re-check** — every use-case service re-resolves the caller's identity
   (`userId` argument) and re-checks ownership / participation / role from the database.
   Examples: `MerchantListingService` re-resolves the owning merchant;
   `OrderService`, `B2BNegotiationService`, `B2BDealService`, `DisputeService` re-resolve
   the participant; `ListingModerationService`, `AdminCatalogService`,
   `MerchantVerificationService`, `DisputeService` re-check the `Admin` Identity role via
   `IUserRoleService` before any admin mutation.
3. **Database** — filtered unique indexes and check constraints are the last line
   (`docs/17-DATA-INVARIANTS.md`): one active dispute per transaction, one review per
   transaction, unique negotiation→deal, non-negative money, exactly-one-transaction on
   `Dispute` / `Review`.

IDOR: every authenticated detail/action endpoint resolves the record **scoped to the
caller** (`GetMyOrderAsync`, `GetMerchantOrderAsync`, `GetDealAsync`, `GetMyDisputeAsync`,
`GetNegotiationAsync`, …). A non-participant receives the same `NotFound` a non-existent id
receives, so guessing ids never confirms a record exists. Private file endpoints
(`/verification-documents/{id}`, `/dispute-evidence/{id}`, listing media, reference-price
evidence) apply the same rule and never expose a storage object key
(`docs/08-SECURITY-AND-PRIVACY.md` §3, §9; `docs/17-DATA-INVARIANTS.md` "Moderation/Audit").

Coverage: `MerchantVerificationAuthorizationTests`, `OrderHttpTests`, `B2BOfferHttpTests`,
`B2BDealHttpTests`, `TrustHttpTests`, `PublicMarketplaceHttpTests`, `Task010HttpTests`
assert anonymous → 401/redirect, wrong-role → 403, and non-participant → 404 for each area.
No gap was found; no code change was required.

## 4. Validation audit

- All POST input is bound to dedicated `ViewModels` / service input records — no EF entity
  is model-bound (`docs/08-SECURITY-AND-PRIVACY.md` §6). Authorization-sensitive fields
  (merchant id, buyer id, status, price, totals, stock) are never present on an input type;
  they are resolved server-side.
- Money is `decimal` throughout, columns `decimal(18,3)`; offer and order prices are reloaded
  from the listing at placement/acceptance and snapshotted (`docs/08` §7). Precision beyond
  three decimals is rejected before an immutable revision is written.
- Domain constructors and transition methods validate their own invariants and throw
  `DomainException`; services translate routine failures to `Result` (no exceptions for
  control flow, `docs/19-CODING-CONVENTIONS.md`).
- Configurable durations/limits are validated at startup where a bad value is unrecoverable
  (`AnalyticsOptions` via `IValidateOptions` + `ValidateOnStart`).

No code change was required.

## 5. Upload security review

Verification documents, listing photos, reference-price evidence and dispute evidence all
pass through the same fail-closed inspector (`VerificationDocumentValidator.ValidatePayload`,
`ListingImageValidator`, `docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md`):

- declared size / content-type / extension must agree with each other;
- the bytes are buffered and structurally walked (PNG/JPEG chunk-by-chunk, PDF
  stream-by-stream, zlib inflated within a budget) before anything is written;
- script markers, embedded archives/executables and active PDF content are rejected;
- the original filename is never used for storage — a random object key is generated;
- private files live outside `wwwroot` (enforced in `DependencyInjection.AddPrivateFileStorage`,
  which throws if `FileStorage:LocalRootPath` resolves inside the web root);
- the multipart body limit in `Program.cs` tracks the largest configured per-file cap so a
  configuration change cannot silently break uploads.

`VerificationDocumentValidatorTests` (unit) and the per-area HTTP tests cover the accepted
and rejected paths. No code change was required.

## 6. Concurrency regression

The SQL Server `rowversion` + transaction guarantees from TASK-004/006/007/008/009 were
re-run as part of the full suite (`docs/09-TEST-STRATEGY.md` §2 — never InMemory/SQLite):

- two B2C buyers competing for the last unit — one succeeds (`OrderServiceTests`);
- B2C order vs. accepted B2B deal for the same stock (`B2BDealServiceTests`);
- two B2B acceptances competing for the same stock — no oversell (`B2BDealServiceTests`);
- reservation release is idempotent for B2C and B2B (`OrderServiceTests`, `B2BDealServiceTests`);
- one active dispute per transaction under a deterministic interleave (`TrustServiceTests`);
- one review per transaction under a race (`TrustServiceTests`).

The TASK-011 demo seeder drives real orders, an accepted deal and a completed deal through
these same transactional paths, adding end-to-end coverage. All green.

## 7. Responsive QA / accessibility pass

The public, merchant and admin UIs were built against the project skills
(`.claude/skills/faed-*`, `docs/07-UI-UX-SPEC.md`) in TASK-004–010: mobile-first layout,
semantic landmarks, `scope`-annotated tables inside `overflow-x` wrappers, non-colour status
badges (`Rendering/*StatusDisplay`), `role="status"` / `role="alert"` messaging,
`<fieldset>`/`<legend>` groupings, explicit empty and error states, and a branded
status-code page (`Program.cs` `UseStatusCodePagesWithReExecute`). This pass re-confirmed
the checklist items in `docs/11-ACCEPTANCE-CRITERIA.md` "UI"; no regressions were found.
Condition meaning, discount reason and defect photos are surfaced as text, not only as a
letter grade (`ConditionGrade.Description`, `ListingMediaType.Defect`).

## 8. Performance / paging review

`docs/06-ARCHITECTURE.md` §13 MVP guidance is followed: async DB calls throughout;
list/queue projections `Select` only needed fields; browse queries are indexed
(`ListingConfiguration`, status/date queue indexes on orders, deals, disputes, negotiations,
audit log). **Every** list, queue and history surface is now database-paged (COUNT +
`Skip`/`Take` in SQL) with stable page size, total counts and a shared `_Pagination` partial
— buyer/merchant orders, B2B negotiations, B2B deals, disputes, the merchant-verification,
listing-moderation and dispute admin queues (finding 6 above), plus the admin
transactions/reviews/audit history already paged in TASK-010, unified onto the same
`PagedResult<T>`. No in-memory pass over an unbounded set feeds a view or a count. No cache
or search engine was added (explicitly deferred until a measured bottleneck).

## 9. Configuration changes (TASK-011)

| Change | File | Reason |
|---|---|---|
| `ConnectionStrings:DefaultConnection` moved out of `appsettings.json` | `appsettings.json` → `appsettings.Development.json` | Non-Development environments must supply their own; the app fails fast rather than falling back to the committed LocalDB string (finding 2) |
| `Faed:DemoSeed` section (`Enabled: false`) | `appsettings.Development.json` | Opt-in switch for the demo data set; disabled by default, Development-only |
| CI SQL password: removed the `\|\| 'Faed_Ci_Password_1'` fallback | `.github/workflows/ci.yml` | The password is a `CI_SQL_PASSWORD` GitHub Actions secret only (finding 5) |

No secret, credential, connection string or machine-specific path is present in any tracked
file. `appsettings.Development.json`'s LocalDB string is a passwordless trusted-connection
string.

No secret, credential, connection string or machine-specific path was added to any tracked
file. The tracked development connection string remains a passwordless LocalDB
trusted-connection string.

## 10. Demo / seed data (TASK-011)

`src/Faed.Web/Data/Seed/DemoDataSeeder.cs` — a deterministic data set for field validation
and demonstration (`docs/12-SEED-DATA.md`). Key properties:

- **Production-safe**: runs only when `environment.IsDevelopment()` **and**
  `Faed:DemoSeed:Enabled` is `true` **and** `Faed:DemoSeed:Password` is supplied
  out-of-band. In every other case it logs one line and returns.
- **No shortcuts**: every merchant, listing, order, negotiation, deal, dispute and review
  is created by calling the same application services a real request calls, so moderation,
  authorization, price integrity, MOQ and stock concurrency all apply unchanged. It never
  writes an entity directly and never relaxes a rule.
- **Idempotent**: the first demo merchant account is a sentinel; re-running the app is a
  no-op. Rebuild by dropping the database.
- **Deterministic**: fixed emails, business names, SKUs, quantities and prices.
- **Embedded fixtures only**: a genuine 1×1 PNG and a minimal valid PDF, both of which pass
  the production upload inspector. No external files, no secrets.

Data set (`docs/12-SEED-DATA.md`):

| Item | Detail |
|---|---|
| `demo-admin@faed.local` | Admin |
| `merchant-a@faed.local` | Approved merchant "Amman Threads" (pickup location + delivery zone) |
| `merchant-b@faed.local` | Approved merchant "Petra Footwear" (pickup location + delivery zone) |
| `pending-merchant@faed.local` | Merchant "Rainbow Kids Wear" — submitted, still `PendingReview` |
| `buyer-a@faed.local`, `buyer-b@faed.local` | Individual buyers |
| Listing — Court Low Sneakers | Grade B, Past Season + Packaging Damage, Size 41/42/43 × Black, B2C + B2B, MOQ 10 |
| Listing — Everyday Cotton Crew Tee | Grade A, Overstock, Size/Colour options, B2C + B2B |
| Listing — Structured Leather Tote | Grade D, Display Item, visible defect photo, B2C only |
| Listing — Merino Half-Zip | Ends **SoldOut** after a demo buyer clears the last units |
| Scenario — active B2C order | Buyer A → Amman Threads, `Confirmed` |
| Scenario — completed B2C order | Buyer B → Amman Threads, `Completed` (buyer confirmed receipt) |
| Scenario — open B2B negotiation | Petra Footwear → Amman Threads tees, `Open` |
| Scenario — counter-offer chain | Amman Threads → Petra sneakers, countered, 2 revisions, `Open` |
| Scenario — completed B2B deal | Amman Threads buys 15 pairs from Petra, `Completed` |
| Scenario — dispute | Buying merchant raises `MissingItems` on the completed deal; admin `UnderReview` |
| Scenario — review | Buyer B leaves a 5★ review on the completed B2C order |

Coverage: `Faed.IntegrationTests.DemoDataSeederTests` (SQL Server, its own `Faed_DemoSeedTests`
catalog) — builds the full data set through the real services, then asserts a second run is a
no-op and that deleting the completion marker makes a third run purge the partial data and
rebuild the whole set.

## 10a. Final-review fixes (independent review of TASK-011)

The independent final review raised eight findings. All are fixed; the fixes are within
TASK-011 scope (hardening + delivery) and add no product feature.

| # | Finding | Root-cause fix |
|---|---|---|
| 1 | Demo seed reliability / SQL query pressure / idempotency / recovery after a partial run | `DemoDataSeeder` now: builds the scenario as one linear pass (no accumulating in-memory work); makes every lookup a projected `AsNoTracking` query so no full table is loaded; clears the change tracker before the transactional scenarios; raises the seed context's command timeout to 5 min so a query does not abort under the brief LocalDB starvation a full test run can cause; defines "fully seeded" by the final artifact (the buyer's review), not an early sentinel; and — the recovery path — when it finds demo accounts but no completion marker (an interrupted previous run) it **purges the partial demo data in foreign-key-safe order and rebuilds from scratch**. Restarting the app is enough to recover; `ef database drop` is not required. |
| 2 | Non-Development silently using the committed LocalDB connection | The development connection string moved to `appsettings.Development.json` (removed from `appsettings.json`). `DependencyInjection.ResolveDatabaseConnectionString` **throws at startup** for any non-Development, non-Testing environment when `ConnectionStrings__DefaultConnection` is missing **or** still targets SQL Server LocalDB. |
| 3 | Local private-file storage usable outside Development | `AddPrivateFileStorage` registers `LocalFileStorage` **only** when `environment.IsDevelopment()`; every other environment gets a stub that throws on first use until a real private object store is registered. |
| 4 | An administrator could create / approve their own merchant identity | `MerchantVerificationService` rejects `SaveDraftAsync` / `AddDocumentAsync` / `SubmitForReviewAsync` from an Admin, and `ApproveAsync` / `ReinstateAsync` refuse a profile whose owner holds the Admin role. The `ApprovedMerchant` MVC policy now also excludes administrators (like `CanNegotiateB2B` / `CanPlaceB2COrder`), so selling authorization can never be satisfied by an admin account. |
| 5 | Committed CI SQL `sa` password fallback | `.github/workflows/ci.yml` uses `${{ secrets.CI_SQL_PASSWORD }}` with **no** `|| '…'` fallback, plus a "Require CI SQL secret" preflight step that fails the run early with a clear message when the secret is unset. |
| 6 | Unbounded order / deal / negotiation / dispute / admin queue surfaces | A shared `PagedResult<T>` + `Paging` + `ToPagedResultAsync` (`Services/Common`). `IOrderService.GetMyOrdersAsync` / `GetMerchantOrdersAsync`, `IB2BNegotiationService.GetMyNegotiationsAsync`, `IB2BDealService.GetMyDealsAsync`, `IDisputeService.GetMyDisputesAsync` / `GetQueueAsync`, `IMerchantVerificationService.GetQueueAsync` and `IListingModerationService.GetQueueAsync` now return a bounded page (COUNT + `Skip`/`Take` in SQL) with a `?page=` route param and a shared `_Pagination` partial. The B2B-negotiation "awaiting me/them" filter moved from an in-memory pass over the merchant's whole history to a SQL `EXISTS` on the current revision's proposer. `GetMyDisputesAsync`'s per-transaction use is served by a new bounded `GetDisputesForTransactionAsync`. The admin transaction/review/audit screens (already paged in TASK-010) were unified onto the same `PagedResult<T>` / `_Pagination`. |
| 7 | Listing media / evidence IDOR leaked record existence | `ListingMediaService.OpenImageAsync` / `OpenReferencePriceEvidenceAsync` return `NotFound` (not `Forbidden`) for an unauthorized caller, so a bad id and an unauthorized private id are indistinguishable (mirrors the TASK-009 dispute-evidence endpoint). |
| 8 | README / setup / test-DB documentation inaccurate | `README.md` and `DEPLOYMENT.md` updated: connection-string location and fail-fast behaviour, `LocalFileStorage` being Development-only, the CI secret requirement, clean-database behaviour (nothing is created/migrated/dropped on startup), and the three destructively managed test catalogs (`Faed_IntegrationTests`, `Faed_WebTests`, `Faed_DemoSeedTests`) with the safety guard that re-checks the target before every drop. |

Tests added: `Faed.UnitTests.PagedResultTests`, `Faed.UnitTests.DatabaseConnectionResolutionTests`,
`Faed.UnitTests.PrivateFileStorageRegistrationTests`,
`Faed.IntegrationTests.Task011HardeningTests` (admin-cannot-be-merchant at the service and
HTTP layers; buyer-order-history database paging plus the `_Pagination` partial rendering for
page 1 and page 2; per-transaction dispute scoping); `DemoDataSeederTests` was rewritten
against its own `Faed_DemoSeedTests` catalog and now also asserts idempotency and recovery
after an interrupted run; the `PublicMarketplaceHttpTests` suspended-listing image assertion
was updated to prove the bad-id / unauthorized-id equivalence. No existing test was weakened.

## 11. Known limitations / manual delivery steps

| Item | Status | Reference |
|---|---|---|
| Production `IFileStorage` (cloud object store) | **Not bundled.** Registered only in Development; every other environment gets a stub that throws on first use until a real implementation is registered. Interface is in place (`IFileStorage`). | `docs/06-ARCHITECTURE.md` §8; `docs/13-OPEN-QUESTIONS.md` §27 |
| Production `IEmailSender` | **Not bundled.** Identity is configured with `RequireConfirmedAccount = true`; in Development the Identity UI shows the confirmation link directly. Production must register an email provider or account confirmation will not be delivered. | `docs/13-OPEN-QUESTIONS.md` §2, §28 |
| Real online payments / escrow / platform shipping | Out of scope for the MVP by design. | `docs/10-IMPLEMENTATION-PLAN.md` "Explicitly deferred" |
| Legal/policy pages (Terms, Privacy, seller agreement) | Placeholders only; content is an open question. | `docs/13-OPEN-QUESTIONS.md` §19–25 |
| Background expiry sweeps | Hosted `BackgroundService`s (in-process). Adequate for a single-node MVP; a multi-node deployment needs a single-runner guarantee. | `docs/06-ARCHITECTURE.md` §7 |

## 12. Validation (after TASK-011)

| Check | Result |
|---|---|
| `dotnet build Faed.slnx` (Debug + Release) | Succeeds, 0 warnings, 0 errors |
| `dotnet test Faed.slnx` | **456 passed** (270 unit + 186 SQL Server integration), 0 failed, 0 skipped |
| `dotnet ef migrations has-pending-model-changes` | No model drift; no migration added |
| `dotnet ef database update` on an empty catalog | Every migration applies cleanly; catalog dropped afterwards |
| Secret scan (`git grep` for connection strings / passwords / keys over tracked files) | No secret; the only tracked connection string is the passwordless LocalDB one in `appsettings.Development.json` |
| App startup, `Development` + demo config | Starts, seeds the full demo data set once ("Demo data set seeded."), Home / Shop / Login / listing detail all HTTP 200, demo listings visible on Shop |
| App startup, demo seed disabled / no password | Starts normally, logs one line, no demo data written |

## 13. Out-of-scope confirmation

No feature, endpoint, entity, migration or schema change was added. `dotnet ef migrations
has-pending-model-changes` reports no drift and no migration exists for TASK-011 or its
final-review fixes.

Production-code changes are all hardening or delivery, within TASK-011's stated scope:
the Development-only `DemoDataSeeder` (+ its guarded one-line call in `Program.cs`); the
fail-fast connection-string / file-storage guards and the admin-cannot-sell guards in
`DependencyInjection` / `MerchantVerificationService` / the `ApprovedMerchant` policy; the
`NotFound`-instead-of-`Forbidden` change in `ListingMediaService`; and the shared
`PagedResult<T>` paging applied to the previously unbounded list/queue services and their
thin controllers/views. Everything else is documentation and one disabled-by-default
configuration section (the development connection string also moved from `appsettings.json`
to `appsettings.Development.json`).
