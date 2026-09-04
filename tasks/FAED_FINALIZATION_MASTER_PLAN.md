# Faed — Finalization Master Plan

## Goal

Take the current Faed project from the post-Phase-One state to a clean, working, realistic, submission-ready repository.

The process is intentionally split between two agents:

- **Codex:** audit/review only. It must not modify code unless explicitly told otherwise.
- **Claude Code:** implementation, fixes, data preparation, cleanup, and documentation updates.


## Progress coordination

`FINALIZATION_PROGRESS.md` is the single handoff file between Codex and Claude Code. Every agent must read it before starting, use it to determine the current task and prerequisite reports, and update it before finishing. This allows the user to simply say **Start the next task** without manually copying previous results.

## Rule: do not clean before verification

Do not delete tests, agent files, skills, old docs, or project history until the application has passed the final pre-cleanup verification.

The `tests/` directory is intentionally kept until the last technical verification. After it passes, create a local backup/tag, then remove the test projects and every stale reference to them if the final submission should not contain tests.

## Current demo-data capability

The project already contains `DemoDataSeeder`.

It currently creates, through application services:

- demo Admin;
- two approved Merchants;
- one pending Merchant;
- two Buyers;
- four Listings;
- listing variants and inventory;
- B2C orders;
- B2B negotiations and a completed deal;
- a dispute;
- a review.

The current seeded listing images are tiny 1x1 PNG fixtures, so the final demo-data task must replace them with realistic local product media and improve the storefront data.

Prefer the existing seeder/application services over direct SQL inserts. Direct database writes should only be used for diagnostics or when the implemented application API cannot express required seed data.

## Sequence

### TASK-014 — Final Runtime Audit — Codex
Review only. Verify restore/build/tests/migrations/startup/current known gaps. Produce a findings report.

### TASK-015 — Final Runtime Fixes — Claude Code
Implement only validated issues from TASK-014. Re-run the technical verification.

### TASK-016 — Realistic Demo Data & Media — Claude Code
Upgrade the existing deterministic demo seed with realistic catalog content and real local product images. Keep it Development-only and idempotent.

### TASK-017 — Demo & Runtime Review — Codex
Review the populated application, role flows, storefront data, navigation, and runtime behavior. No edits.

### TASK-018 — Repository Cleanup Audit — Codex
Inventory the repository and classify every non-source artifact as KEEP / REMOVE / CONSOLIDATE / GENERATED. No deletions.

### TASK-019 — Repository Cleanup Execution — Claude Code
After a backup/tag, perform the approved cleanup. Remove tests if requested, remove agent/skill files, remove generated folders, remove development-history files, repair the solution/workflow/docs, and leave no stale references.

### TASK-020 — README & Final Documentation — Claude Code
Rewrite the README as a concise student project README and retain only documentation that adds submission value.

### TASK-021 — Final Submission Audit — Codex
Audit a clean checkout after cleanup: build, migrations, startup, demo data, core role flows, secrets, broken references, repository tree, and Git status.

## Recommended final repository shape

The exact tree must be decided by TASK-018, but the target should be close to:

```text
Faed/
├── .github/
│   └── workflows/
│       └── build.yml          # optional but useful
├── src/
│   └── Faed.Web/
├── docs/                      # only concise submission-value docs, if retained
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── Faed.slnx
├── README.md
└── DEPLOYMENT.md              # only if still useful after README rewrite
```

Expected removal candidates after audit:

```text
.vs/
bin/
obj/
tests/                         # only after final pre-cleanup verification
.claude/
AGENTS.md
CLAUDE.md
START_PROMPT.md
.github/copilot-instructions.md
tasks/
PROJECT_STATUS.md
MERGE_NOTES.md
MANIFEST.json
QUALITY-CHECK.md
reference/
historical UI/implementation plans that no longer add submission value
```

Do not blindly delete the list above. TASK-018 must first confirm that no retained source file or final documentation still depends on it.

## Definition of Done

The repository is submission-ready only when:

1. A clean checkout restores and builds.
2. The current migrations create/update the SQL Server database correctly.
3. There are no pending model changes before test removal.
4. The app starts without unhandled exceptions.
5. Public, Buyer, Merchant, and Admin core flows are usable.
6. Demo data is realistic enough for presentation.
7. Product images render correctly.
8. No broken navigation or obvious role leakage exists.
9. No secrets or machine-specific paths are committed.
10. No stale references to deleted tests/agents/skills remain.
11. README setup steps are sufficient for another person to run the project.
12. Git status is clean.
