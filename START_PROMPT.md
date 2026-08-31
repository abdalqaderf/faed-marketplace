# Start Prompt — Faed

You are the coding agent responsible for implementing **Faed**.

The developer has intentionally created the initial `Faed.Web` project manually in Visual Studio using the ASP.NET Core MVC template with **.NET 10** and **Individual Accounts / ASP.NET Core Identity**.

## Critical rule

**Do not recreate `Faed.Web`.**

Before changing architecture or writing new foundation code, your first task is to **audit the Visual Studio-generated baseline** and prove that it is correctly created and usable.

## Before writing code

1. Read `AGENTS.md` completely.
2. Read every file under `/docs`.
3. Read `tasks/TASK-001-FOUNDATION.md` completely.
4. Treat those files as the source of truth.
5. Inspect the existing Visual Studio-generated solution/project.
6. Run the mandatory Phase 0 baseline audit from TASK-001.
7. Do not implement later phases early.
8. Do not invent unresolved business rules.

## Baseline behavior

If the Visual Studio baseline has a fundamental problem such as:
- wrong framework;
- wrong template;
- Identity not selected;
- missing `Faed.Web`;
- destructive/nested solution structure;
- baseline cannot build;

mark the task `BLOCKED`, explain the smallest correction, and stop before building on a bad foundation.

If the baseline is fundamentally correct but needs a safe architectural correction such as migrating the generated EF provider to SQL Server, report it as `PASS WITH SAFE CORRECTIONS` and proceed deliberately.

## Then

Execute **TASK-001 only**.

Preserve working generated MVC/Identity behavior while completing the documented clean modular monolith structure around the existing Web project.

## Required completion response

Provide:

1. baseline audit result (`PASS`, `PASS WITH SAFE CORRECTIONS`, or `BLOCKED`);
2. what Visual Studio generated;
3. any corrections made and why;
4. exact final project/file structure;
5. migrations created/changed;
6. Identity/DbContext final arrangement;
7. build result;
8. test result;
9. authentication verification result;
10. all important files changed;
11. deviations from the specification;
12. recommended next task.

The site UI is English-only for the MVP.

Do not implement `TASK-002` or later tasks unless explicitly requested.
