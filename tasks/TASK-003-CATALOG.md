# TASK-003 — Catalog Foundations

## Objective
Create the DB-driven taxonomy and disclosure reference data required by Fashion Overstock:
hierarchical `Category`, `ConditionGrade`, `DiscountReason`, optional `Brand`, one migration,
and an idempotent runtime seed. No listings, variants, or reference-price evidence (TASK-004).

## Read first
- `docs/04-DOMAIN-MODEL.md` §2 (catalog entities), §11 (indexes)
- `docs/03-BUSINESS-RULES.md` §3 and `docs/adr/0003-CONDITION-VS-DISCOUNT-REASON.md`
- `docs/01-PRD.md` §6–7 (grades, discount reasons)
- `docs/12-SEED-DATA.md` (reference data)
- `docs/13-OPEN-QUESTIONS.md` items 4–6
- `docs/14-FUTURE-EXPANSION.md` (multi-sector taxonomy principle)

## Architecture (do not deviate)

Faed is a **single-project organized ASP.NET Core MVC** application. All code for this task
goes inside `src/Faed.Web`:

- entities -> `Models/Entities`, enums -> `Models/Enums`
- EF Core configuration, migrations and seed -> `Data/` (`Configurations/`, `Migrations/`, `Seed/`)
- business logic -> `Services/` (use-case methods; may use `ApplicationDbContext` directly)
- public MVC endpoints -> `Controllers/`; role-specific screens -> `Areas/Admin`, `Areas/Merchant`, `Areas/Buyer`
- UI/input models -> `ViewModels/` (keep separate from entities)

Do not create separate Domain, Application, or Infrastructure projects. Do not introduce
Repository Pattern, UnitOfWork, CQRS, or MediatR. Keep controllers thin. See `AGENTS.md`
section 5 and `docs/adr/0006-SINGLE-PROJECT-MVC.md`.

## Decisions (resolved for this task)
- **Category**: seed the `Fashion Overstock` root plus the three launch categories only
  (Clothing, Shoes, Bags & Accessories). Deeper taxonomy is deferred (open question 4); the
  lower-level tree in `docs/12-SEED-DATA.md` is dev/demo data for a later task.
- **Brand**: optional everywhere, admin-controlled only — no merchant-authored brands
  (open questions 5–6). Minimal entity: `Id`, `Name`, `Slug`, `IsActive`. No brands seeded.
- **ConditionGrade**: DB reference table, not an enum. Grades A–D only; no Grade E.
- **DiscountReason**: DB reference table. Seed all 8 `docs/01-PRD.md` §7 reasons, including
  `OtherApprovedReason`.
- **Seeding**: runtime idempotent seeder (same pattern as `IdentityDataSeeder`), invoked at
  startup. Schema is applied manually via `dotnet ef database update`; the seeder must no-op
  cleanly whether tables are empty or already populated.
- **Multi-sector**: the sector is a `Category` row, never an enum or hard-coded constant. No
  sector name in business logic.
- **Slugs**: `Category.Slug` and `Brand.Slug` are globally unique (DB unique index) and are
  display/routing identifiers only, never used for authorization.
- **Admin**: no catalog management UI in this task. Full admin catalog management is TASK-010.

## Deliverables
- `Category` entity (self-referencing `ParentCategoryId`, `Name`, `Slug`, `IsActive`,
  `SortOrder`) + configuration + unique slug index
- `ConditionGrade` reference table (`Code` A–D, `Name`, `Description`, `SortOrder`, `IsActive`)
- `DiscountReason` reference table (`Code`, `Name`, `Description`, `IsActive`)
- `Brand` entity (`Name`, `Slug`, `IsActive`) + unique slug index
- one EF Core migration covering the above
- idempotent runtime catalog seeder wired into startup
- no UI

## Required seed
- Categories: `Fashion Overstock` → `Clothing`, `Shoes`, `Bags & Accessories`
- Condition grades: A, B, C, D (`docs/01-PRD.md` §6)
- Discount reasons (8, `docs/01-PRD.md` §7): Overstock, Past Season, Customer Return,
  Display Item, Packaging Damage, Cosmetic Defect, Missing Non-Essential Item,
  Other Approved Reason

## Critical rules
- `ConditionGrade` and `DiscountReason` stay separate, with no FK between them (`docs/adr/0003`).
- Grade E must not appear in seed or schema.
- No category, grade, or reason value is hard-coded in code or views — all read from the DB.

## Required tests
- Running the seeder twice produces no duplicate rows (idempotency).
- Category hierarchy: root has null parent; the three launch categories reference the root.
- `Category.Slug` and `Brand.Slug` unique constraints are enforced at the database.
- Exactly grades A–D are seeded; no Grade E.
- `ConditionGrade` and `DiscountReason` persist independently (no FK/coupling).
- A second root category can be added by data alone with no schema change.

## Exit criteria
- [ ] Migration applies from an empty database.
- [ ] Seed runs repeatedly without duplication.
- [ ] Condition and discount reason are separate; Grade E is absent.
- [ ] No catalog/condition/reason values are hard-coded in code or views.
- [ ] Catalog unit/integration tests pass.
- [ ] `dotnet build` succeeds; `PROJECT_STATUS.md` updated.
