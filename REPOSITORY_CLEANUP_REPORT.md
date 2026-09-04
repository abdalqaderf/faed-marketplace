# Repository Cleanup Report

**Task:** TASK-019 — Repository Cleanup Execution
**Agent:** Claude Code
**Mode:** IMPLEMENTATION — files deleted, moved, and edited as described below.
**Date:** 2026-09-05
**Prerequisite:** TASK-018 (`REPOSITORY_CLEANUP_AUDIT.md`) — COMPLETED WITH NOTES, followed as
the required input, with one decision resolved by the user before execution (see §A).

---

## A. Decision resolved before execution

The audit flagged one blocking decision: whether to keep or remove `AGENTS.md`,
`docs/00-SPEC-MAP.md`–`docs/20-DEVELOPMENT-WORKFLOW.md`, and `docs/adr/*`, given that removing
them would strand ~600 source-comment citations across ~150 files in `src/Faed.Web`.

**The user chose to keep them** (the audit's default recommendation). `AGENTS.md` and
`docs/00`–`20` + `docs/adr/*` remain in the repository as the internal engineering-contract
reference; no source-comment citations of them were touched, and no large-scale comment
rewrite was performed. `CLAUDE.md` was still removed — it is Claude-Code-specific routing,
distinct from `AGENTS.md`, with zero references from `src/`/`tests/`.

---

## B. Safety gate

1. Confirmed the final pre-cleanup verification was green: a Release build run before any
   deletion succeeded with 0 warnings/0 errors (matching TASK-017/018's already-recorded
   464/464 test PASS from the same day).
2. Created a local backup tag `pre-cleanup-2026-09-05` at the pre-cleanup commit
   (`739591f`), before any deletion. **Not pushed** — kept local-only per the safety gate's
   own instruction not to push backup branches unless the user wants them in the submitted
   repository.
3. No destructive Git history operations were performed; all removals are plain `git rm` /
   file deletions recorded as a normal, invertible-via-backup-tag change set.

---

## C. Removed

### Test projects (Decision #3/#4 — tests remained until this final pre-cleanup verification)
- `tests/Faed.UnitTests/` (20 files)
- `tests/Faed.IntegrationTests/` (35 files, incl. `Support/`)
- The two `<Project Path="tests/...csproj" />` entries removed from `Faed.slnx`
- `.github/workflows/ci.yml` rewritten: dropped the SQL Server service container and both
  `dotnet test` steps; now `restore` + `build` only

### Agent/skill/task-orchestration files (zero source references, per audit §D)
- `.claude/` (6 skills + `.claude/skills/README.md`)
- `.github/copilot-instructions.md`
- `CLAUDE.md`
- `START_PROMPT.md`

### Docs outside `docs/00-SPEC-MAP.md`'s official read map
- `docs/21-CLAUDE-SKILLS-USAGE.md`
- `docs/22-VISUAL-STUDIO-BASELINE.md`
- `docs/23-GITHUB-REPOSITORY-POLICY.md` (not folded into `DEPLOYMENT.md` — its content was
  specifically about committing the now-removed agent/skill files, so it was moot rather than
  worth preserving)
- `docs/25-FINAL-VISUAL-DESIGN-POLISH-PLAN.md` (1,862-line completed-task plan)
- `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` (1,216-line completed-task plan; this also resolved
  the pre-existing duplicate "24" numbering collision with `docs/24-DELIVERY-AND-HARDENING.md`,
  which is now the only "24" file)

### Local-only, untracked build/IDE output (deleted from disk; no Git action was possible or needed)
- Root `bin/`, `obj/`
- `src/Faed.Web/bin/`, `src/Faed.Web/obj/`
- `tests/*/bin/`, `tests/*/obj/` (removed along with the rest of `tests/`)

**Not deleted:** `.vs/` — locked by an open Visual Studio instance on this machine
(`Device or resource busy`); it is untracked and has zero effect on the submitted repository,
so this is a local cosmetic item only, safe to delete by hand later. `reference/`,
`MANIFEST.json`, `MERGE_NOTES.md`, `QUALITY-CHECK.md` were also left in place: they are
untracked, `.gitignore`d, and confirmed by the audit to have zero effect on the submitted
repository either way, and `reference/ORIGINAL-PROJECT-BRIEF-AR.txt` in particular is the
original, irreplaceable stakeholder brief with no Git history backing it up — deleting local,
zero-impact, irreplaceable files did not seem worth the irreversibility. Recommend the user
delete these by hand only if they specifically want local tidiness.

---

## D. Retained (per audit §C and §D)

- `AGENTS.md` — kept per the user's decision in §A.
- `docs/00-SPEC-MAP.md` – `docs/20-DEVELOPMENT-WORKFLOW.md`, `docs/adr/0001`–`0007` — kept.
- `docs/24-DELIVERY-AND-HARDENING.md` — kept (not folded into `DEPLOYMENT.md`; that remains an
  option for TASK-020 if desired, but was not required by this task).
- `src/Faed.Web/**` — unaffected production application code, aside from the comment edits in
  §E.
- `Faed.slnx`, `Directory.Build.props`, `.editorconfig` — kept; `Faed.slnx` edited (§C).
- `README.md`, `DEPLOYMENT.md` — kept as-is; their rewrite is TASK-020's job, not this one.
- `PROJECT_STATUS.md`, `FINAL_DEMO_AUDIT.md`, `FINAL_RUNTIME_AUDIT.md`,
  `FINAL_RUNTIME_FIX_REPORT.md`, `DEMO_DATA_REPORT.md`, `REPOSITORY_CLEANUP_AUDIT.md` — **not
  deleted in this task**, matching the audit's own ordered checklist (§F step 12): TASK-020
  still needs to extract anything from them (demo accounts, known limitations) into the final
  README, and TASK-021 needs to close the tracker before these finalization-sequence artifacts
  add no further value. Deleting them now would remove information TASK-020 needs.
- `tasks/` (the whole directory, incl. this tracker) — **not deleted**, per the audit's explicit
  instruction to remove it only as the very last step, once `FINALIZATION_PROGRESS.md` itself
  records the sequence as finished. It is still the live coordination file set for this task.

---

## E. Critical consistency work — stale references removed

After the deletions above, every remaining tracked file was grepped for the exact paths/names
removed (`CLAUDE.md`, `.claude/`, `START_PROMPT`, `copilot-instructions`, `docs/21`–`23`,
`docs/25`, `docs/24-FINAL-UI-UX-COMPLETION-PLAN`, the two test `.csproj` names), excluding the
files in §D that are deliberately deferred to TASK-020/021. The following genuine dangling
references were found and fixed (all other ~600 `AGENTS.md`/`docs/00-20`/`adr` citations across
`src/` were left untouched per §A):

| File | Change |
|---|---|
| `src/Faed.Web/Services/Listings/InventoryService.cs:29` | Dropped the literal `.claude/skills/faed-dashboard-ux` path from a comment; kept the underlying rationale in plain language. |
| `src/Faed.Web/Services/Listings/MerchantListingService.cs:86` | Same. |
| `src/Faed.Web/Services/Ordering/OrderService.cs:463` | Same. |
| `src/Faed.Web/wwwroot/css/faed.css:1,739` | Dropped the `.claude/skills/faed-ui-direction` path from two header comments; kept the `docs/07-UI-UX-SPEC.md` and `tasks/TASK-005...` citations, which remain valid. |
| `src/Faed.Web/Areas/Identity/Pages/Account/AccessDenied.cshtml` (+ `.cshtml.cs`) | Replaced the `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` citation with the underlying rationale in plain language (no fabricated citation to a doc section that doesn't actually contain this specific rule). |
| `src/Faed.Web/Services/Listings/IInventoryService.cs`, `IMerchantListingService.cs` | Same — replaced the `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md §8` citation with plain-language rationale. |
| `src/Faed.Web/Views/Home/Privacy.cshtml`, `src/Faed.Web/Views/Shared/Error.cshtml` | Same. |
| `docs/00-SPEC-MAP.md` | Removed the "Project skill system" section (pointed at deleted `.claude/skills/`) and the "Foundation environment" section (pointed at deleted `docs/22`/`docs/23`). |
| `docs/20-DEVELOPMENT-WORKFLOW.md` | Rewrote the "UI workflow" section to stop instructing agents to load `.claude/skills/` and Claude-specific slash commands; replaced with a plain pointer to `docs/07-UI-UX-SPEC.md`. |
| `docs/24-DELIVERY-AND-HARDENING.md` | Dropped the `.claude/skills/faed-*` citation from the responsive/accessibility section; kept the `docs/07-UI-UX-SPEC.md` citation and the historical narrative. |
| `AGENTS.md` §11 | Rewrote "UI skill system" (a full list of now-deleted Claude skill names and slash commands) into a short "UI rules" section pointing at `docs/07-UI-UX-SPEC.md`. |
| `AGENTS.md` "Visual Studio baseline ownership" → See: | Removed the `docs/22-VISUAL-STUDIO-BASELINE.md` bullet (file deleted); kept the `tasks/TASK-001-FOUNDATION.md` bullet (file still present). |
| `AGENTS.md` "Git and repository policy" | Removed `CLAUDE.md`, `.claude/skills/`, `.github/copilot-instructions.md`, and `/tasks` from the "Commit:" list (the first three no longer exist; `/tasks` will be removed only at the very end of the finalization sequence per §D) and removed the trailing `docs/23-GITHUB-REPOSITORY-POLICY.md` citation (file deleted). |
| `.gitignore` | Rewrote the header comment to list only `AGENTS.md`, `docs/`, and `tasks/` as intentionally version-controlled (dropped `CLAUDE.md`, `.claude/skills/`, `.github/copilot-instructions.md`, which are gone); removed the now-meaningless `.claude/settings.local.json` ignore rule. |

A final repository-wide grep for all of the removed paths/names (excluding the deliberately
deferred files in §D) returned no further matches.

---

## F. Verification

All run against the post-cleanup working tree, in order:

1. **Clean restore** — `dotnet restore Faed.slnx` → succeeded; restored only
   `src/Faed.Web/Faed.Web.csproj` (confirms the solution now contains a single real project).
2. **Release build** — `dotnet build Faed.slnx --no-restore --configuration Release` →
   **Build succeeded. 0 Warning(s). 0 Error(s).**
3. **EF pending-model-change check** — `dotnet ef migrations has-pending-model-changes` →
   "No changes have been made to the model since the last migration."
4. **Migrations list** — `dotnet ef migrations list` → all 10 expected migrations present, from
   `20260831174908_InitialIdentity` through `20260903162224_HardenDisputeInvariants`.
5. **Migrations against a fresh SQL Server database** — `dotnet ef database update` against a
   newly created, previously-nonexistent LocalDB database
   (`Faed_Task019CleanupVerification`) → all 10 migrations applied cleanly, no errors. (A
   destructive `dotnet ef database drop` against the existing `Faed` Development database was
   attempted first for a stricter drop-and-recreate test, per the pattern used in TASK-016/017,
   but was blocked by this session's auto-mode safety classifier as a destructive action; using
   a distinct, never-before-existing database name achieves the same "migrations apply cleanly
   to a fresh database" verification without touching the existing demo data or requiring
   further destructive-action approval.)
6. **Development startup smoke test** — `dotnet run --project src/Faed.Web --configuration
   Release` with `ASPNETCORE_ENVIRONMENT=Development` against the existing `Faed` database →
   started cleanly: Identity/catalog seed queries ran, "Development Admin admin@faed.local
   already present", "Demo data seed skipped: set Faed:DemoSeed:Enabled=true (Development only)
   to enable it." (expected — demo seed is disabled in this environment's config, unrelated to
   the cleanup), "Now listening on: http://localhost:5074", "Application started." No
   unhandled exceptions.
7. **HTTP smoke test** — with the app running: `GET /` → 200, `GET /Shop` → 200,
   `GET /Identity/Account/Register` → 200, `GET /css/faed.css` (the edited stylesheet) → 200.
   The process was then stopped.

No test-suite run was performed in this task — the test projects were deleted in step 1 of the
cleanup itself, matching Decision #3/#4 ("tests remain until the last pre-cleanup
verification," which was already recorded as PASS 464/464 by TASK-017/018 earlier the same day,
before any code changed in this task beyond comment edits).

