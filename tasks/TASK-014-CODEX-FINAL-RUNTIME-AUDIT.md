# TASK-014 — Final Runtime Audit

**Agent:** Codex  
**Mode:** REVIEW ONLY — DO NOT MODIFY FILES.

## Objective

Audit the current Faed repository before any cleanup or deletion. Treat the current code as the source of truth.

## Required checks

1. Read the current `README`, `PROJECT_STATUS`, relevant docs, solution, project files, configuration, DbContext, migrations, seeders, controllers/services, authorization, and tests.
2. Run, where the environment supports it:
   - `dotnet restore Faed.slnx`
   - `dotnet build Faed.slnx -c Release`
   - all Unit tests
   - all Integration tests with an isolated SQL Server test database
   - `dotnet ef migrations has-pending-model-changes`
3. Apply the full migration chain to a fresh SQL Server database.
4. Start the web app in Development and verify startup completes without unhandled exceptions.
5. Verify the existing `DemoDataSeeder` can run idempotently.
6. Re-check known risk areas:
   - Buyer role semantics / registration behavior;
   - Merchant Reviews collection paging;
   - authentication/authorization boundaries;
   - navigation by role;
   - startup and database configuration;
   - missing routes/views/assets;
   - migration drift.
7. Do not treat old status documents or passing historical tests as proof if the current runtime disagrees.

## Output

Create `FINAL_RUNTIME_AUDIT.md` with:

- PASS / FAIL summary;
- exact commands run;
- build result;
- test result;
- database/migration result;
- startup result;
- findings grouped as P0 / P1 / P2 / Documentation;
- exact file paths and code locations;
- recommended fix for each finding;
- a final statement: `READY FOR FIX PHASE` or `BLOCKED`.

Do not edit source code.
