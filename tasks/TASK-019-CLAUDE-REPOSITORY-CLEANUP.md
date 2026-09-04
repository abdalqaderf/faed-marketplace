# TASK-019 — Repository Cleanup Execution

**Agent:** Claude Code  
**Mode:** IMPLEMENTATION.

## Input

Follow the approved `REPOSITORY_CLEANUP_AUDIT.md`.

## Safety gate

Before deletion:

1. confirm the final pre-cleanup verification passed;
2. create a local backup or tag/branch that preserves the pre-cleanup project;
3. do not push backup branches if the user does not want them in the submitted repository.

## Required cleanup

Subject to the audit, remove:

- `.vs/`;
- all `bin/` and `obj/`;
- test projects (`tests/`) as requested;
- test project entries from `Faed.slnx`;
- test-only CI steps and test-only configuration;
- `.claude/`;
- `AGENTS.md`;
- `CLAUDE.md`;
- `START_PROMPT.md`;
- `.github/copilot-instructions.md`;
- obsolete `tasks/`;
- historical agent/development status files;
- local packaging/reference artifacts;
- any other file marked REMOVE.

Consolidate or remove docs that no longer add submission value.

## Critical consistency work

After deletions:

- remove stale references to deleted tests, agent files, skills, tasks, and old plans;
- update `.gitignore` comments;
- ensure the solution contains only real project(s);
- if GitHub Actions is retained, make it a clean restore/build workflow that does not reference deleted test projects;
- keep migrations, application source, required static assets, demo seed assets, and essential setup documentation.

## Verification

Run:
- clean restore;
- Release build;
- EF pending-model-change check;
- migrations against a fresh SQL Server database;
- Development startup.

Produce `REPOSITORY_CLEANUP_REPORT.md` with removed files, retained files, and verification results.
