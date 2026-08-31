# Faed — Surplus Inventory Marketplace

**Faed** is a specialized marketplace for surplus and non-perfect merchant inventory in Jordan.

A verified merchant lists inventory once and can sell from the same stock through:

- `B2C` — individual buyers purchase units.
- `B2B` — verified merchants negotiate and buy quantities/lots.

Faed is not a general classifieds platform. Its product identity is built around structured condition disclosure, verified business sellers, quantity integrity, trusted transactions, and inventory-recovery analytics.

## MVP

- Market: Amman, Jordan
- UI: English
- Currency: JOD
- Sellers: verified merchants only
- Buyers: individuals + verified merchants
- Launch sector: Fashion Overstock
- Launch categories:
  - Clothing
  - Shoes
  - Bags & Accessories

## Tech

- ASP.NET Core MVC / .NET 10 LTS
- Entity Framework Core + SQL Server
- ASP.NET Core Identity
- Razor Views + Bootstrap 5 + JavaScript
- Modular monolith / clean project boundaries
- SQL Server `rowversion` for stock concurrency

## Read before coding

1. `AGENTS.md` — engineering contract and precedence.
2. `docs/00-SPEC-MAP.md` — map of every specification file.
3. All files under `/docs` in numeric order.
4. `tasks/TASK-001-FOUNDATION.md` — first executable task.

## Start

Give your coding agent the contents of `START_PROMPT.md`, or simply tell it:

> Read `AGENTS.md` and execute `tasks/TASK-001-FOUNDATION.md`.

The full implementation task queue (`TASK-001` through `TASK-011`) is included under `/tasks`.

The `/reference` directory is historical context only.


## Claude skills

This repository includes project-specific Claude skills under:

```text
.claude/skills/
```

Use them together with any relevant built-in/workspace Claude skills available in your account.
See:
- `docs/21-CLAUDE-SKILLS-USAGE.md`

## Visual Studio-first foundation workflow

The initial `Faed.Web` project is intentionally created manually in Visual Studio using:

- ASP.NET Core MVC
- .NET 10
- Individual Accounts / Identity
- HTTPS

Then the coding agent executes TASK-001.

TASK-001 does **not** recreate the Web project. It begins with a mandatory baseline audit and adopts the generated project.

See:
- `docs/22-VISUAL-STUDIO-BASELINE.md`
- `docs/23-GITHUB-REPOSITORY-POLICY.md`
- `tasks/TASK-001-FOUNDATION.md`
