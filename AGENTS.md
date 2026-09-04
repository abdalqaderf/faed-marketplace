# AGENTS.md — Faed Engineering Contract

> This file is the highest-priority repository instruction for coding agents.

## 1. Product in one sentence

**Faed** is a specialized marketplace for verified merchants in Jordan to recover value from surplus and non-perfect inventory by selling the same stock to individual buyers (`B2C`) or to other verified merchants (`B2B`) through structured, trusted workflows.

The MVP launches in **Amman** with **Fashion Overstock** only, while the domain and architecture must remain ready for future sectors.

---

## 2. Source-of-truth order

When instructions conflict, use this precedence:

1. `AGENTS.md`
2. `docs/00-SPEC-MAP.md`
3. `docs/01-PRD.md`
4. `docs/02-SCOPE-AND-DECISIONS.md`
5. `docs/03-BUSINESS-RULES.md`
6. `docs/04-DOMAIN-MODEL.md`
7. `docs/05-USER-FLOWS-AND-STATE-MACHINES.md`
8. `docs/06-ARCHITECTURE.md`
9. `docs/07-UI-UX-SPEC.md`
10. `docs/08-SECURITY-AND-PRIVACY.md`
11. `docs/09-TEST-STRATEGY.md`
12. `docs/10-IMPLEMENTATION-PLAN.md`
13. `docs/11-ACCEPTANCE-CRITERIA.md`
14. `docs/12-SEED-DATA.md`
15. `docs/13-OPEN-QUESTIONS.md`
16. `docs/14-FUTURE-EXPANSION.md`
17. `docs/15-GLOSSARY.md`
18. `docs/16-PERMISSIONS-MATRIX.md`
19. `docs/17-DATA-INVARIANTS.md`
20. `docs/18-TRACEABILITY.md`
21. `docs/19-CODING-CONVENTIONS.md`
22. `docs/20-DEVELOPMENT-WORKFLOW.md`
23. `docs/adr/*`

The files under `/reference` are historical context only and are **not authoritative**.

If a requested change conflicts with an authoritative rule, flag the conflict before changing the rule.

---

## 3. Non-negotiable product rules

### Marketplace identity
- Faed is **not** a general classifieds website.
- Individuals can buy but **cannot sell**.
- Only verified merchants can sell.
- The platform does not own inventory.
- The platform does not physically inspect inventory in the MVP.
- The platform does not operate warehouses or a delivery fleet in the MVP.

### Launch scope
- Geography: **Amman, Jordan**.
- Launch sector: **Fashion Overstock**.
- Initial top-level launch categories:
  1. Clothing
  2. Shoes
  3. Bags & Accessories
- Do not expose unrelated sectors in the MVP UI.
- Do not hard-code the platform so future sectors require a core schema rewrite.

### English UI
- All system UI copy, navigation, routes, validation messages, email templates, admin labels, and status labels are **English-only for the MVP**.
- Free-text merchant/customer content must remain Unicode-safe.
- The architecture may be localization-ready, but do not build an Arabic UI unless explicitly requested.
- Currency: `JOD`, stored with 3 decimal places.
- Store timestamps in UTC; display them for `Asia/Amman`.

### Trust
- Merchant verification is mandatory before listing creation.
- During validation-stage MVP, every new/edited listing that materially changes condition, pricing, or product identity requires admin moderation before becoming public.
- Implement listing moderation so the policy can later change without redesigning the listing model.
- Reviews are allowed only after a transaction reaches `Completed`.
- Defects must be disclosed and visually evidenced where applicable.

---

## 4. Four architecture rules that must not be broken

### Rule A — Inventory lives at sellable Variant/SKU level
Do **not** keep only one quantity on `Listing`.

Example:

- Black / M = 4
- Black / L = 2
- White / M = 3

These are distinct sellable variants and must have independent stock and concurrency protection.

### Rule B — Condition is not discount reason
Keep these separate.

Examples:

- `Condition = A`, `DiscountReason = PastSeason`
- `Condition = B`, `DiscountReason = PackagingDamage`

A past-season product may be physically perfect.

### Rule C — B2B negotiation is not the accepted deal
Do not use one status enum to represent both negotiation and fulfillment.

Model:
- negotiation;
- offer revisions/counter-offers;
- accepted deal;
- fulfillment.

`OfferExpiresAt` and `ReservationExpiresAt` are different timestamps.

### Rule D — B2C uses Order + OrderItems
An order belongs to exactly one merchant in the MVP but may contain multiple items/variants from that merchant.

Do not implement a multi-merchant cart/order.

---

## 5. Technical baseline

Use:

- `.NET 10 LTS`
- `ASP.NET Core MVC`
- `Entity Framework Core`
- `SQL Server`
- `ASP.NET Core Identity`
- `Razor Views`
- `Bootstrap 5`
- vanilla `JavaScript`
- cloud object storage behind an interface
- email provider behind an interface
- `Git` / `GitHub`

Architecture: **single-project organized ASP.NET Core MVC**, not microservices and not
a multi-project Domain/Application/Infrastructure split (see `docs/adr/0006-SINGLE-PROJECT-MVC.md`).

Faed uses a single-project organized ASP.NET Core MVC architecture.

All production application code lives inside `src/Faed.Web`.

Do not create separate Domain, Application, or Infrastructure projects.

Use:
- `Models/Entities` for persisted entities
- `Models/Enums` for enums
- `Data` for EF Core, `DbContext`, configurations, migrations, and seed data
- `Services` for business logic
- `Controllers` for public MVC endpoints
- `Areas/Admin`, `Areas/Merchant`, and `Areas/Buyer` for role-specific functionality
- `ViewModels` for UI/input models

