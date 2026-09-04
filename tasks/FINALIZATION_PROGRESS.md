# Faed — Finalization Progress Tracker

> This file is the single coordination point between Codex and Claude Code.
> Every agent must read this file before starting work and update it before finishing.
> Do not rely on chat history when this file and the repository are available.

---

## Current State

**Project Phase:** Finalization after Phase One submission  
**Overall Status:** IN PROGRESS  
**Current Task:** TASK-020  
**Next Agent:** Claude Code  
**Next Action:** Execute `TASK-020-CLAUDE-README-FINAL-DOCS.md` — rewrite `README.md` and
finalize retained documentation, using the cleaned repository plus `REPOSITORY_CLEANUP_REPORT.md`
as input. Before deleting `PROJECT_STATUS.md`, `FINAL_DEMO_AUDIT.md`, `FINAL_RUNTIME_AUDIT.md`,
`FINAL_RUNTIME_FIX_REPORT.md`, `DEMO_DATA_REPORT.md`, or `REPOSITORY_CLEANUP_AUDIT.md`, extract
anything the README still needs from them (demo accounts, known limitations) — per TASK-019's
cleanup ordering, they were intentionally not deleted yet.

---

## Agent Workflow Rule

Before starting any task:

1. Read this file completely.
2. Read the task file referenced in `Current Task`.
3. Read the output/report files produced by prerequisite tasks.
4. Inspect the current repository state.
5. Execute only the assigned task.
6. Do not silently skip required verification.
7. Before finishing, update this file.

After completing a task, the agent MUST:

- mark the task as `COMPLETED`, `COMPLETED WITH NOTES`, or `BLOCKED`;
- add the completion date;
- write a short factual summary;
- list the main files changed or created;
- list the main verification results;
- list any unresolved blockers;
- set `Current Task` to the next task;
- set `Next Agent`;
- set `Next Action`.

If a task is blocked, do **not** advance to the next task unless the blocker is explicitly non-blocking.

---

# Task Progress

| Task | Agent | Purpose | Status | Main Output |
|---|---|---|---|---|
| TASK-014 | Codex | Final runtime audit | COMPLETED WITH NOTES | `FINAL_RUNTIME_AUDIT.md` |
| TASK-015 | Claude Code | Fix validated runtime issues | COMPLETED WITH NOTES | `FINAL_RUNTIME_FIX_REPORT.md` |
| TASK-016 | Claude Code | Realistic demo data and media | COMPLETED | `DEMO_DATA_REPORT.md` |
| TASK-017 | Claude Code (deviation; assigned Codex) | Review populated runtime/demo | COMPLETED WITH NOTES | `FINAL_DEMO_AUDIT.md` |
| TASK-018 | Codex (assigned); performed by Claude Code (deviation) | Repository cleanup audit | COMPLETED WITH NOTES | `REPOSITORY_CLEANUP_AUDIT.md` |
| TASK-019 | Claude Code | Execute repository cleanup | COMPLETED | `REPOSITORY_CLEANUP_REPORT.md` |
| TASK-020 | Claude Code | Rewrite README/final docs | NOT STARTED | Updated `README.md` |
| TASK-021 | Codex | Final submission audit | NOT STARTED | `FINAL_SUBMISSION_AUDIT.md` |

---

# Task Details and Handoffs

## TASK-014 — Final Runtime Audit

**Agent:** Codex  
**Task File:** `TASK-014-CODEX-FINAL-RUNTIME-AUDIT.md`  
**Status:** COMPLETED WITH NOTES  
**Prerequisites:** None  
**Expected Output:** `FINAL_RUNTIME_AUDIT.md`

### Completion Record

- Date: 2026-09-04
- Result: COMPLETED WITH NOTES — overall runtime result `FAIL`; final disposition `READY FOR FIX PHASE`.
- Summary: Restore, Release build, unit tests, migration drift, fresh migration application, Development startup, routes/assets, authorization probes, and production fail-fast configuration passed. The full SQL Server integration run timed out in the demo-data test; that test passed in isolation. The audit recorded three P1 findings, one P2 finding, and three documentation findings.
- Verification: Restore PASS; Release build PASS with 0 warnings/0 errors; unit tests 270/270 PASS; integration tests 189 PASS/1 FAIL/0 skipped (190 total); isolated demo-seed test 1/1 PASS in about 2m30s; no pending EF model changes; all 10 migrations applied to a fresh isolated LocalDB database; Development startup and HTTP/auth/asset probes PASS; production missing-connection fail-fast PASS.
- Files created/changed: `FINAL_RUNTIME_AUDIT.md`; `tasks/FINALIZATION_PROGRESS.md`.
- Blockers/notes: No P0 or audit blocker. TASK-015 must fix the integration-suite reliability issue, Buyer registration/role mismatch, merchant review paging gap, and related query-performance warning. In-app browser visual/console verification was unavailable; HTTP and static checks were completed instead. No application/source code was modified.

