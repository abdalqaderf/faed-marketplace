# TASK-013 — Final Visual Design & Polish

## Objective

Complete the final visual-design layer of Faed after `TASK-012`.

This is a **real visual polish task**.

The application is already functionally mature. The remaining goal is to make it look like a polished, cohesive, trustworthy marketplace rather than an ASP.NET Core / Bootstrap application with custom content.

Detailed execution plan:

`docs/25-FINAL-VISUAL-DESIGN-POLISH-PLAN.md`

Read and execute that file completely.

---

## Read first

- `AGENTS.md`
- `PROJECT_STATUS.md`
- `docs/07-UI-UX-SPEC.md`
- `docs/03-BUSINESS-RULES.md`
- `docs/05-USER-FLOWS-AND-STATE-MACHINES.md`
- `docs/08-SECURITY-AND-PRIVACY.md`
- `docs/16-PERMISSIONS-MATRIX.md`
- `docs/21-CLAUDE-SKILLS-USAGE.md`
- `docs/25-FINAL-VISUAL-DESIGN-POLISH-PLAN.md`
- all relevant `.claude/skills/**/SKILL.md`

Use current source as implementation truth.

---

## Mandatory skills

Use:

- `faed-ui-direction`
- `faed-commerce-ux`
- `faed-marketplace-pages`
- `faed-dashboard-ux`
- `faed-responsive-accessibility`
- `faed-ui-quality-gate`

---

## Primary visual problems to solve

The final UI must no longer visibly resemble default ASP.NET Core / Bootstrap.

Specifically address:

- default-looking global Navbar;
- minimal/default Footer;
- ASP.NET template CSS residue;
- default blue Bootstrap/Identity styling;
- Login/Register/Manage visual disconnect;
- weak global visual hierarchy;
- public marketplace polish;
- product-card polish;
- Shop/filter presentation;
- Listing Details e-commerce presentation;
- Buyer CRUD-like pages;
- Merchant workspace consistency;
- Admin workspace consistency;
- responsive visual behavior;
- hover/focus/active states.

The before/after visual difference must be immediately noticeable.

---

## Hard boundaries

Do not change:

- Business Logic;
- domain/state semantics;
- database model;
- authorization;
- permissions;
- privacy rules;
- B2C/B2B workflows;
- moderation behavior;
- inventory semantics.

Do not introduce:

- React/Vue/Angular;
- Tailwind;
- generic dashboards/themes;
- large visual frameworks;
- fake marketing/trust content;
- unrelated features.

If minimal Identity scaffolding is required, scaffold only what is necessary and preserve authentication behavior.

---

## Required visual outcome

Faed should feel:

- modern;
- trusted;
- commerce-first;
- premium but restrained;
- custom;
- coherent;
- mobile-ready.

It must not feel:

- default Bootstrap;
- ASP.NET tutorial;
- generic CRUD;
- generic AI SaaS;
- noisy discount marketplace.

---

## Required surfaces

Polish all applicable current surfaces:

### Shared
- Navbar
- Footer
- account controls
- buttons/forms/tables/statuses/pagination
- alerts/errors/empty states

### Public
- Home
- Shop
- Listing Cards
- Listing Details
- Merchant Store
- Privacy/Error/404

### Identity
- Login
- Register
- AccessDenied
- reachable Manage pages

### Buyer
- Checkout
- Orders
- Order Details
- Disputes
- Reviews where applicable

### Merchant
- sub-navigation
- verification
- listings/create/workspace
- inventory
- B2C orders
- B2B offers/deals
- disputes/reviews
- analytics
- store settings

### Admin
- sub-navigation
- overview
- verification
- moderation
- transactions
- disputes
- reviews
- catalog
- audit log

---

## Required browser QA

Real browser validation is required for plain `PASS`.

Test at:

- 360 px
- 390 px
- 768 px
- 1280 px
- 1440 px

Validate:

- navigation/footer;
- forms;
- product cards;
- filters;
- tables;
- pagination;
- gallery;
- Identity;
- Buyer;
- Merchant;
- Admin;
- keyboard/focus;
- browser console.

If real browser validation is impossible, do not report plain `PASS`; use `PASS WITH DOCUMENTED LIMITATIONS`.

---

## Regression gate

Use the post-TASK-012 baseline recorded in `PROJECT_STATUS.md`.

Known expected baseline from the completion report:

`460 passed / 0 failed / 0 skipped`

Run:

```text
dotnet build Faed.slnx -c Debug
dotnet build Faed.slnx -c Release
dotnet test Faed.slnx -c Release
dotnet ef migrations has-pending-model-changes
```

No test may be disabled.

No migration/model change is expected.

---

## Exit criteria

- [ ] Navbar is clearly custom Faed UI.
- [ ] Footer is clearly custom Faed UI.
- [ ] normal UI no longer shows default ASP.NET/Bootstrap styling.
- [ ] Login/Register visually belong to Faed.
- [ ] Home has strong marketplace hierarchy.
- [ ] Shop feels like polished commerce browse.
- [ ] Product cards are polished and consistent.
- [ ] Listing Details feels like real e-commerce.
- [ ] Buyer UI no longer feels plain CRUD.
- [ ] Merchant UI feels like a coherent seller workspace.
- [ ] Admin UI feels like a coherent operational workspace.
- [ ] Forms/tables/statuses/pagination share one visual system.
- [ ] responsive QA passes.
- [ ] accessibility/focus/keyboard QA passes.
- [ ] browser console is clean.
- [ ] final `faed-ui-quality-gate` passes.
- [ ] Debug/Release builds pass.
- [ ] full automated suite passes.
- [ ] no pending model change.
- [ ] no Business Logic/Authorization regression.
- [ ] `PROJECT_STATUS.md` is updated.

---

## Final report

Report:

1. final status;
2. biggest before/after visual changes;
3. exact files changed;
4. Navbar/Footer work;
5. Identity work;
6. Public marketplace work;
7. Buyer work;
8. Merchant work;
9. Admin work;
10. responsive/accessibility fixes;
11. exact viewport/browser QA;
12. exact build/test/model-check results;
13. remaining limitations.

Do not commit or push.

If TASK-013 closes cleanly, recommend moving to final documentation / ERD / demo / deployment — not another general UI/UX task.
