# TASK-018 — Repository Cleanup Audit

**Agent:** Codex  
**Mode:** REVIEW ONLY — NO DELETIONS.

## Objective

Classify every repository artifact so the final submission contains only files that add value.

## Required classification

Classify files/directories as:

- `KEEP`
- `REMOVE`
- `CONSOLIDATE`
- `GENERATED / LOCAL ONLY`

At minimum inspect:

- `.vs/`
- all `bin/` and `obj/`
- `tests/`
- `.claude/`
- `AGENTS.md`
- `CLAUDE.md`
- `START_PROMPT.md`
- `.github/copilot-instructions.md`
- `.github/workflows/`
- `tasks/`
- `PROJECT_STATUS.md`
- `MERGE_NOTES.md`
- `MANIFEST.json`
- `QUALITY-CHECK.md`
- `reference/`
- `docs/`
- `DEPLOYMENT.md`
- `README.md`
- `.gitignore`
- `.editorconfig`
- solution/project files
- seed assets
- local database/storage files.

## Special requirement: tests

The user wants the final submitted repository not to contain tests.

Before recommending removal, confirm:

1. the final pre-cleanup build/tests passed;
2. no production project references a test project;
3. removing tests only requires:
   - deleting `tests/`;
   - removing test projects from `Faed.slnx`;
   - updating/removing test CI steps;
   - removing stale test references from README/docs/comments where needed.

Recommend a local backup/tag before deletion.

## Agent/skill removal

Identify every file that exists mainly for Codex/Claude/agent operation and can be removed from the final student repository. Also identify all documentation that references those files so no stale references remain.

## Output

Create `REPOSITORY_CLEANUP_AUDIT.md` with:

- current repository tree summary;
- KEEP/REMOVE/CONSOLIDATE table;
- dependency/stale-reference impact for each removal;
- recommended final tree;
- exact ordered deletion/update checklist.

Do not delete anything.