---

## G. Files changed

**Deleted:** see §C for the full list (test projects, `.claude/`, `CLAUDE.md`, `START_PROMPT.md`,
`.github/copilot-instructions.md`, `docs/21`, `docs/22`, `docs/23`, `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md`,
`docs/25`, local `bin/`/`obj/`).

**Modified:** `Faed.slnx`, `.github/workflows/ci.yml`, `.gitignore`, `AGENTS.md`,
`docs/00-SPEC-MAP.md`, `docs/20-DEVELOPMENT-WORKFLOW.md`, `docs/24-DELIVERY-AND-HARDENING.md`,
`src/Faed.Web/Areas/Identity/Pages/Account/AccessDenied.cshtml` (+`.cshtml.cs`),
`src/Faed.Web/Services/Listings/IInventoryService.cs`,
`src/Faed.Web/Services/Listings/IMerchantListingService.cs`,
`src/Faed.Web/Services/Listings/InventoryService.cs`,
`src/Faed.Web/Services/Listings/MerchantListingService.cs`,
`src/Faed.Web/Services/Ordering/OrderService.cs`,
`src/Faed.Web/Views/Home/Privacy.cshtml`, `src/Faed.Web/Views/Shared/Error.cshtml`,
`src/Faed.Web/wwwroot/css/faed.css`.