### Handoff

If completed successfully:

- Next Task: `TASK-015`
- Next Agent: Claude Code
- Required input for next task: `FINAL_RUNTIME_AUDIT.md`

---

## TASK-015 — Final Runtime Fixes

**Agent:** Claude Code  
**Task File:** `TASK-015-CLAUDE-FINAL-RUNTIME-FIXES.md`  
**Status:** COMPLETED WITH NOTES  
**Prerequisites:** TASK-014 completed  
**Required Input:** `FINAL_RUNTIME_AUDIT.md`  
**Expected Output:** `FINAL_RUNTIME_FIX_REPORT.md`

### Completion Record

- Date: 2026-09-04
- Result: COMPLETED WITH NOTES — all mandatory application checks satisfied; later LocalDB saturation recorded as a non-blocking environment limitation.
- Summary: Normal registration now atomically assigns the Buyer role; B2C policy and service-layer authorization require Buyer or Merchant and exclude Admin/roleless identities; role-specific navigation matches those rules; merchant-owned review history is database-paged with an all-time summary; audited listing aggregate loads use scoped split queries; regression coverage and stale runtime documentation were updated. No schema change or migration was required.
- Verification: Restore PASS; Release build PASS with 0 warnings/0 errors; latest confirmed successful complete test run PASS 464/464 (270 unit + 194 SQL Server integration), 0 failed/0 skipped; focused TASK-015 regressions PASS 3/3; no pending EF model changes; all 10 migrations applied to fresh `Faed_Task015Verification_20260904`; Development startup and demo seed PASS; HTTP smoke PASS for Home, Shop, Register, and protected Merchant Reviews redirect. A later rerun again passed 270/270 unit tests but LocalDB saturated during integration database creation in `MigrateAsync`; affected tests did not reach their bodies.
- Files created/changed: Buyer registration Razor Page; Buyer/Merchant authorization and navigation; order, review, marketplace/listing/inventory/B2B services; Merchant Reviews controller/ViewModel/view; integration fixtures and TASK-015 regressions; `README.md`; `DEPLOYMENT.md`; `PROJECT_STATUS.md`; `FINAL_RUNTIME_FIX_REPORT.md`; this tracker. Full paths are listed in the report.
- Blockers/notes: No application blocker. The later LocalDB creation-time saturation is environmental and does not supersede the earlier complete green run. In-app browser viewport/screenshot review remained unavailable; rendered HTML and live HTTP checks passed. The disposable TASK-015 verification database may remain locally because cleanup/retry was stopped during close-out.

### Handoff

If completed successfully:

- Next Task: `TASK-016`
- Next Agent: Claude Code
- Required input for next task: current repository + `FINAL_RUNTIME_FIX_REPORT.md`

---

## TASK-016 — Realistic Demo Data & Media

**Agent:** Claude Code  
**Task File:** `TASK-016-CLAUDE-REALISTIC-DEMO-DATA.md`  
**Status:** COMPLETED  
**Prerequisites:** TASK-015 completed  
**Expected Output:** `DEMO_DATA_REPORT.md`

### Completion Record

