# TASK-015 — Final Runtime Fixes

**Agent:** Claude Code  
**Mode:** IMPLEMENTATION.

## Input

Use `FINAL_RUNTIME_AUDIT.md` from TASK-014 as the primary fix list.

## Objective

Fix the validated issues required for a reliable academic/project submission without broad refactoring.

## Rules

- Fix only real issues confirmed by the audit.
- Preserve current architecture and business rules.
- Do not delete tests or agent files yet.
- Do not redesign the whole UI.
- Do not change the database schema unless a confirmed defect requires it.
- Update migrations only if a real model/schema fix requires them.
- Keep the project runnable on SQL Server.

## Required focus

At minimum, resolve any confirmed issue around:

- Buyer-role behavior/consistency;
- Merchant Reviews paging if it is still a growing unpaged collection;
- role-specific navigation;
- broken links/views;
- runtime exceptions;
- validation or authorization regressions;
- database/migration drift;
- obvious responsive/runtime defects found by the audit.

## Verification

After fixes:

- restore;
- Release build;
- all tests;
- pending-model-change check;
- clean-database migration;
- Development startup;
- demo-seed run.

Create `FINAL_RUNTIME_FIX_REPORT.md` listing:
- changes made;
- files changed;
- commands/results;
- remaining limitations.
