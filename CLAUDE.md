# CLAUDE.md — Faed

Rules for working in this repository. Read before making any change.

## What this project is

Faed is a marketplace where verified, subscribed merchants in Amman sell **open-box and
ex-display** appliances and power tools. Every listing discloses its condition and the reason
for the discount. Buyers reserve online and pay cash at pickup — **the platform never handles
money**.

`docs/CORE.md` is the scope. `docs/BUSINESS-MODEL.md` is the reasoning.
`docs/PHASE-PLAN.md` is the current work plan.
`docs/COMMITS-AND-COMMENTS.md` is how commits are written and what a comment is for —
follow it for every commit message and every comment you write.

**Acceptance test for any feature:** does it serve this sentence?

> A verified, subscribed merchant publishes an open-box or ex-display item with a disclosed
> condition, discount reason and defect photo; an admin reviews it; a buyer reserves it, then
> inspects and pays cash at pickup — with no overselling when two orders arrive at once.

If not, it does not belong in Phase 1.

## Branch

All Phase-1 work happens on **`faed-core`**. `main` holds the previous two-sided version and
must not be touched.

## Commands

```bash
dotnet build Faed.slnx
dotnet test                                        # tests/Faed.Web.Tests
dotnet run --project src/Faed.Web
dotnet ef migrations add <Name> --project src/Faed.Web
dotnet ef database update --project src/Faed.Web
dotnet ef database drop -f --project src/Faed.Web
```

The app does **not** migrate on startup. It seeds roles and reference data idempotently.

## Invariants — never break these

1. **`rowversion` on stock quantities.** `ListingVariant` uses an EF `[Timestamp]` concurrency
   token. Two simultaneous reservations must never oversell the same unit. Most listings are a
   single physical item, so this is the core correctness property of the system.
2. **Order snapshots.** `OrderItem` stores price, title, variant and condition as they were at
   order time. Editing a listing must never change a past order.
3. **No physical deletes of transactional history.** Orders, reviews and listings that orders
   reference are archived or deactivated, never removed. Configure FK delete behaviour
   deliberately.
4. **Moderation history is append-only.** Each review decision is a new `ListingModeration`
   row. Never update or delete a previous decision.
5. **Business rules live in the entity, not the service.** `Listing` is a rich domain model
   with encapsulated collections and behaviour methods (`SubmitForReview`, `Approve`,
   `DescribeSubmissionBlockers`). Keep it that way — do not turn entities into property bags
   and move logic into services.
6. **Nothing waits on a human forever.** Every state that depends on a person acting has a
   deadline and an automatic outcome, executed by a hosted background service.
7. **Publishing requires two independent conditions:** approved verification **and** an active
   subscription. Check both; do not collapse them into one flag.
8. **Money never touches the platform.** No payment entities, no balances, no escrow.

## Layout

```
src/Faed.Web/
  Areas/{Admin,Merchant,Buyer,Identity}/   role-scoped controllers + views
  Controllers/                             public endpoints
  Models/{Entities,Enums,Identity}/        EF Core domain model
  ViewModels/                              view-facing shapes
  Data/                                    DbContext, Configurations/, Migrations/, Seed/
  Services/<Area>/                         business logic, one folder per domain area
  Authorization/                           policy names + handlers
  Rendering/                               display helpers only, no logic
  wwwroot/                                 css/faed.css, js/, images/
tests/Faed.Web.Tests/                      xUnit
docs/
```

## Conventions

- Nullable reference types are on. Do not suppress warnings to make code compile.
- Money is `decimal(18,3)` — JOD has three decimal places.
- Timestamps are UTC (`...AtUtc`); display conversion happens in `Rendering/AmmanTime.cs`.
- Entity configuration goes in `Data/Configurations/`, not in `OnModelCreating`.
- Services return a `Result`-style outcome for expected failures; `DomainException` is for
  invariant violations that indicate a bug.
- Reference data (categories, condition grades, discount reasons, subscription plans) is
  **seeded and admin-editable** — never hard-coded in business logic.
- UI language is **English only**. No localisation framework.

## Do not add

- New NuGet packages without being asked. The stack is fixed: ASP.NET Core MVC on .NET 10,
  EF Core, SQL Server, Identity, Razor, Bootstrap 5, vanilla JS.
- A frontend framework or build step.
- In-app messaging or chat. Merchant↔buyer contact is a `wa.me` deep link.
- Payment processing, escrow, wallets, balances.
- B2B merchant-to-merchant features. That module was removed deliberately; its design is
  archived in `docs/B2B-DESIGN.md`.
- A dispute system. Deferred — buyers inspect before paying.
- Auctions, shipping providers, warehouses, ERP sync.
- Annual plans, discounts, trials, or a free tier.
- Fields on the merchant listing form. It is nine — eight that ask the merchant to decide
  something, plus an optional description. Adding one needs a decision, not a commit. The test
  is not the count: does the new field make the merchant *reason* about something? A text box
  does not; a second taxonomy overlapping an existing one does, and that is what is banned.

## UI principles

`docs/DESIGN-BRIEF.md` is the authority on colour, type and the visual system from Phase 11
onward — tokens, the ink/rust colour rule, and the condition-stamp treatment are locked there.
Read it before touching any Razor view or `faed.css` in Phase 11 or 12.

- The merchant listing form is **one page**: eight decision fields plus an optional
  description, last. Do not split it or add sections.
- Merchants never see the words "variant", "SKU", "condition grade" or "discount reason".
  One condition question derives the grade and the reason.
- The merchant sees two lifecycle words only: **Pause** and **Delete**.
- Buyers **Reserve**; they do not "check out". No cart-and-payment language anywhere.
- The shop has four filters: category, condition, price range, text search. No advanced
  filter drawer.
- Validation is inline and immediate, never a list of errors after the fact.
- Every list that pages, pages — never infinite scroll. A page link preserves the search
  query, the filters, the sort, the route values, the current page and the total count.
  Pagination is one shared partial, not a per-page reimplementation.

## Working style

- One phase per session. Do not start the next phase without being asked.
- After every phase: `dotnet build` and `dotnet test` must both pass before you report done.
- Prefer deleting code over adding flags. This project is being reduced, not extended.
- Delete code, never comment it out — commented-out code still matches the phase greps.
- No `TODO` comments. `docs/PHASE-PLAN.md` is the backlog.
- If a change requires a decision the docs do not cover, stop and ask. Do not invent a rule.