Target solution structure:

```text
Faed.slnx
src/
  Faed.Web/
    Areas/{Admin,Merchant,Buyer,Identity}/
    Controllers/
    Models/{Entities,Enums,Identity}/
    ViewModels/
    Data/{ApplicationDbContext.cs,Configurations/,Migrations/,Seed/}
    Services/
    Authorization/
    Views/
    wwwroot/
tests/
  Faed.UnitTests/
  Faed.IntegrationTests/
```

The tests reference `Faed.Web` directly.

Use one EF Core application `DbContext`. Migrations live in `src/Faed.Web/Data/Migrations`.

Controllers must remain thin. Services may use `ApplicationDbContext` directly.

Do not introduce:
- microservices;
- Repository Pattern / generic repository abstractions;
- UnitOfWork;
- MediatR;
- CQRS infrastructure;
- event buses;
- Redis;
- Elasticsearch;
unless a future requirement explicitly justifies it.

---

## 6. Coding rules

- Nullable reference types enabled.
- Async I/O throughout.
- Thin controllers.
- Business logic in `Services` (not in controllers or Razor views).
- Razor views receive ViewModels, never EF entities directly.
- Use `decimal`, never floating point, for money.
- Configure money columns as `decimal(18,3)`.
- Use enums/value objects for stable workflow states; use reference tables for admin-manageable catalog/condition/reason data.
- Use UTC internally.
- No magic strings for roles/statuses.
- No secrets in source control.
- No public URL to merchant verification documents.
- Validate all uploaded files.
- All authorization is enforced server-side.
- Never trust browser-supplied:
  - merchant identity;
  - unit price;
  - totals;
  - stock quantity;
  - review eligibility;
  - workflow status.

---

## 7. Inventory and concurrency rules

Every quantity-bearing SKU/variant must include optimistic concurrency (`rowversion` / `[Timestamp]`).

Critical stock changes must execute transactionally.

At minimum test:
1. two B2C requests for the last unit;
2. B2C order competing with accepted B2B deal;
3. two B2B accept attempts competing for the same stock;
4. reservation release.

Never use EF Core InMemory or SQLite as proof that SQL Server `rowversion` concurrency works.

---

## 8. Moderation and private documents

Merchant verification documents are private.

Store only a protected object key/metadata in the DB. Admin access must be authorized and audited.

Listing moderation must preserve:
- merchant draft;
- submitted version;
- moderation decision;
- rejection reason;
- approved/public version semantics.

Do not let a merchant edit a live listing's identity/condition/price and bypass review.

---

## 9. Definition of Done for every feature

A feature is not done until:

- functional acceptance criteria pass;
- server-side authorization is present;
- validation is present;
- happy path and meaningful failure paths are covered;
- mobile-first view checked;
- English UI copy is consistent;
- sensitive data is not exposed;
- schema migration is included if needed;
- automated tests exist for important business rules;
- critical state transitions are logged/auditable;
- project builds with no errors;
- relevant documentation is updated.

For stock-sensitive features, passing the SQL Server concurrency test is mandatory.

---


## 11. UI rules

When a task touches UI, UX, Razor views, CSS, responsive layout, forms, dashboards,
product cards, listing pages, or merchant/admin screens, apply `docs/07-UI-UX-SPEC.md`
in full, including its mobile-first, accessibility, and commerce-presentation rules.

Before declaring any UI task complete, re-check responsive behavior and accessibility,
and revise until the page no longer looks generic, default Bootstrap, or obviously
AI-generated.


## Visual Studio baseline ownership

The developer intentionally creates `Faed.Web` manually in Visual Studio before `TASK-001`.

Expected baseline:
- ASP.NET Core Web App (Model-View-Controller);
- .NET 10;
- Individual Accounts / ASP.NET Core Identity;
- HTTPS enabled;
- project name `Faed.Web`.

`TASK-001` must **audit and adopt** this project.

Agents must:
- inspect the generated project before changing it;
- verify the baseline builds/runs;
- verify Identity was generated;
- preserve working generated authentication;
- create only missing solution projects;
- avoid duplicate MVC/Identity projects.

Agents must not:
- recreate `Faed.Web`;
- overwrite generated Identity blindly;
- silently repair a fundamentally wrong Visual Studio baseline;
- proceed if the baseline has a blocking setup error.

See:
- `tasks/TASK-001-FOUNDATION.md`

## Git and repository policy

Project instructions are source-controlled.

Commit:
- `AGENTS.md`;
- `/docs`;
- application source;
- migrations;
- non-secret configuration.

Do not commit:
- build output;
- IDE user state;
- credentials/secrets;
- local uploads/private storage;
- local databases;
- packaging/reference artifacts excluded by `.gitignore`.

## 12. Agent behavior

Before coding:
1. Read this file.
2. Read all `/docs`.
3. Read the current task file under `/tasks`.
4. Inspect the current code before proposing structural changes.

During coding:
- Work in the phase order from `docs/10-IMPLEMENTATION-PLAN.md`.
- Do not scaffold future phases "while you're here".
- Prefer the smallest complete vertical increment.
- Do not silently resolve an unresolved product decision.
- Record major deviations as a new ADR.

After coding, report:
1. files changed;
2. migrations;
3. tests executed and results;
4. behavior implemented;
5. unresolved blockers;
6. next recommended task.

Start with `tasks/TASK-001-FOUNDATION.md`.
