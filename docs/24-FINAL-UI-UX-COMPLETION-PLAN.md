# Faed — Final UI/UX Completion Plan

> Final post-MVP UI/UX completion plan after `TASK-001` through `TASK-011`.

## 1. Goal

Close Faed UI/UX completely before demo/deployment without redesigning working parts or expanding product scope.

The final application must be:

- coherent across Public, Buyer, Merchant, Admin, and Identity surfaces;
- navigable according to role, ownership, merchant/account state, and workflow state;
- complete for empty/error/pending/rejected/sold-out/expired/stale states;
- responsive on phone, tablet, and desktop;
- accessible at the repository-required baseline;
- visually intentional rather than default Bootstrap;
- faithful to existing commerce, moderation, authorization, privacy, and concurrency rules.

**Preserve good UI. Fix only issues proven by the audit.**

---

## 2. Sources of truth and inspection rule

Follow the precedence defined by `AGENTS.md`.

Before implementation, read:

- `AGENTS.md`
- `PROJECT_STATUS.md`
- all current `docs/`
- all current `tasks/`
- relevant ADRs
- `docs/21-CLAUDE-SKILLS-USAGE.md`
- relevant `.claude/skills/`

For every affected UI surface, inspect the current:

- Razor View / Partial / Layout;
- Controller action;
- ViewModel;
- authorization attribute/policy;
- ownership check;
- service/query feeding the UI;
- CSS;
- JavaScript.

**Documentation alone is not proof of current UI behavior.**

---

## 3. Post-TASK-011 baseline to preserve

TASK-011 already introduced shared paging infrastructure:

- `src/Faed.Web/Services/Common/PagedResult.cs`
- `src/Faed.Web/Services/Common/QueryablePagingExtensions.cs`
- `src/Faed.Web/ViewModels/PaginationViewModel.cs`
- `src/Faed.Web/Views/Shared/_Pagination.cshtml`

It also moved the major Buyer, Merchant, and Admin history/queue surfaces to database-side paging.

TASK-012 must **reuse this**, not rebuild pagination globally.

Pre-TASK-012 validation baseline:

- Debug build: clean;
- Release build: clean;
- full suite: `456 passed / 0 failed / 0 skipped`;
- no pending EF model changes;
- clean demo startup verified.

The final gate must preserve or improve this baseline.

---

## 4. Non-negotiable rules

### Preserve

- current ASP.NET Core MVC + Razor architecture;
- Bootstrap 5 as foundation;
- existing Faed CSS/design-token layer;
- vanilla/lightweight JavaScript;
- server-side authorization;
- current business rules and workflow states;
- English-only system UI for MVP;
- `JOD` formatting;
- variant/SKU-level stock semantics;
- separation of `Condition` and `Why discounted`;
- merchant verification and listing moderation rules;
- privacy of protected documents/evidence;
- Admin/merchant role separation.

### Do not add

- React/Vue/Angular;
- Tailwind;
- generic admin/marketplace templates;
- unrelated features;
- new business rules;
- Arabic UI;
- online payment/platform shipping or other deferred scope;
- schema changes for visual reasons;
- unrelated architecture/refactoring work.

A small functional change is allowed only when required to correct an existing MVP UX defect. Document why and add regression coverage.

---

## 5. Mandatory working rules

1. Audit before visual polish.
2. Current source beats assumptions.
3. Reuse before adding.
4. Do not redesign already-good screens.
5. Navigation visibility must match real permission/state.
6. Hiding a link never replaces server authorization.
7. No critical page may be unintentionally unreachable.
8. No visible link should predictably send its intended user to `403`.
9. Every growable collection must be database-bounded.
10. Search/filter/sort are added only where they improve a real task.
11. Client validation may assist; server validation remains authoritative.
12. Concurrency/stale data must never silently overwrite newer state.
13. Do not invent urgency, stock, ratings, reviews, discounts, or trust claims.
14. Do not add fake skeleton/loading UI to normal server-rendered navigation.
15. Mobile and keyboard use are completion requirements.
16. Screenshots alone cannot close a phase.

---

# 6. Phase 0 — Source-Backed UI/UX Audit

## Objective

Understand the entire current UI before changing it.

## Review areas

### Shared/global

Inspect current equivalents of:

- `Views/Shared/_Layout.cshtml`
- `_LoginPartial.cshtml`
- `_Pagination.cshtml`
- validation/status/error partials
- shared product/listing/table/form components
- `_Layout.cshtml.css`
- `wwwroot/css/site.css`
- `wwwroot/css/faed.css`
- relevant `wwwroot/js/**`

### Public

Inventory every actual public route/view, including:

- Home
- Shop/discovery
- Listing Details
- Merchant Store
- search/filter result states
- final informational/error pages

### Buyer

Inventory every current Buyer surface, including:

- checkout;
- orders + detail;
- disputes;
- reviews;
- other Buyer account/actions that actually exist.

### Merchant

Inventory every current Merchant surface, including:

- verification/onboarding;
- dashboard;
- listings/create/edit;
- inventory;
- B2C orders;
- B2B offers/negotiations;
- B2B deals;
- disputes;
- analytics;
- reviews where applicable;
- store settings.

### Admin

Inventory every current Admin surface, including:

- dashboard;
- merchant verification;
- listing moderation;
- orders/deals monitoring;
- disputes;
- reviews;
- catalog;
- audit/history.

## Surface inventory

Create this matrix from the source:

| Surface | Route | View | Controller | ViewModel | Authorization | Ownership | Account/workflow state | Primary CTA | Reachable from | Collection? | Issues |
|---|---|---|---|---|---|---|---|---|---|---|---|

## Additional audit matrices

Create:

1. **Role × account state × navigation**
2. **Collection × search/filter/sort/paging**
3. **Form/action × validation/confirmation/concurrency**
4. **Workflow state × visible status/CTA**
5. **Page × mobile/tablet/desktop risk**

## Finding priority

- **P0** — blocks use, privacy/security understanding, authorization UX, or critical accessibility.
- **P1** — major IA/navigation, flow, state, form, or responsive defect.
- **P2** — inconsistency or meaningful polish problem.
- **P3** — optional refinement.

## Exit criteria

- [ ] Every real MVP surface is inventoried.
- [ ] Every protected surface has its actual authorization/ownership rule recorded.
- [ ] Every visible navigation item maps to a valid audience and destination.
- [ ] Every important page has an intentional entry point.
- [ ] Every growable collection is identified.
- [ ] Every state-changing form/action is identified.
- [ ] All source-defined user-visible states are identified.
- [ ] P0–P3 findings are prioritized.
- [ ] No broad visual implementation starts before this audit is complete.

---

# 7. Phase 1 — Information Architecture and Navigation

## Objective

Make navigation reflect what each user can actually do.

Derive link visibility from:

- authentication;
- role;
- merchant/application state;
- authorization policy;
- ownership;
- workflow state.

## Required behavior

### Anonymous

- public destinations only;
- no Buyer/Merchant/Admin operational clutter;
- onboarding link only if intentionally public.

### Authenticated Buyer / non-approved seller

- clear Buyer destinations;
- merchant onboarding presented as onboarding;
- no approved-Merchant operational links.

### Merchant applicant

Navigation must change with actual source states, such as where applicable:

- not started;
- draft;
- pending;
- rejected;
- approved.

Pending/rejected users must not see approved-only operations.

### Approved Merchant

- all core Merchant workflows reachable;
- operational links grouped in Merchant navigation rather than bloating global navigation.

### Admin

- Admin tools only;
- no merchant-selling navigation;
- Admin/merchant identity separation preserved.

### Ownership-sensitive actions

Show edit/manage/fulfill/moderate/private-document actions only when the current user is allowed to perform them.

## Navigation rules

- global navigation stays small;
- dense Merchant/Admin pages use role-specific navigation;
- current section is identifiable where useful;
- mobile navigation has no overflow;
- do not use disabled links when hiding is clearer;
- use disabled state only when the reason itself is useful information;
- direct URLs remain protected server-side.

## Acceptance criteria

- [ ] No intended user sees a link that predictably ends in `403`.
- [ ] No critical workflow is direct-URL-only by accident.
- [ ] Unapproved merchants do not see approved-only navigation.
- [ ] Admin does not see Merchant selling links.
- [ ] Approved Merchant can reach every core Merchant screen.
- [ ] Buyer flows remain independent of Merchant/Admin navigation.
- [ ] Mobile navigation works at 360–390 px.

---

