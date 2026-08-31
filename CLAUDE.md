# CLAUDE.md

This repository uses `AGENTS.md` as the canonical coding-agent instruction file.

Before doing any work:

1. Read `AGENTS.md`.
2. Read all files under `/docs`.
3. Read the active task under `/tasks`.
4. Follow the source-of-truth precedence in `AGENTS.md`.

Do not treat this file as a separate product specification.

## Architecture

Faed uses a **single-project organized ASP.NET Core MVC** architecture. All production
application code lives inside `src/Faed.Web`:

- `Models/Entities` for persisted entities, `Models/Enums` for enums
- `Data` for EF Core, `DbContext`, configurations, migrations, and seed data
- `Services` for business logic (may use `ApplicationDbContext` directly)
- `Controllers` for public MVC endpoints
- `Areas/Admin`, `Areas/Merchant`, `Areas/Buyer` for role-specific functionality
- `ViewModels` for UI/input models

Do not create separate Domain, Application, or Infrastructure projects. Do not introduce
Repository Pattern, UnitOfWork, CQRS, or MediatR unless a future requirement explicitly
justifies it. Controllers must remain thin. See `AGENTS.md` section 5 and
`docs/adr/0006-SINGLE-PROJECT-MVC.md`.


## Project skills

This repository provides project-specific skills under `.claude/skills/`.

When a task involves UI or UX, load the relevant project skill(s) in addition to any
available built-in Claude skills such as `/modern-web-guidance`, `/design-system`,
`/design-critique`, `/accessibility-review`, and `/ux-copy`.

## Existing Visual Studio baseline

Before executing TASK-001, assume the developer has already created `Faed.Web`
in Visual Studio with .NET 10 MVC + Individual Accounts.

Do not recreate the web project.

Run the baseline audit defined in `tasks/TASK-001-FOUNDATION.md` and
`docs/22-VISUAL-STUDIO-BASELINE.md` before restructuring anything.

If the baseline has a fundamental error, stop and report it instead of building
on top of it.