**Created:** `REPOSITORY_CLEANUP_REPORT.md` (this file). `REPOSITORY_CLEANUP_AUDIT.md` (created
by TASK-018, still uncommitted from that task) is included in this task's staged change set.

**No functional/behavioral code change was made** — every `src/Faed.Web/**` edit in this task is
a comment or documentation-comment change (removing a stale file citation); no method body,
route, view markup, or business logic changed. No new migration was needed or created.

All of the above is currently **staged but not committed** — this task did not commit or push,
consistent with "only commit when the user asks."

---

## H. Blockers/notes

No blocker. Two non-blocking notes:

1. This session's auto-mode safety classifier blocked a `dotnet ef database drop --force`
   against the existing Development database (see §F step 5); the equivalent verification was
   completed instead against a freshly created, distinct database name, which is a strictly
   safer test of the same claim ("migrations apply cleanly to a fresh database") without risk
   to the existing seeded demo data. If a literal drop-and-recreate of the `Faed` database is
   still wanted, the user can run `dotnet ef database drop --project src/Faed.Web --force`
   themselves and confirm.
2. `.vs/` could not be deleted — locked by an open Visual Studio instance. It is untracked and
   has no effect on the submitted repository; delete it by hand once Visual Studio is closed,
   if desired.

The one blocking decision from TASK-018 (§A) was resolved by the user before this task began
executing deletions, per the workflow rule.

---

## Handoff

If completed successfully:

- Next Task: `TASK-020`
- Next Agent: Claude Code
- Required input for next task: cleaned repository + `REPOSITORY_CLEANUP_REPORT.md`