# 8. Phase 2 — Search, Filters, Sort and Server-Side Pagination

## Objective

Make every growable collection bounded and usable while preserving TASK-011 paging.

## Build the collection matrix

Audit actual current collections, including applicable:

- Shop/listings;
- Buyer orders/disputes/reviews;
- Merchant listings;
- Merchant inventory;
- Merchant orders;
- Merchant B2B offers/negotiations;
- Merchant deals;
- Merchant disputes;
- Admin verification/moderation queues;
- Admin transaction monitoring;
- Admin disputes/reviews/catalog/audit log.

For each record:

| Collection | Expected growth | Search | Filters | Sort | Paging | Server-side? | Needed change |
|---|---:|---|---|---|---|---|---|

## Query rules

For growable collections:

1. filter;
2. sort deterministically;
3. page;
4. materialize.

Use the existing shared paging abstraction.

Never:

- load all rows then filter in Razor;
- load all rows then paginate in JavaScript;
- duplicate paging helpers per Area.

## Search

Add only where users need record discovery.

Requirements:

- trim input;
- preserve query;
- reset to page 1 when query changes;
- server-side predicate;
- no-results recovery;
- clear/reset control.

## Filters

Use meaningful existing dimensions only, such as where applicable:

- status;
- category;
- condition;
- date;
- merchant;
- workflow state.

Do not expose internal/future states.

## Sort

Add only where useful.

Use existing business meaning. Do not invent priority.

Ensure stable ordering with a tie-breaker when needed.

## Query-string behavior

- search/filter/sort/page state lives in query parameters;
- pagination preserves active state;
- filter/sort/search changes reset page appropriately;
- back/forward navigation behaves predictably;
- invalid page values fail safely.

## Mobile

Dense filters may use an accessible Bootstrap offcanvas/collapsible pattern if appropriate.

Do not use infinite scroll for operational histories/queues.

## Acceptance criteria

- [ ] Every growable collection is database-bounded.
- [ ] Existing `_Pagination.cshtml` and paging types are reused.
- [ ] Search/filter/sort exist only where useful.
- [ ] Query state survives pagination.
- [ ] Empty and no-results states are distinct.
- [ ] Mobile filter UX is usable.
- [ ] Material query changes have regression tests.

---

# 9. Phase 3 — Design System and Shared Components

## Objective

Normalize the existing design system without replacing working visual identity.

## Audit

Inspect:

- `faed.css`;
- `site.css`;
- layout CSS;
- inline styles;
- repeated Bootstrap utility patterns;
- repeated component markup.

## Normalize only where needed

### Tokens

Use semantic tokens for:

- background/surface;
- text/muted text;
- border;
- brand;
- success/warning/danger/info;
- focus;
- spacing;
- typography;
- radii;
- shadows;
- content width.

### Components

Standardize repeated patterns:

- buttons/links;
- status badges;
- verification signals;
- alerts;
- form controls/errors;
- panels/cards;
- listing cards;
- price blocks;
- condition/discount blocks;
- stock/availability;
- tables;
- pagination;
- filters;
- page headers;
- empty states;
- KPI cards;
- timelines/history;
- confirmation/action areas.

Prefer shared partials only for real repeated semantics.

## Bootstrap cleanup

Bootstrap remains the foundation.

Remove only verified obsolete template styling. Do not delete vendor assets blindly.

## Acceptance criteria

- [ ] Good existing Faed styling is preserved.
- [ ] Major surfaces do not look like default Bootstrap.
- [ ] Repeated components are consistent.
- [ ] Inline one-off styling is removed or justified.
- [ ] Public/Buyer/Merchant/Admin feel like the same product.
- [ ] Merchant/Admin remain appropriately denser than public commerce UI.

---

# 10. Phase 4 — Global Shell, Identity, Feedback and Errors

## Review

- header/footer;
- global containers;
- breadcrumbs where useful;
- role navigation;
- account controls;
- mobile menu;
- Identity pages actually reachable;
- alerts/status messages;
- validation summary;
- 404/error/forbidden behavior.

## Rules

- do not scaffold all Identity pages only for styling;
- reachable Identity screens must visually fit Faed;
- feedback should explain what happened and the next action;
- preserve intentional 404 behavior used to avoid private-resource existence disclosure.

## Acceptance criteria

