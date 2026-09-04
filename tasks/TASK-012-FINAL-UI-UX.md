# TASK-012 — Final UI/UX Completion

## Objective

Execute the final post-MVP UI/UX completion pass for Faed before demo/deployment.

Detailed execution plan:

`docs/24-FINAL-UI-UX-COMPLETION-PLAN.md`

Do not repeat the plan here.

## Read first

- `AGENTS.md`
- `PROJECT_STATUS.md`
- all current `docs/`
- relevant current `tasks/`
- relevant `.claude/skills/`
- `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md`

Inspect the current source before changing UI.

## Critical rule

**Start with the source-backed Phase 0 audit, not visual polish.**

The audit must cover:

- every user-facing surface;
- route/controller/view/ViewModel;
- authorization + ownership;
- role/merchant-state navigation;
- growable collections;
- forms/state-changing actions;
- user-visible workflow states;
- responsive/accessibility risks.

## Preserve TASK-011

TASK-011 already added shared server paging:

- `PagedResult<T>`
- `QueryablePagingExtensions`
- `PaginationViewModel`
- `Views/Shared/_Pagination.cshtml`

Reuse it. Do not rebuild pagination globally.

Baseline before TASK-012:

- `456 passed / 0 failed / 0 skipped`;
- clean Debug/Release builds;
- no pending EF model changes.

## In scope

Execute all applicable requirements in the detailed plan, especially:

- Information Architecture and role/state/ownership-aware navigation;
- misplaced/inaccessible/unreachable page fixes;
- Search / Filters / Sort / server-side Pagination for growable collections;
- design-system consistency;
- Public / Buyer / Merchant / Admin / Identity UI;
- forms, validation, confirmations and feedback;
- stale/concurrency UX;
- empty/error/pending/rejected/sold-out/expired states;
- responsive, accessibility, copy, images and front-end performance;
- final Role × State × Ownership × Viewport QA.

## Out of scope

No:

- unrelated features or business-rule changes;
- workflow/state redesign;
- authorization/privacy weakening;
- schema changes for visual reasons;
- React/Vue/Angular/Tailwind;
- generic admin/marketplace templates;
- Arabic UI;
- deferred payments/shipping/features;
- unrelated refactors.

A small functional correction is allowed only for a verified existing MVP UX defect, with regression coverage.

## Required validation

```text
dotnet build Faed.slnx -c Debug
dotnet build Faed.slnx -c Release
dotnet test Faed.slnx -c Release
```

Also run:

- pending-model-change check;
- relevant focused tests;
- Anonymous / Buyer / Merchant applicant / Approved Merchant / Admin manual flows;
- ownership/direct-URL checks;
- search/filter/sort/paging regression;
- stale/concurrency recovery;
- phone 360–390 px;
- tablet 768 px;
- desktop 1280–1440 px;
- keyboard/focus/form accessibility;
- `faed-responsive-accessibility`;
- `faed-ui-quality-gate`.

Do not disable existing tests.

## Exit criteria

- [ ] Detailed plan is fully executed or items are explicitly proven not applicable.
- [ ] No unresolved P0/P1 UI/UX issue remains without an accepted limitation.
- [ ] Navigation matches role, ownership, and merchant/account state.
- [ ] No visible link predictably leads its intended user to an inaccessible page.
- [ ] Every critical page is intentionally reachable.
- [ ] Every growable collection is database-bounded.
- [ ] Search/filter/sort/paging work where applicable.
- [ ] Good existing UI is preserved.
- [ ] Public/Buyer/Merchant/Admin UI is coherent.
- [ ] Forms/validation/feedback/confirmations are consistent.
- [ ] Stale/concurrency conflicts are recoverable.
- [ ] Empty/error/pending/rejected/sold-out/expired states are intentional.
- [ ] Responsive/accessibility matrix passes.
- [ ] No sensitive/private data is newly exposed.
- [ ] Debug + Release builds pass.
- [ ] Full automated suite passes with existing tests still enabled.
- [ ] No unapproved model/schema change exists.
- [ ] Final UI quality gates pass.
- [ ] `PROJECT_STATUS.md` records TASK-012 completion and exact validation results.

## Completion report

Report only:

- final status;
- major audit/findings fixed;
- IA/navigation changes;
- collection/paging changes;
- shared visual-system changes;
- role-flow/state/concurrency fixes;
- responsive/accessibility/performance fixes;
- exact build/test/model-check results;
- final QA matrix result;
- accepted limitations.

If all gates pass, recommend **demo/deployment**, not another general UI/UX phase.
