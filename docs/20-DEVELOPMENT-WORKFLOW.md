# 16 — Development Workflow

## Before every task

1. Read `AGENTS.md`.
2. Read `PROJECT_STATUS.md`.
3. Read the active task file under `/tasks` (for example `tasks/TASK-001-FOUNDATION.md`).
4. Inspect existing code and migrations.
5. Identify whether the task requires a product decision from `13-OPEN-QUESTIONS.md`.

## During implementation

- Keep commits/changes phase-scoped.
- Do not mix unrelated refactors with feature work.
- Add migrations only when the active task needs schema changes.
- Add tests with the business rule they protect.
- Update documentation only when the implemented behavior intentionally changes the spec.

## After every task

Update `PROJECT_STATUS.md`:
- phase;
- completed task;
- migrations;
- tests;
- known issues;
- next task.

## Database workflow

All EF Core code (entities in `Models/`, `ApplicationDbContext`, configurations, migrations,
seed) lives in `src/Faed.Web`. Run `dotnet ef` with `--project src/Faed.Web`.

For every schema change:
1. update entity/configuration (`Models/` + `Data/Configurations/`);
2. add migration into `Data/Migrations/` (`dotnet ef migrations add <Name> --project src/Faed.Web`);
3. inspect generated migration;
4. apply to clean/dev database (`dotnet ef database update --project src/Faed.Web`);
5. run relevant tests;
6. never edit production schema manually without a migration.

## Agent stop rule

The agent must stop at the task's exit criteria.

It may recommend the next task, but must not implement it unless explicitly instructed to continue.

## Change-control rule

If the product owner requests a change to a locked decision:
1. state the impacted docs/models;
2. update the relevant source-of-truth docs;
3. add/update an ADR if architectural;
4. then change code.

Do not let code silently become the only place where a product decision lives.


## UI workflow

For any UI task:

1. read the relevant task file;
2. apply the Faed-specific UI direction, commerce, dashboard, and accessibility rules
   established in earlier phases (see `07-UI-UX-SPEC.md`);
3. build the page/flow;
4. check responsive and accessibility behavior before calling the page done;
5. revise until the page no longer looks generic, default Bootstrap, or obviously AI-generated.


## Foundation workflow

Before TASK-001:
1. initialize/clone the GitHub repository;
2. commit the specification/docs/skills;
3. create `Faed.Web` manually in Visual Studio using .NET 10 MVC + Individual Accounts;
4. build/run the untouched baseline;
5. commit the clean Visual Studio baseline;
6. open the repository root in VS Code;
7. execute TASK-001.

TASK-001 must audit the Visual Studio baseline before changing architecture.