- [ ] Shell is coherent and responsive.
- [ ] Identity UI is not visually disconnected.
- [ ] Feedback patterns are consistent.
- [ ] Error pages provide recovery without information leakage.

---

# 11. Phase 5 — Public Marketplace

## Home

The first viewport should communicate approved current product positioning:

- what Faed is;
- why inventory may be discounted;
- seller verification/trust;
- where to browse.

Do not add unsupported marketing claims.

## Shop

Review:

- search/filter/sort;
- result context/count where useful;
- product cards;
- current price;
- valid reference price;
- `Condition`;
- `Why discounted`;
- merchant signal;
- availability;
- no-results;
- pagination;
- mobile filters.

## Listing Details

Review:

- product identity;
- gallery;
- condition;
- discount reason;
- disclosed defects/evidence;
- variant selection;
- variant-level stock;
- current price/reference price;
- merchant verification;
- B2C action;
- B2B action where authorized;
- fulfillment expectations;
- sold-out/expired/unavailable state.

Do not imply Faed physically inspected inventory.

## Merchant Store

Review:

- merchant identity;
- verification signal;
- active listings;
- empty state;
- collection controls where justified.

## Acceptance criteria

- [ ] Product identity and price hierarchy are clear.
- [ ] Condition and discount reason are separate.
- [ ] Defects/evidence are clear where applicable.
- [ ] Variant stock is honest.
- [ ] Sold-out/unavailable listing cannot show misleading purchase CTA.
- [ ] Public collection controls work on mobile.
- [ ] No deferred category/sector leaks into MVP UI.

---

# 12. Phase 6 — Buyer UX

## Validate end to end

- browse;
- select variant/quantity;
- checkout;
- allowed fulfillment;
- confirmation;
- orders;
- order detail;
- dispute eligibility;
- review eligibility.

## Checkout rules

- merchant boundary clear;
- item/variant/quantity clear;
- totals remain server-authoritative;
- only allowed fulfillment methods appear;
- validation is recoverable;
- stock/concurrency changes produce a clear retry/review path;
- duplicate click prevention may assist but never replaces server safety.

## Orders / post-transaction

Review:

- status;
- merchant/items/totals;
- progression;
- fulfillment;
- allowed actions;
- dispute/review eligibility;
- collection filters/paging where useful.

## Acceptance criteria

- [ ] Core Buyer flow works on phone.
- [ ] Stale/out-of-stock checkout is recoverable.
- [ ] Actions match actual order state.
- [ ] Reviews/disputes appear only when eligible.
- [ ] Histories remain bounded.
- [ ] No Merchant/Admin-only action appears.

---

# 13. Phase 7 — Merchant UX

## Verification

For every actual application state, show:

- current state;
- missing work;
- next action;
- rejection reason when allowed;
- resubmission path when supported.

Approved-only navigation stays hidden until approved.

## Listings

Review:

- list/create/edit;
- moderation status;
- rejection;
- resubmission;
- live/sold-out/expired/hidden states where present;
- variants;
- media/evidence;
- condition/discount.

Material edits must not visually imply immediate public publication when moderation is required.

## Inventory

Keep variant/SKU identity explicit.

Review:

- available/reserved quantities;
- adjustment controls;
- stale conflict;
- table/mobile treatment.

## B2C Orders

Review collection + details + allowed fulfillment actions.

## B2B

Keep negotiation and accepted deal visually distinct.

Review:

- who must act;
- current/latest offer;
- expiry;
- counter/accept/reject;
- accepted deal;
- reservation;
- fulfillment;
- completion/expiry.

## Disputes / Analytics / Store Settings

- show actionable dispute state clearly;
- analytics needs useful labels and no-data state;
- store settings remain concise.

## Acceptance criteria

- [ ] Merchant navigation matches verification state.
- [ ] Approved Merchant can reach all core operations.
- [ ] Listing moderation is understandable.
- [ ] Inventory remains SKU/variant-aware.
- [ ] Negotiation and deal are distinct.
- [ ] Expired/reserved/action-required states are clear.
- [ ] Collections are bounded.
- [ ] Tables/forms are usable on phone.

---

# 14. Phase 8 — Admin UX

## Review

- dashboard;
- merchant verification;
- listing moderation;
- transactions;
- disputes;
- reviews;
- catalog;
- audit log.

## Queue rules

