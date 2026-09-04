# Faed — Finalization Progress Tracker

> This file is the single coordination point between Codex and Claude Code.
> Every agent must read this file before starting work and update it before finishing.
> Do not rely on chat history when this file and the repository are available.

---

## Current State

**Project Phase:** Finalization after Phase One submission  
**Overall Status:** IN PROGRESS  
**Current Task:** TASK-016  
**Next Agent:** Claude Code  
**Next Action:** Prepare realistic demo data and local product media using the current repository and `FINAL_RUNTIME_FIX_REPORT.md`; do not revisit TASK-015 unless a regression is found.

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
| TASK-016 | Claude Code | Realistic demo data and media | NOT STARTED | `DEMO_DATA_REPORT.md` |
| TASK-017 | Codex | Review populated runtime/demo | NOT STARTED | `FINAL_DEMO_AUDIT.md` |
| TASK-018 | Codex | Repository cleanup audit | NOT STARTED | `REPOSITORY_CLEANUP_AUDIT.md` |
| TASK-019 | Claude Code | Execute repository cleanup | NOT STARTED | `REPOSITORY_CLEANUP_REPORT.md` |
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
**Status:** NOT STARTED  
**Prerequisites:** TASK-015 completed  
**Expected Output:** `DEMO_DATA_REPORT.md`

### Completion Record

- Date:
- Result:
- Summary:
- Demo users/roles prepared:
- Listings/media prepared:
- Transactions/scenarios prepared:
- Verification:
- Files created/changed:
- Blockers/notes:

### Handoff

If completed successfully:

- Next Task: `TASK-017`
- Next Agent: Codex
- Required input for next task: current repository + `DEMO_DATA_REPORT.md`

---

## TASK-017 — Demo & Runtime Review

**Agent:** Codex  
**Task File:** `TASK-017-CODEX-DEMO-RUNTIME-REVIEW.md`  
**Status:** NOT STARTED  
**Prerequisites:** TASK-016 completed  
**Required Input:** `DEMO_DATA_REPORT.md`  
**Expected Output:** `FINAL_DEMO_AUDIT.md`

### Completion Record

- Date:
- Result:
- Summary:
- Routes/roles checked:
- Demo/media result:
- Verification:
- Files created/changed:
- Blockers/notes:

### Handoff

If completed successfully:

- Next Task: `TASK-018`
- Next Agent: Codex
- Required input for next task: current repository + `FINAL_DEMO_AUDIT.md`

---

## TASK-018 — Repository Cleanup Audit

**Agent:** Codex  
**Task File:** `TASK-018-CODEX-REPOSITORY-CLEANUP-AUDIT.md`  
**Status:** NOT STARTED  
**Prerequisites:** TASK-017 completed  
**Expected Output:** `REPOSITORY_CLEANUP_AUDIT.md`

### Completion Record

- Date:
- Result:
- Summary:
- KEEP decisions:
- REMOVE decisions:
- CONSOLIDATE decisions:
- Blockers/notes:

### Handoff

If completed successfully:

- Next Task: `TASK-019`
- Next Agent: Claude Code
- Required input for next task: `REPOSITORY_CLEANUP_AUDIT.md`

---

## TASK-019 — Repository Cleanup Execution

**Agent:** Claude Code  
**Task File:** `TASK-019-CLAUDE-REPOSITORY-CLEANUP.md`  
**Status:** NOT STARTED  
**Prerequisites:** TASK-018 completed  
**Required Input:** `REPOSITORY_CLEANUP_AUDIT.md`  
**Expected Output:** `REPOSITORY_CLEANUP_REPORT.md`

### Completion Record

- Date:
- Result:
- Backup/tag created:
- Summary:
- Removed:
- Retained:
- Verification:
- Files created/changed:
- Blockers/notes:

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