- Date: 2026-09-04
- Result: COMPLETED — the Development/demo database was rebuilt from a clean state and repopulated through the real application services; no blockers.
- Summary: Expanded `DemoDataSeeder` from 4 to 12 listings (11 Live, 1 SoldOut) across the two approved merchants, added two admin-controlled brands, a fourth B2C order scenario (merchant-delivery/OutForDelivery), and one manual inventory adjustment, all still driven through the existing services/business rules and still idempotent (looked-up-by-name brand reuse; existing purge-and-rebuild recovery). Replaced the single hardcoded 1×1 PNG fixture with 19 real, locally generated flat-illustration product images (`tools/demo-images/generate-demo-images.ps1`, System.Drawing — no downloads or hotlinking), wired into the build via a `Content` item in `Faed.Web.csproj` and loaded from disk by `DemoDataSeeder.DemoAssets`. Dropped and recreated the local `Faed` LocalDB database (it held only synthetic Development data — the old demo set plus stray rows from earlier ad hoc runs) and reapplied all 10 migrations cleanly.
- Demo users/roles prepared: Admin (`demo-admin@faed.local`); 2 approved merchants (`merchant-a@faed.local` "Amman Threads", `merchant-b@faed.local` "Petra Footwear"); 1 pending merchant (`pending-merchant@faed.local` "Rainbow Kids Wear"); 2 buyers (`buyer-a@faed.local`, `buyer-b@faed.local`). Shared Development-only password via `Faed:DemoSeed:Password` (user secrets) or `Faed__DemoSeed__Password` (env var); seed gated by `Faed:DemoSeed:Enabled=true` in Development only.
- Listings/media prepared: 12 listings (11 Live, 1 SoldOut) across Clothing/Shoes/Bags & Accessories, all 4 condition grades, 6 of 8 discount reasons, 2 brands (Nova Basics, TrailHead), a low-stock item, a sold-out item, and several multi-variant listings. 19 original locally generated PNG images (~1 MB total) under `src/Faed.Web/Data/Seed/Assets/Images/`, including the defect/packaging photos the app's own business rules require for Grade B/D or PackagingDamage/CosmeticDefect listings.
- Transactions/scenarios prepared: 4 B2C orders (active/Confirmed, completed/reviewed, sold-out/cleared, dispatched/OutForDelivery-via-delivery-zone) plus 1 manual `StockFound` inventory adjustment; 3 B2B scenarios (open negotiation, counter-offer chain, completed deal); 1 dispute (`MissingItems`, `UnderReview`); 1 five-star review.
- Verification: Release build PASS (0 warnings/0 errors); full test suite PASS 464/464 (270 unit + 194 integration, incl. updated `DemoDataSeederTests` covering the new counts, idempotency, and interrupted-run recovery); `Faed` LocalDB dropped and recreated with all 10 migrations applying cleanly; two full app startups in Development confirmed "Demo data set seeded." then "Demo data already present; skipping demo seed." with unchanged row counts; HTTP smoke on `/`, `/Shop`, `/Identity/Account/Register`, `/Merchant/Reviews`, listing detail pages, both merchant storefronts, category filters and search all returned expected results; a fetched listing image confirmed a genuine 900×900 `image/png` response.
- Files created/changed: `src/Faed.Web/Data/Seed/DemoDataSeeder.cs`; `src/Faed.Web/Faed.Web.csproj`; `tests/Faed.IntegrationTests/DemoDataSeederTests.cs`; new `src/Faed.Web/Data/Seed/Assets/Images/*.png` (19 files); new `tools/demo-images/generate-demo-images.ps1`; `DEMO_DATA_REPORT.md`; this tracker.
- Blockers/notes: None blocking. Demo product photography is original locally generated flat-illustration artwork rather than real photography, since no user-supplied images, image-generation tool, or web-fetch capability was available this session — documented in `DEMO_DATA_REPORT.md` §H. The optional "pending listing" moderation scenario from the task file (explicitly optional) was not added; existing pending-merchant and dispute scenarios already exercise the Admin review workflow.

### Handoff

If completed successfully:

- Next Task: `TASK-017`
- Next Agent: Codex
- Required input for next task: current repository + `DEMO_DATA_REPORT.md`

---

## TASK-017 — Demo & Runtime Review

**Agent:** Codex (assigned). **Actually performed by:** Claude Code, at the user's
explicit direction after being asked and choosing to proceed despite the assignment
mismatch. This is a recorded deviation, not a change to Decision #1/#2.  
**Task File:** `TASK-017-CODEX-DEMO-RUNTIME-REVIEW.md`  
**Status:** COMPLETED WITH NOTES  
**Prerequisites:** TASK-016 completed  
**Required Input:** `DEMO_DATA_REPORT.md`  
**Expected Output:** `FINAL_DEMO_AUDIT.md`

### Completion Record