Each queue should show enough information to decide, not every available field.

Use:

- useful search/filter/sort;
- shared pagination;
- clear status;
- obvious next action;
- detail page for heavier evidence/context;
- deliberate confirmation for high-impact decisions.

## Admin navigation

Use a coherent Admin section.

Do not put every Admin destination in the global public navbar.

Admin must not receive Merchant selling navigation.

## Acceptance criteria

- [ ] Every operational Admin page is reachable.
- [ ] Queues are server-paged.
- [ ] Search/filter/sort are present where needed.
- [ ] Decisions show enough context.
- [ ] High-impact actions are deliberate.
- [ ] Private information remains authorized.
- [ ] Mobile Admin navigation is usable.
- [ ] UI does not become a generic dashboard template.

---

# 15. Phase 9 — Forms, Validation, Confirmation and Concurrency UX

## Forms

For every form verify:

- visible labels;
- required/optional clarity;
- correct input types;
- consistent help text;
- field errors;
- validation summary where appropriate;
- safe input retention after failure;
- logical keyboard/focus order;
- phone usability.

Server validation remains authoritative.

## Confirmations

Use confirmation for actions that are:

- destructive;
- irreversible;
- operationally significant;
- costly if clicked accidentally.

Do not add confirmation friction to harmless/reversible actions.

## Duplicate submit

High-impact forms may disable/show a real submitting state after valid submission.

Do not rely on JavaScript for correctness.

## Stale/concurrency

Audit UI mapping for:

- stock changed;
- inventory row changed;
- offer/deal state changed;
- moderation/application state changed.

Rules:

- never silently overwrite;
- explain that server state changed;
- preserve safe user input where possible;
- give refresh/review/retry action;
- show current state before retry where useful.

## Acceptance criteria

- [ ] Forms/validation are consistent.
- [ ] Server validation remains authoritative.
- [ ] High-impact actions are deliberate.
- [ ] Duplicate-click UX is controlled where needed.
- [ ] Stale/conflict states are recoverable.
- [ ] No silent stale overwrite path exists.

---

# 16. Phase 10 — State Completeness

Audit every **actual source-defined** user-visible state.

Required families where applicable:

- empty;
- no results;
- real async loading;
- validation/business error;
- pending;
- rejected;
- approved;
- live;
- hidden/inactive;
- sold out;
- expired;
- reserved;
- stale/conflict;
- order states;
- negotiation states;
- deal states;
- dispute states;
- review states;
- analytics no-data.

Rules:

- empty ≠ no results;
- status must not rely on color only;
- sold out/expired must disable/remove invalid actions;
- pending/rejected states explain the next step;
- no invented low-stock threshold;
- no invented workflow state.

## Acceptance criteria

- [ ] Every actual user-visible state has intentional status/copy/action treatment.
- [ ] Invalid CTAs disappear or are clearly unavailable with a useful reason.
- [ ] Stale/conflict states provide recovery.
- [ ] State copy uses consistent domain terminology.

---

# 17. Phase 11 — Responsive, Accessibility, Copy, Images and Front-End Performance

## Viewports

Manually review at minimum:

- **360–390 px** phone;
- **768 px** tablet;
- **1280–1440 px** desktop.

Check intermediate widths where breakpoints are risky.

## Responsive

Verify:

- no unintended horizontal overflow;
- menus/dropdowns/offcanvas/modals fit;
- forms remain usable;
- primary CTA is discoverable;
- tables have deliberate mobile behavior;
- filters do not dominate the viewport;
- long IDs/statuses/names wrap;
- sticky elements do not cover content;
- galleries work.

## Accessibility

Verify:

- one clear page `h1`;
- sensible heading order;
- links vs buttons semantics;
- visible focus;
- accessible names for icon controls;
- labels for inputs;
- validation association;
- status not color-only;
- practical contrast;
- correct table headers;
- accessible pagination;
- `aria-current` where useful;
- Bootstrap modal/offcanvas focus behavior;
- meaningful image alt;
- decorative image `alt=""`;
- custom motion respects reduced motion if present.

## UX copy

Normalize English system wording for:

- actions;
- statuses;
- validation;
- confirmations;
- empty states;
- date/time display;
- `JOD`;
- Merchant/Buyer/Admin;
- listing/variant;
- offer/negotiation/deal;
- order/dispute/review.

