# CLAUDE.md

This repository uses `AGENTS.md` as the canonical coding-agent instruction file.

Before doing any work:

1. Read `AGENTS.md`.
2. Read all files under `/docs`.
3. Read the active task under `/tasks`.
4. Follow the source-of-truth precedence in `AGENTS.md`.

Do not treat this file as a separate product specification.


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