- Date: 2026-09-05
- Result: COMPLETED WITH NOTES — overall result `PASS WITH NOTES`; no P0/P1 code issues found.
- Summary: Verified Release build (0 warnings/0 errors) and the full test suite (464/464 — 270 unit + 194 integration). Verified all 10 migrations apply cleanly to a freshly dropped/recreated Development database with no model drift. Found that the demo accounts left by TASK-016 could not log in with the currently configured `Faed:DemoSeed:Password` secret; root-caused this (with user approval to drop/recreate the local DB) to stale local state — the accounts' stored password reflected whatever secret value was active on a prior run, not necessarily today's value, since `DemoDataSeeder` only sets a password once at account creation and never re-applies it on the idempotent-skip path. After a fresh reseed with the current secret, all 6 demo accounts authenticated successfully via real HTTP form logins, and a second app restart reproduced the idempotent skip with unchanged row counts and continued-working credentials — confirming the seeding/auth mechanism itself has no defect. Performed full black-box role/route verification via authenticated cookie sessions for Anonymous, Buyer, Pending Merchant, Approved Merchant, and Admin, including B2B negotiation/deal, dispute, and moderation-queue pages. No unhandled exceptions or broken assets were observed.
- Routes/roles checked: Anonymous (Home/Shop/Register/Login, storefronts, category/search filters, Live and SoldOut listing detail, listing image), Buyer (Orders, Disputes, Checkout, blocked from Merchant/Admin), Pending Merchant (Verification allowed, Listings/Orders correctly blocked), Approved Merchant (Listings, Orders, Reviews, Deals, Analytics, Inventory, StoreSettings, Offers list/details, blocked from Admin), Admin (dashboard, MerchantVerification, ListingModeration, Disputes incl. detail, Catalog, Reviews, AuditLog, Transactions Orders/Deals, blocked from Merchant). Full detail in `FINAL_DEMO_AUDIT.md` §E.
- Demo/media result: Final seeded counts matched `DEMO_DATA_REPORT.md` exactly (7 users, 3 merchant profiles, 12 listings, 4 orders, 3 B2B negotiations, 1 B2B deal, 1 review, 1 dispute, 2 brands); all core B2C/B2B/dispute/moderation/review workflow scenarios are reachable through the UI; one listing image fetch confirmed a genuine PNG.
- Verification: Release build PASS; full test suite PASS 464/464; `dotnet ef migrations list` shows all 10 migrations; `dotnet ef migrations has-pending-model-changes` reports no drift; `dotnet ef database drop --force` + `dotnet ef database update` PASS on a fresh Development database; two consecutive app starts against that database show "Demo data set seeded." then "Demo data already present; skipping demo seed." with unchanged row counts; no unhandled exceptions/errors in server logs across the full session.
- Files created/changed: `FINAL_DEMO_AUDIT.md`; `tasks/FINALIZATION_PROGRESS.md`. No application/source code was modified (REVIEW ONLY task). The local `Faed` LocalDB was dropped and recreated with a fresh demo seed (disposable Development data only, not source-controlled).
- Blockers/notes: No blocker. One P2 finding (stale local demo login credentials after a database refresh) was fully diagnosed as an environmental/documentation issue, not a code defect — see `FINAL_DEMO_AUDIT.md` §D for root cause and recommendation (a one-sentence reseed note for TASK-020's README, not a fix task). In-app browser visual verification was unavailable this session; HTTP/authenticated-session and direct SQL checks were used instead, consistent with prior tasks.

### Handoff

If completed successfully:

- Next Task: `TASK-018`
- Next Agent: Codex
- Required input for next task: current repository + `FINAL_DEMO_AUDIT.md`

---

## TASK-018 — Repository Cleanup Audit

**Agent:** Codex (assigned). **Actually performed by:** Claude Code, at the user's explicit
direction after being asked and choosing to proceed despite the assignment mismatch — the
same recorded-deviation pattern used for TASK-017. This is a recorded deviation, not a
change to Decision #1/#2.  
**Task File:** `TASK-018-CODEX-REPOSITORY-CLEANUP-AUDIT.md`  
**Status:** COMPLETED WITH NOTES  
**Prerequisites:** TASK-017 completed  
**Expected Output:** `REPOSITORY_CLEANUP_AUDIT.md`

### Completion Record

- Date: 2026-09-05
- Result: COMPLETED WITH NOTES — REVIEW ONLY, no deletions performed; full KEEP/REMOVE/CONSOLIDATE/GENERATED classification produced for every path the task file required, with dependency/stale-reference impact measured directly via `git grep` rather than assumed.
- Summary: Inspected the tracked repository tree (`git ls-files`), confirmed a clean working tree, and confirmed `reference/`, `MANIFEST.json`, `MERGE_NOTES.md`, `QUALITY-CHECK.md`, `.vs/`, `bin/`, `obj/`, and `App_Data/private-storage/` are already untracked/gitignored (no Git action needed or possible on them). Measured how many source files actually cite each finalization-file candidate before recommending removal, and found the master plan's original draft removal list is unsafe to apply as-is for two items: `AGENTS.md` (64 rationale citations across ~50 files under `src/`) and `docs/00-20` + `docs/adr/*` (564 citations across ~150 files) are cited pervasively in code comments as the "why" behind business rules, per `docs/00-SPEC-MAP.md`'s own rule to reference specs instead of duplicating rules in comments. Deleting them as originally drafted would create 600+ dangling references, which directly conflicts with the master plan's own Definition of Done #10 ("no stale references ... remain"). Recommended KEEPING `AGENTS.md` and `docs/00-20`/`docs/adr/*` by default, and confirmed the rest of the originally-drafted removal list (`.claude/`, `CLAUDE.md`, `START_PROMPT.md`, `.github/copilot-instructions.md`, `docs/21`, `docs/22`, `docs/23`, `docs/25`, `tests/`, `PROJECT_STATUS.md`, the finalization-sequence report files) has zero-to-low source-reference cost and can proceed as drafted. Also found and flagged a pre-existing duplicate-numbering issue (`docs/24-DELIVERY-AND-HARDENING.md` and `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` share the "24" prefix) and confirmed no secrets exist in tracked `appsettings.*` files.
- KEEP decisions: `AGENTS.md`; `docs/00-SPEC-MAP.md` … `docs/20-DEVELOPMENT-WORKFLOW.md`; `docs/adr/0001-0007`; `docs/24-DELIVERY-AND-HARDENING.md` (or fold into `DEPLOYMENT.md`); `src/Faed.Web/**`; `Faed.slnx` (edited); `Directory.Build.props`; `.editorconfig`; `.gitignore` (edited); `README.md`/`DEPLOYMENT.md` (rewritten by TASK-020); `.github/workflows/ci.yml` (rewritten to drop test/SQL Server steps, or removed).
- REMOVE decisions: `tests/Faed.UnitTests/`, `tests/Faed.IntegrationTests/` (with `Faed.slnx`/CI updates); `.claude/`; `CLAUDE.md`; `START_PROMPT.md`; `.github/copilot-instructions.md`; `docs/21-CLAUDE-SKILLS-USAGE.md`; `docs/22-VISUAL-STUDIO-BASELINE.md`; `docs/23-GITHUB-REPOSITORY-POLICY.md`; `docs/25-FINAL-VISUAL-DESIGN-POLISH-PLAN.md`; `PROJECT_STATUS.md`; `FINAL_DEMO_AUDIT.md`, `FINAL_RUNTIME_AUDIT.md`, `FINAL_RUNTIME_FIX_REPORT.md`, `DEMO_DATA_REPORT.md`, `REPOSITORY_CLEANUP_AUDIT.md` (once TASK-020/021 have extracted anything still needed for the README); `tasks/` (only as the very last step, once the tracker records the sequence finished — not mid-sequence).
- CONSOLIDATE decisions: `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` — remove the file but first edit its 6 direct source-comment citations to point at a permanent doc instead of a defunct plan file; `docs/23-GITHUB-REPOSITORY-POLICY.md`'s repo-hygiene guidance may be folded into a few sentences of `DEPLOYMENT.md` instead of being deleted outright, at the user's discretion.
- Blockers/notes: No blocker to completing the audit itself. One decision needs explicit user confirmation before TASK-019 deletes anything: whether to accept this audit's recommendation to keep `AGENTS.md`/`docs/00-20`/`docs/adr/*` (default), or override it and accept editing ~600 source comments to remove them as the master plan's draft list originally specified. Full evidence and an ordered execution checklist are in `REPOSITORY_CLEANUP_AUDIT.md` §C–F.

### Handoff

If completed successfully:

- Next Task: `TASK-019`
- Next Agent: Claude Code
- Required input for next task: `REPOSITORY_CLEANUP_AUDIT.md`

---

## TASK-019 — Repository Cleanup Execution

**Agent:** Claude Code  
**Task File:** `TASK-019-CLAUDE-REPOSITORY-CLEANUP.md`  
**Status:** COMPLETED  
**Prerequisites:** TASK-018 completed  
**Required Input:** `REPOSITORY_CLEANUP_AUDIT.md`  
**Expected Output:** `REPOSITORY_CLEANUP_REPORT.md`

### Completion Record

- Date: 2026-09-05
- Result: COMPLETED — the blocking decision from TASK-018 was put to the user first (keep vs.
  remove `AGENTS.md`/`docs/00-20`/`docs/adr/*`); the user chose **keep** (the audit's default
  recommendation), so no large-scale source-comment rewrite was performed. The rest of the
  audit's classification was executed directly.
- Backup/tag created: local-only tag `pre-cleanup-2026-09-05` at the pre-cleanup commit
  (`739591f`), created before any deletion; not pushed.
- Summary: Removed both test projects and their `Faed.slnx` entries; rewrote
  `.github/workflows/ci.yml` to restore+build only; removed `.claude/`, `CLAUDE.md`,
  `START_PROMPT.md`, `.github/copilot-instructions.md`, `docs/21`, `docs/22`, `docs/23`,
  `docs/25`, and `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` (which also resolved a pre-existing
  duplicate "24" numbering collision — only `docs/24-DELIVERY-AND-HARDENING.md` remains "24").
  Fixed every stale source/doc reference this created (13 files: 4 source-comment path
  citations, 6 `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` citations replaced with plain-language
  rationale, `docs/00-SPEC-MAP.md`, `docs/20-DEVELOPMENT-WORKFLOW.md`, `docs/24-DELIVERY-AND-HARDENING.md`,
  and `AGENTS.md`'s own UI-skill/VS-baseline/Git-policy sections). Updated `.gitignore`'s header
  comment to match what's actually kept. Deleted local untracked `bin/`/`obj/` build output;
  `.vs/` was left in place (locked by an open Visual Studio instance, zero repository impact).
  `reference/`, `MANIFEST.json`, `MERGE_NOTES.md`, `QUALITY-CHECK.md` were deliberately left in
  place — untracked, zero effect on the submitted repository, and one file
  (`reference/ORIGINAL-PROJECT-BRIEF-AR.txt`) is irreplaceable with no Git history backing it up.
- Removed: full list in `REPOSITORY_CLEANUP_REPORT.md` §C.
- Retained: `AGENTS.md`, `docs/00-20`, `docs/adr/*` (per the user's decision), `docs/24-DELIVERY-AND-HARDENING.md`,
  `README.md`/`DEPLOYMENT.md` (TASK-020's job), and — deliberately not yet deleted, per the
  audit's own ordering — `PROJECT_STATUS.md`, `FINAL_DEMO_AUDIT.md`, `FINAL_RUNTIME_AUDIT.md`,
  `FINAL_RUNTIME_FIX_REPORT.md`, `DEMO_DATA_REPORT.md`, `REPOSITORY_CLEANUP_AUDIT.md`, and all of
  `tasks/` (still the live coordination file set for this sequence).
- Verification: clean `dotnet restore` PASS (single project, `Faed.Web`); Release build PASS
  (0 warnings/0 errors); `dotnet ef migrations has-pending-model-changes` → no pending changes;
  `dotnet ef migrations list` → all 10 migrations present; `dotnet ef database update` against a
  freshly created, previously-nonexistent LocalDB database (`Faed_Task019CleanupVerification`)
  → all 10 migrations applied cleanly; Development startup smoke test PASS (clean seed/log
  output, "Now listening on..."); HTTP smoke test PASS for `/`, `/Shop`,
  `/Identity/Account/Register`, and the edited `/css/faed.css` (all 200). No test suite was run
  in this task — the test projects were deleted as this task's own first step; the last
  complete test-suite result (464/464 PASS) was already recorded by TASK-017/018 earlier the
  same day, before any source changed here beyond stale-citation comment edits.
- Files created/changed: full list in `REPOSITORY_CLEANUP_REPORT.md` §G. No functional/behavioral
  code changed — every `src/Faed.Web/**` edit is a comment-only fix for a citation to a file this
  task deleted. No new migration was needed. All changes are staged but not committed (per "only
  commit when asked").
- Blockers/notes: No blocker. A `dotnet ef database drop --force` against the existing
  Development database was attempted for a stricter drop-and-recreate test (matching the
  pattern used in TASK-016/017) but was blocked by this session's auto-mode safety classifier
  as a destructive action; verification proceeded instead against a distinct, newly created
  database name, which verifies the same claim without touching existing demo data. `.vs/`
  could not be deleted (locked by Visual Studio); harmless, untracked, no repository impact.

### Handoff

If completed successfully:

- Next Task: `TASK-020`
- Next Agent: Claude Code
- Required input for next task: cleaned repository + `REPOSITORY_CLEANUP_REPORT.md`

---

## TASK-020 — README & Final Documentation

**Agent:** Claude Code  
**Task File:** `TASK-020-CLAUDE-README-FINAL-DOCS.md`  
**Status:** NOT STARTED  
**Prerequisites:** TASK-019 completed  
**Expected Output:** Final `README.md` and retained documentation

### Completion Record

- Date:
- Result:
- Summary:
- Documentation kept:
- Documentation removed/consolidated:
- README setup verified:
- Files created/changed:
- Blockers/notes:

### Handoff

If completed successfully:

- Next Task: `TASK-021`
- Next Agent: Codex
- Required input for next task: cleaned repository + final README/docs

---

## TASK-021 — Final Submission Audit

**Agent:** Codex  
**Task File:** `TASK-021-CODEX-FINAL-SUBMISSION-AUDIT.md`  
**Status:** NOT STARTED  
**Prerequisites:** TASK-020 completed  
**Expected Output:** `FINAL_SUBMISSION_AUDIT.md`

### Completion Record

- Date:
- Result:
- Overall status:
- Build:
- Database/migrations:
- Startup:
- Demo data:
- Role/routes:
- Secrets/repository hygiene:
- Git status:
- Blockers/notes:

### Final Handoff

If all checks pass:

- Project Phase: `FINALIZED`
- Overall Status: `READY FOR SUBMISSION`
- Current Task: `NONE`
- Next Agent: `NONE`
- Next Action: `Commit/push the verified final repository and submit it.`

---

# Important Project Decisions

Keep this section short and factual. Add only decisions that affect later tasks.

1. Codex is used for review/audit tasks.
2. Claude Code is used for implementation tasks.
3. Tests remain in the repository until the last pre-cleanup verification.
4. The user wants test projects removed from the final submitted repository after successful verification.
5. Agent/skill/task-development files should be removed from the final repository after the cleanup audit confirms they are no longer needed.
6. `DemoDataSeeder` should be improved rather than replaced with uncontrolled direct SQL inserts.
7. Realistic local product images should be used for the final demo data.
8. The final README should read like a concise student project README, not an audit log.

---

# Running History

Agents must append one short entry here after every completed/blocked task.

### Example format

```text
[YYYY-MM-DD] TASK-014 — COMPLETED
- Build: PASS
- Tests: PASS
- Migration drift: none
- Main findings: 2 P2 issues
- Output: FINAL_RUNTIME_AUDIT.md
- Next: TASK-015 / Claude Code
```

Do not delete previous entries.

```text
[2026-09-04] TASK-014 — COMPLETED WITH NOTES
- Build: PASS (Release, 0 warnings, 0 errors)
- Tests: Unit PASS 270/270; integration FAIL 189/190 with one demo-seed timeout; isolated demo-seed test PASS 1/1
- Migration drift: none; all 10 migrations applied to a fresh isolated database
- Main findings: 3 P1 issues, 1 P2 issue, 3 documentation issues; no P0
- Output: FINAL_RUNTIME_AUDIT.md
- Next: TASK-015 / Claude Code

[2026-09-04] TASK-015 — COMPLETED WITH NOTES
- Build: PASS (Release, 0 warnings, 0 errors)
- Tests: latest confirmed complete run PASS 464/464 (270 unit + 194 SQL Server integration), 0 failed, 0 skipped
- Database: no model drift or new migration; all 10 migrations applied to a fresh isolated database; Development/demo seed PASS
- Environment note: a later rerun hit LocalDB saturation in test-database `MigrateAsync` before affected test bodies; unit tests remained 270/270 PASS
- Output: FINAL_RUNTIME_FIX_REPORT.md
- Next: TASK-016 / Claude Code

[2026-09-04] TASK-016 — COMPLETED
- Build: PASS (Release, 0 warnings, 0 errors)
- Tests: PASS 464/464 (270 unit + 194 SQL Server integration), 0 failed, 0 skipped, incl. updated demo-seeder idempotency/recovery coverage
- Database: `Faed` LocalDB dropped and recreated clean; all 10 migrations applied, no drift, no new migration
- Demo data: 12 listings (11 Live/1 SoldOut), 2 brands, 4 B2C order scenarios, 3 B2B scenarios, 1 dispute, 1 review, 19 locally generated product images
- Main findings: none blocking; demo images are original generated artwork, not real photography (no image tool/web access available)
- Output: DEMO_DATA_REPORT.md
- Next: TASK-017 / Codex

[2026-09-05] TASK-017 — COMPLETED WITH NOTES (performed by Claude Code, assigned Codex; deviation approved by user)
- Build: PASS (Release, 0 warnings, 0 errors)
- Tests: PASS 464/464 (270 unit + 194 integration), 0 failed, 0 skipped
- Migrations: all 10 present/applied; no model drift; clean drop+recreate PASS
- Seeding: fresh seed PASS ("Demo data set seeded."); idempotent restart PASS ("Demo data already present; skipping demo seed."); row counts match DEMO_DATA_REPORT.md exactly
- Main findings: 1 P2 (non-blocking, diagnosed, no code fix needed) — demo accounts left by TASK-016 could not log in against the DemoSeed password currently in secrets.json; root-caused to stale local state (password is only set once at account creation and not re-applied on the idempotent-skip path), confirmed by a fresh reseed where all 6 demo accounts then logged in successfully
- Role/route checks: Anonymous, Buyer, Pending Merchant, Approved Merchant, Admin all verified via real authenticated HTTP sessions; authorization boundaries correct in both directions; no unhandled exceptions or broken assets
- Output: FINAL_DEMO_AUDIT.md
- Next: TASK-018 / Codex

[2026-09-05] TASK-018 — COMPLETED WITH NOTES (performed by Claude Code, assigned Codex; deviation approved by user)
- Mode: REVIEW ONLY, no deletions
- Classified every path the task file required as KEEP/REMOVE/CONSOLIDATE/GENERATED-LOCAL-ONLY
- Main finding: the master plan's draft removal list for AGENTS.md and docs/00-20/adr is unsafe as written — 64 and 564 source-comment citations respectively would go stale, conflicting with the plan's own "no stale references" Definition of Done; recommended KEEPING those files by default instead
- Everything else in the original draft removal list (.claude/, CLAUDE.md, START_PROMPT.md, copilot-instructions.md, docs/21-23, docs/25, tests/, PROJECT_STATUS.md) confirmed safe to remove with low/zero source impact
- Also flagged: duplicate "24" numbering between docs/24-DELIVERY-AND-HARDENING.md and docs/24-FINAL-UI-UX-COMPLETION-PLAN.md; no secrets found in tracked appsettings files
- Output: REPOSITORY_CLEANUP_AUDIT.md
- Next: TASK-019 / Claude Code (needs user confirmation on the AGENTS.md/docs KEEP-vs-REMOVE decision before deletions begin)

[2026-09-05] TASK-019 — COMPLETED
- User decision: KEEP AGENTS.md and docs/00-20/adr (audit's default recommendation); no large-scale comment rewrite performed
- Removed: both test projects + Faed.slnx entries; CI rewritten to restore+build only; .claude/, CLAUDE.md, START_PROMPT.md, .github/copilot-instructions.md; docs/21, 22, 23, 25, and docs/24-FINAL-UI-UX-COMPLETION-PLAN.md (resolving the duplicate "24" numbering); local bin/obj build output
- Stale references fixed: 13 files (4 source-comment path citations, 6 docs/24-FINAL-UI-UX-COMPLETION-PLAN.md citations, docs/00-SPEC-MAP.md, docs/20, docs/24-DELIVERY-AND-HARDENING.md, AGENTS.md's own UI-skill/VS-baseline/Git-policy sections, .gitignore header)
- Deliberately not yet deleted (per audit ordering): PROJECT_STATUS.md, FINAL_*.md, DEMO_DATA_REPORT.md, REPOSITORY_CLEANUP_AUDIT.md, tasks/
- Build: PASS (Release, 0 warnings, 0 errors); restore now resolves a single project
- Migrations: no pending model changes; all 10 present; applied cleanly to a fresh isolated database
- Startup/HTTP smoke: PASS (Development startup clean; /, /Shop, /Identity/Account/Register, /css/faed.css all 200)
- No test suite run this task (test projects deleted as step 1); last full-suite result was TASK-017/018's 464/464 PASS, same day
- Backup: local-only tag pre-cleanup-2026-09-05, not pushed
- Output: REPOSITORY_CLEANUP_REPORT.md
- Next: TASK-020 / Claude Code
```

---

# Quick Start Instruction for Any Agent

When the user says:

> Start the next task.

Do the following automatically:

1. Read `FINALIZATION_PROGRESS.md`.
2. Identify `Current Task`.
3. Open its task file.
4. Open prerequisite report(s) listed in this tracker.
5. Execute the task.
6. Update this tracker before finishing.

Do not ask the user to paste previous results if the required report files already exist in the repository.