Do not rename domain concepts casually.

## Images

Review:

- consistent aspect ratio;
- alt strategy;
- defect evidence visibility;
- layout-shift prevention;
- lazy loading below fold where appropriate;
- reasonable source dimensions;
- no protected evidence exposure.

## Front-end performance

Audit:

- duplicate CSS;
- obsolete page styles;
- unnecessary JS;
- image weight/layout shift;
- console errors;
- client-side errors.

Do not perform risky vendor cleanup only to chase scores.

## Acceptance criteria

- [ ] Critical flows pass at all three viewport groups.
- [ ] Keyboard-only critical path passes.
- [ ] Focus/forms/status meaning are accessible.
- [ ] Images are usable and reasonably optimized.
- [ ] No material console/client errors remain.
- [ ] No major overflow/layout-shift issue remains.

---

# 18. Expected repository review map

Adapt this to the actual current tree.

### Shared

- `src/Faed.Web/Views/Shared/**`
- `src/Faed.Web/Views/_ViewImports.cshtml`
- `src/Faed.Web/Views/_ViewStart.cshtml`
- `src/Faed.Web/wwwroot/css/**`
- `src/Faed.Web/wwwroot/js/**`

### Public

- `src/Faed.Web/Views/**`
- `src/Faed.Web/Controllers/**`
- related public ViewModels/services

### Buyer

- `src/Faed.Web/Areas/Buyer/Views/**`
- `src/Faed.Web/Areas/Buyer/Controllers/**`
- Buyer ViewModels/services

### Merchant

- `src/Faed.Web/Areas/Merchant/Views/**`
- `src/Faed.Web/Areas/Merchant/Controllers/**`
- Merchant ViewModels/services/policies

### Admin

- `src/Faed.Web/Areas/Admin/Views/**`
- `src/Faed.Web/Areas/Admin/Controllers/**`
- Admin ViewModels/services/policies

### Cross-cutting

- `src/Faed.Web/Authorization/**`
- `src/Faed.Web/ViewModels/**`
- paging types/partial
- list-query services
- controller/business-result mapping for POST/conflict flows

Do not change domain/service code unless a verified existing UX defect cannot be correctly fixed at the presentation/controller boundary.

---

# 19. Final Role × State × Ownership × Viewport QA Matrix

During Phase 0, replace generic state labels with exact current source state names.

| Persona / state | Ownership / authorization scenario | Critical flow | Phone | Tablet | Desktop |
|---|---|---|:---:|:---:|:---:|
| Anonymous | Public | Home → Shop → Listing → Merchant Store | ☐ | ☐ | ☐ |
| Anonymous | Protected direct URL | Buyer/Merchant/Admin route denies/redirects correctly | ☐ | ☐ | ☐ |
| Buyer | Own account | Checkout → Orders → Order detail | ☐ | ☐ | ☐ |
| Buyer | Eligibility | Review/dispute action only when valid | ☐ | ☐ | ☐ |
| Merchant applicant — initial/draft | Own | Verification; approved-only nav hidden | ☐ | ☐ | ☐ |
| Merchant applicant — pending | Own | Status clear; operations unavailable | ☐ | ☐ | ☐ |
| Merchant applicant — rejected | Own | Reason + next action/resubmit if supported | ☐ | ☐ | ☐ |
| Approved Merchant | Own | Listings → moderation → inventory → B2C orders | ☐ | ☐ | ☐ |
| Approved Merchant | B2B buyer/seller | Offer/negotiation → deal → fulfillment | ☐ | ☐ | ☐ |
| Approved Merchant | Other/private resource | Ownership restriction + no invalid CTA | ☐ | ☐ | ☐ |
| Admin | Admin | Dashboard/queues/moderation/disputes/reviews/audit | ☐ | ☐ | ☐ |
| Admin | Merchant-selling route | Merchant selling nav absent; route denied | ☐ | ☐ | ☐ |
| Any role | Empty collection | Useful empty state | ☐ | ☐ | ☐ |
| Any role | No results | Active filters + reset/recovery | ☐ | ☐ | ☐ |
| Transactional role | Stale/concurrency | Clear conflict + safe recovery | ☐ | ☐ | ☐ |
| Relevant role | Pending/rejected/sold-out/expired | Correct state + valid CTA behavior | ☐ | ☐ | ☐ |
| Any user | 404/error | Recovery without private data leakage | ☐ | ☐ | ☐ |

