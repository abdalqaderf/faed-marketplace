# Project Status

## Current state

**Specification and agent skill system complete.**

The developer will manually create the initial `Faed.Web` project in Visual Studio before the coding agent begins implementation.

Expected Visual Studio baseline:

- ASP.NET Core Web App (Model-View-Controller)
- .NET 10
- Individual Accounts / ASP.NET Core Identity
- HTTPS enabled
- project name: `Faed.Web`

## Active task

`tasks/TASK-001-FOUNDATION.md`

TASK-001 now begins with a mandatory audit of the Visual Studio-generated project before the agent changes architecture.

## Locked product choices

- English MVP website
- Amman
- Fashion Overstock launch
- Clothing / Shoes / Bags & Accessories
- Verified merchants only as sellers
- B2C + B2B
- no real online payment
- no platform shipping
- no warehouse/fleet
- no used goods
- no Grade E

## Important architecture decisions

- pragmatic clean modular monolith
- existing Visual Studio `Faed.Web` is adopted, not recreated
- ASP.NET Core Identity preserved and then integrated cleanly
- one application DbContext in Infrastructure as final target
- SQL Server
- variant/SKU-level stock
- Condition ≠ DiscountReason
- B2B Negotiation ≠ B2B Deal
- Order + OrderItems for B2C
- SQL Server rowversion concurrency

## Next action

1. create/clone the GitHub repository;
2. place the authoritative project files in the repository root;
3. create `Faed.Web` manually in Visual Studio according to `docs/22-VISUAL-STUDIO-BASELINE.md`;
4. build/run the untouched Visual Studio baseline once;
5. commit the clean generated baseline;
6. open the repository root in VS Code;
7. use `START_PROMPT.md`;
8. execute TASK-001 only.

## Prepared execution queue

Full task queue from `TASK-001` through `TASK-011` is ready.

Only TASK-001 is active initially.