Overlay this matrix on every exact current state for:

- merchant verification;
- listing moderation/publication;
- inventory/reservation;
- B2C orders;
- B2B negotiations;
- B2B deals;
- disputes;
- reviews.

---

# 20. Validation after each phase

After each implementation phase:

1. build;
2. run focused tests;
3. exercise changed flows;
4. verify authorization/ownership;
5. verify validation/conflict behavior;
6. test phone + desktop;
7. keyboard-check changed controls;
8. record remaining findings;
9. do not continue with a broken build.

---

# 21. Final Build/Test/Regression Gate

## Build

```text
dotnet build Faed.slnx -c Debug
dotnet build Faed.slnx -c Release
```

Expected:

- 0 errors;
- no new TASK-012 warnings.

## Full tests

```text
dotnet test Faed.slnx -c Release
```

Baseline: `456 passed / 0 failed / 0 skipped`.

Final suite must include all existing tests plus any TASK-012 regression tests.

Do not remove/skip previously passing tests to close the task.

## EF/model guard

Run the same pending-model-change check used by TASK-011.

A UI task should normally create no model/schema change.

Any model change is a scope exception and must be explicitly justified.

## Manual regression

Pass at minimum:

- Anonymous discovery;
- Buyer checkout/orders;
- merchant onboarding states;
- approved Merchant listings/inventory/B2C;
- Merchant B2B;
- Admin queues/moderation;
- disputes/reviews eligibility;
- stale/concurrency recovery;
- search/filter/sort/pagination;
- protected direct URLs;
- 404/error/privacy behavior.

## UI quality gates

Run repository-prescribed skills, at minimum:

- `faed-responsive-accessibility`
- `faed-ui-quality-gate`

and other design/accessibility/copy skills required by repository instructions.

---

# 22. Definition of Done

TASK-012 is complete only when:

### Audit / IA

- [ ] Full source-backed inventory is complete.
- [ ] Exact role/state/ownership matrix is complete.
- [ ] No intended user sees predictably inaccessible navigation.
- [ ] No critical page is accidentally unreachable.
- [ ] Server authorization remains intact.

### Collections

- [ ] Every growable collection is database-bounded.
- [ ] TASK-011 paging infrastructure is reused.
- [ ] Search/filter/sort are added only where useful.
- [ ] Query state survives paging.
- [ ] Empty/no-results are intentional.

### Visual system

- [ ] Good existing UI is preserved.
- [ ] Major default Bootstrap/template residue is removed.
- [ ] Repeated components/tokens are consistent.
- [ ] Public/Buyer/Merchant/Admin feel like one product.

### Flows/states

- [ ] Public, Buyer, Merchant, Admin flows are coherent.
- [ ] Pending/rejected/sold-out/expired states are intentional.
- [ ] Forms/feedback/confirmation are consistent.
- [ ] Concurrency/stale data is recoverable and never silently overwritten.

### Responsive/accessibility/performance

- [ ] Phone/tablet/desktop QA passes.
- [ ] Keyboard/focus/form checks pass.
- [ ] Status is not color-only.
- [ ] Image/asset treatment is reasonable.
- [ ] No major overflow/client error remains.

### Regression

- [ ] Debug build passes.
- [ ] Release build passes.
- [ ] Full automated suite passes.
- [ ] No existing test is disabled.
- [ ] No unapproved model/schema change exists.
- [ ] Final UI quality gates pass.
- [ ] `PROJECT_STATUS.md` records TASK-012 closure, exact validation results, and accepted limitations.

---

# 23. Required completion report

Report:

- final status: `PASS`, `PASS WITH DOCUMENTED LIMITATIONS`, or `FAIL`;
- P0/P1/P2/P3 audit summary;
- IA/navigation fixes;
- search/filter/sort/pagination changes;
- design-system/shared-component changes;
- Public/Buyer/Merchant/Admin flow fixes;
- state/form/concurrency fixes;
- responsive/accessibility/performance fixes;
- exact build/test/model-check results;
- final Role × State × Ownership × Viewport QA result;
- accepted limitations.

After a clean TASK-012 closure, the next step is **demo/deployment**, not another general UI/UX phase.
