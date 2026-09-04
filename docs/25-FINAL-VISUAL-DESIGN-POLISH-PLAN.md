# Faed — Final Visual Design & Polish Plan

> Supporting plan for `TASK-013`.
>
> Goal: transform the already-functional Faed MVP into a visually polished, cohesive, demo-ready marketplace without changing business behavior, authorization, architecture, or database semantics.

---

# 1. Why TASK-013 exists

`TASK-001` through `TASK-012` completed the MVP functionality, workflows, authorization, trust rules, B2C/B2B behavior, pagination, UX hardening, and regression coverage.

The remaining gap is **visual execution**.

The repository already contains a useful Faed design foundation, especially:

- `src/Faed.Web/wwwroot/css/faed.css`
- reusable `faed-*` components;
- public marketplace components;
- Merchant/Admin sub-navigation;
- project-specific UI skills.

However, the application still contains visible ASP.NET Core / Bootstrap scaffold residue and does not consistently feel like a polished real marketplace.

Examples already verified in the current source include:

- `_Layout.cshtml` still uses a mostly default Bootstrap navbar:
  `navbar navbar-expand-sm navbar-light bg-white border-bottom`
- the footer is minimal/default in presentation;
- `_Layout.cshtml.css` still contains ASP.NET template styling such as `#1b6ec2`;
- `site.css` still contains a default blue focus ring (`#258cfb`);
- Login/Register/Manage still visually resemble default ASP.NET Identity;
- several operational screens are functionally good but still visually read like CRUD/admin pages.

This task is therefore a **real visual design and polish pass**, not another functional audit.

---

# 2. Final visual objective

Faed should immediately feel like a:

> **modern, trustworthy, premium-but-accessible fashion overstock marketplace**

The UI should communicate:

- trust;
- clarity;
- honest discounts;
- verified sellers;
- modern retail quality;
- operational competence;
- visual consistency.

It must **not** look like:

- default Bootstrap;
- an ASP.NET tutorial project;
- a generic CRUD application;
- a generic admin dashboard template;
- an AI-generated SaaS template;
- a classifieds board;
- a noisy discount website;
- a luxury editorial fashion site;
- a clone of another marketplace.

The visual difference between the pre-TASK-013 and post-TASK-013 application must be **immediately noticeable**.

---

# 3. Read before implementation

Follow repository precedence from `AGENTS.md`.

Read at minimum:

1. `AGENTS.md`
2. `PROJECT_STATUS.md`
3. `docs/07-UI-UX-SPEC.md`
4. `docs/03-BUSINESS-RULES.md`
5. `docs/05-USER-FLOWS-AND-STATE-MACHINES.md`
6. `docs/08-SECURITY-AND-PRIVACY.md`
7. `docs/16-PERMISSIONS-MATRIX.md`
8. `docs/21-CLAUDE-SKILLS-USAGE.md`
9. `docs/24-FINAL-UI-UX-COMPLETION-PLAN.md` if present
10. `tasks/TASK-012-FINAL-UI-UX.md` if present
11. this plan
12. all relevant `.claude/skills/**/SKILL.md`

The current source code is the implementation truth.

---

# 4. Mandatory Faed UI skills

Read and apply:

- `.claude/skills/faed-ui-direction/SKILL.md`
- `.claude/skills/faed-commerce-ux/SKILL.md`
- `.claude/skills/faed-marketplace-pages/SKILL.md`
- `.claude/skills/faed-dashboard-ux/SKILL.md`
- `.claude/skills/faed-responsive-accessibility/SKILL.md`
- `.claude/skills/faed-ui-quality-gate/SKILL.md`

The final quality gate must actively reject major surfaces that still look like default Bootstrap.

---

# 5. Product rules that visual work must preserve

Do not change business meaning.

Preserve:

- only verified merchants can sell;
- Buyer, Merchant, and Admin roles remain distinct;
- Admin is not a merchant-selling identity;
- Fashion Overstock remains the MVP scope;
- `Condition` and `Why discounted` remain separate;
- reference price/discount appears only when valid;
- defects/evidence remain prominent where applicable;
- stock remains variant/SKU-aware;
- B2C and B2B remain distinct;
- negotiation and accepted deal remain distinct;
- protected files/evidence remain private;
- server-side authorization remains authoritative;
- system UI remains English-only;
- `JOD` formatting remains correct;
- no fake reviews, ratings, urgency, stock, trust, or social proof.

---

# 6. Technical boundaries

Keep:

- ASP.NET Core MVC;
- Razor Views;
- Bootstrap 5 as foundation;
- existing Faed CSS layer;
- lightweight/vanilla JavaScript;
- current controllers/services/ViewModels;
- current authorization;
- current database model.

Do **not** introduce:

- React;
- Vue;
- Angular;
- Tailwind;
- Material UI;
- another CSS framework;
- paid marketplace templates;
- generic admin themes;
- large animation libraries;
- unnecessary icon frameworks.

The visual identity must come from project-owned CSS and Razor structure.

---

# 7. Implementation philosophy

## 7.1 Visual change is explicitly required

For TASK-013, “preserve good UI” does **not** mean “avoid visible change”.

Preserve:

- correct information architecture;
- correct business content;
- correct functional hierarchy;
- correct semantics/accessibility;
- useful reusable components.

Improve strongly where the UI feels:

- generic;
- flat;
- scaffolded;
- unfinished;
- inconsistent;
- CRUD-like;
- visually weak.

## 7.2 Function before decoration

Every design choice should improve one or more of:

- hierarchy;
- trust;
- scanability;
- navigation;
- action visibility;
- product evaluation;
- state understanding;
- responsive usability.

## 7.3 Avoid over-design

Avoid:

- random gradients;
- neon/glow;
- glassmorphism;
- giant radii;
- deep shadows everywhere;
- card-inside-card-inside-card layouts;
- decorative blobs;
- meaningless icons;
- excessive chips/badges;
- unnecessary animation;
- oversized hero typography.

---

# 8. Visual North Star

## 8.1 Personality

Target tone:

- modern;
- credible;
- calm;
- commerce-first;
- transparent;
- refined;
- professional;
- slightly premium;
- approachable.

## 8.2 Existing palette to preserve

The existing tokens are a strong base:

```css
--faed-bg: #f6f6f3;
--faed-surface: #ffffff;
--faed-surface-muted: #eeeeea;
--faed-text: #1b1b18;
--faed-text-muted: #5c5c53;
--faed-border: #e0e0d9;
--faed-brand: #0f6f5c;
--faed-brand-strong: #0b5344;
--faed-brand-tint: #e5f1ee;
```

Recommended use:

- deep emerald — brand, primary actions, current nav;
- strong emerald — hover/emphasis;
- mint tint — selected/trust/soft promotional surfaces;
- off-white — application background;
- white — cards/forms/panels;
- charcoal — primary text;
- neutral gray — metadata;
- semantic colors — real states only.

Do not add arbitrary page-specific colors.

## 8.3 Page contrast strategy

Avoid “white everywhere”.

Create visual rhythm using:

- page background vs surfaces;
- selective tinted sections;
- stronger typography contrast;
- borders;
- whitespace;
- occasional dark/brand sections such as footer or closing CTA.

---

# 9. Typography system

Use a modern commerce-appropriate system sans-serif unless an intentional existing project font is already configured.

An external font is not required.

Create/refine hierarchy for:

- hero/display title;
- page title;
- section heading;
- card title;
- form section heading;
- body;
- metadata;
- labels;
- helper text;
- table content;
- badges.

Rules:

- clear weight hierarchy;
- comfortable line height;
- readable body size;
- mobile metadata remains legible;
- hero text must not push useful content too far below the fold.

---

# 10. Spacing, radius, shadow, motion

## Spacing

Use consistent rhythm for:

- page padding;
- section gaps;
- card padding;
- table density;
- forms;
- heading/content gaps;
- buttons;
- mobile stacks.

Public pages should breathe more than dashboards.

## Radius

Keep restrained radii.

Use:

- small radius for controls;
- medium radius for cards/panels;
- larger radius only for major hero/auth surfaces when justified.

## Shadows

Use subtle elevation only where layer distinction matters.

## Motion

Small transitions are allowed for:

- hover;
- focus;
- card elevation;
- selected controls;
- Bootstrap dropdown/offcanvas behavior.

Respect `prefers-reduced-motion` for custom motion.

---

# 11. Phase 0 — Visual source audit

Before broad changes, inspect every visual surface.

At minimum review:

## Shared/global

- `src/Faed.Web/Views/Shared/_Layout.cshtml`
- `src/Faed.Web/Views/Shared/_Layout.cshtml.css`
- `src/Faed.Web/Views/Shared/_LoginPartial.cshtml`
- `src/Faed.Web/Views/Shared/_ListingCard.cshtml`
- `src/Faed.Web/Views/Shared/_ShopBrowse.cshtml`
- `src/Faed.Web/Views/Shared/_Pagination.cshtml`
- `src/Faed.Web/Views/Shared/Error.cshtml`
- `src/Faed.Web/wwwroot/css/site.css`
- `src/Faed.Web/wwwroot/css/faed.css`
- relevant `src/Faed.Web/wwwroot/js/**`

## Public

- `Views/Home/Index.cshtml`
- `Views/Home/Privacy.cshtml`
- `Views/Home/StatusCode.cshtml`
- `Views/Shop/Index.cshtml`
- `Views/Listing/Details.cshtml`
- `Views/Store/Index.cshtml`

## Buyer

- `Areas/Buyer/Views/Checkout/Index.cshtml`
- `Areas/Buyer/Views/Orders/Index.cshtml`
- `Areas/Buyer/Views/Orders/Details.cshtml`
- `Areas/Buyer/Views/Disputes/Index.cshtml`
- `Areas/Buyer/Views/Disputes/Create.cshtml`
- `Areas/Buyer/Views/Disputes/Details.cshtml`

## Merchant

- `Areas/Merchant/Views/Shared/_MerchantSubnav.cshtml`
- `Verification/Index.cshtml`
- `Verification/Apply.cshtml`
- `Listings/Index.cshtml`
- `Listings/Create.cshtml`
- `Listings/Workspace.cshtml`
- `Inventory/Index.cshtml`
- `Orders/Index.cshtml`
- `Orders/Details.cshtml`
- `Offers/Index.cshtml`
- `Offers/Create.cshtml`
- `Offers/Details.cshtml`
- `Deals/Index.cshtml`
- `Deals/Details.cshtml`
- `Disputes/Index.cshtml`
- `Disputes/Create.cshtml`
- `Disputes/Details.cshtml`
- `Reviews/Index.cshtml`
- `Analytics/Index.cshtml`
- `StoreSettings/Index.cshtml`

## Admin

- `Areas/Admin/Views/Shared/_AdminSubnav.cshtml`
- `Home/Index.cshtml`
- `MerchantVerification/Index.cshtml`
- `MerchantVerification/Details.cshtml`
- `ListingModeration/Index.cshtml`
- `ListingModeration/Details.cshtml`
- `Transactions/Orders.cshtml`
- `Transactions/OrderDetails.cshtml`
- `Transactions/Deals.cshtml`
- `Transactions/DealDetails.cshtml`
- `Disputes/Index.cshtml`
- `Disputes/Details.cshtml`
- `Reviews/Index.cshtml`
- `Catalog/Index.cshtml`
- `AuditLog/Index.cshtml`

## Identity

Inspect actual runtime Login/Register/Manage implementation and determine whether it is:

- scaffolded locally;
- provided by package UI;
- partially controllable through the shared layout/CSS;
- a candidate for minimum safe scaffolding.

Do not assume.

---

# 12. Phase 1 — Global shell transformation

This phase has the highest visual impact.

## 12.1 Navbar

The current Bootstrap-looking navbar must be visually replaced.

Target qualities:

- strong Faed brand presence;
- deliberate height/spacing;
- clean commerce navigation;
- role-aware account actions;
- polished desktop and mobile states;
- custom hover/focus/active treatment.

### Brand treatment

Use a stronger wordmark and, optionally, a small CSS/SVG-like project-owned brand mark if appropriate.

Do not introduce random stock logos.

### Desktop navigation

Separate clearly:

- public marketplace links;
- account actions;
- Merchant/Admin workspace entry.

Operational sub-pages stay inside role sub-navigation.

### Logged-out state

Present clearly:

- Sign in;
- Register if enabled;
- merchant onboarding where appropriate.

### Logged-in state

Present clearly:

- account state;
- role workspace;
- sign out.

Do not change permission logic.

### Active state

Use Faed styling rather than Bootstrap blue.

Possible cues:

- brand underline;
- tint;
- weight;
- subtle background.

### Mobile

At 360–390 px:

- brand visible;
- toggler touch-friendly;
- links stack predictably;
- account actions remain clear;
- no horizontal overflow.

### Acceptance

- [ ] header no longer looks like default Bootstrap;
- [ ] no default Bootstrap blue;
- [ ] role visibility remains correct;
- [ ] Admin does not regain Buyer/Merchant operational links;
- [ ] keyboard navigation works;
- [ ] mobile menu works.

---

# 13. Phase 2 — Footer transformation

Replace the minimal footer with an intentional marketplace footer.

Use only real routes.

Possible groups when supported:

## Brand
- Faed;
- short honest description.

## Marketplace
- Home;
- Shop.

## Merchant
- Sell/Apply as Merchant;
- Merchant Center where contextually valid.

## Account
- Sign in / relevant dashboard when appropriate.

## Information
- Privacy.

Do not create fake links such as About/FAQ/Careers/Contact/Social unless real routes exist.

Visual direction:

- deep emerald or charcoal surface is allowed;
- strong contrast;
- multi-column desktop;
- stacked mobile;
- polished hover/focus;
- restrained legal row.

Acceptance:

- [ ] intentional Faed footer;
- [ ] no dead links;
- [ ] works at 360 px;
- [ ] clearly belongs to the same product.

---

# 14. Phase 3 — Remove ASP.NET/Bootstrap scaffold residue

Audit and clean:

- `_Layout.cshtml.css`
- `site.css`

Remove/replace obsolete rules such as:

- template blue links;
- template `.btn-primary` override;
- template `.nav-pills` blue;
- old absolute-footer rules;
- default blue focus ring;
- old body margin built for template footer.

Do not remove a rule until its runtime impact is understood.

End state:

- Faed links;
- Faed buttons;
- Faed focus ring;
- Faed forms;
- no visible template colors.

---

# 15. Phase 4 — Refine design tokens

Keep `faed.css` as the main visual layer.

Add only meaningful repeated tokens, potentially for:

- header background/border;
- footer background/text;
- interactive hover surface;
- focus ring;
- elevated shadow;
- section backgrounds;
- content width;
- transition timing.

Avoid token proliferation.

---

# 16. Phase 5 — Core component upgrade

## Buttons

Create clear hierarchy:

1. primary;
2. secondary/ghost;
3. subtle/text;
4. danger.

Support:

- hover;
- active;
- focus-visible;
- disabled;
- small/compact;
- full-width mobile where appropriate.

Do not give every action equal weight.

## Forms

Standardize:

- input height;
- border/radius;
- background;
- focus;
- placeholder;
- labels;
- helper text;
- validation errors;
- disabled state;
- selects;
- textarea;
- checkbox/radio.

## Panels

Use intentionally, not around every text block.

## Tables

Improve:

- header hierarchy;
- row rhythm;
- density;
- hover;
- status/action distinction;
- mobile behavior;
- empty state.

## Badges

Use for actual status, not ordinary metadata.

## Pagination

Polish shared pagination:

- current page obvious;
- branded hover/focus;
- disabled state;
- mobile compactness;
- preserve query behavior.

## Empty/error states

Use:

- short heading;
- explanation;
- valid next action;
- restrained visual treatment.

---

# 17. Phase 6 — Home page redesign

Keep correct content but make the visual execution substantially stronger.

## Hero

Improve:

- first-viewport composition;
- brand treatment;
- headline hierarchy;
- CTA hierarchy;
- trust information;
- spacing.

A two-column desktop composition is allowed if it adds value.

Do not add fake product imagery or fake statistics.

## Trust strip

Use existing real model data only.

## Categories

Improve:

- card hierarchy;
- grid rhythm;
- hover state;
- visual distinction.

Do not invent category art.

## Featured inventory

Product cards should be a major visual focal point.

## How Faed works

Avoid three generic equal-weight cards if a clearer sequential layout works better.

## Transparency section

Make the distinction between `Condition` and `Why discounted` visually memorable.

## Merchant CTA

Use a strong closing section with clear hierarchy.

Acceptance:

- [ ] visibly stronger homepage;
- [ ] purpose clear in first viewport;
- [ ] trust visible;
- [ ] product discovery prominent;
- [ ] no generic marketing fluff;
- [ ] mobile hero remains compact.

---

# 18. Phase 7 — Product card redesign

Target `_ListingCard.cshtml` and shared CSS.

Keep core anatomy:

1. image;
2. title;
3. merchant;
4. current price;
5. valid reference/discount;
6. condition/reason signal;
7. availability;
8. optional B2B signal.

## Media

Improve:

- consistent aspect ratio;
- clean crop;
- neutral placeholder;
- sold-out treatment;
- discount badge placement;
- restrained hover.

## Body

Improve:

- title hierarchy;
- verified merchant line;
- price dominance;
- reference-price hierarchy;
- concise condition/reason;
- restrained channel chips.

## Hover

Use subtle elevation/border/image movement only.

Accessibility must not depend on hover.

---

# 19. Phase 8 — Shop/Browse redesign

Target:

- `Views/Shop/Index.cshtml`
- `_ShopBrowse.cshtml`
- related CSS/JS.

## Desktop

Create a deliberate commerce layout:

- heading/context;
- filter sidebar or strong filter panel;
- result/sort bar;
- product grid;
- pagination.

## Mobile

Filters should use an intentional drawer/offcanvas/collapse.

Requirements:

- clear Filters trigger;
- easy reset;
- active-state visibility where data allows;
- products not pushed too far below controls;
- no horizontal overflow.

## Filters

Group related fields with headings.

Avoid an endless flat stack of selects.

## Results context

Use existing data to show useful context without adding expensive queries only for decoration.

---

# 20. Phase 9 — Listing Details redesign

Target `Views/Listing/Details.cshtml` and related CSS/JS.

This must feel like a real e-commerce product page.

## Desktop

Use a strong two-column layout:

### Left
- gallery;
- thumbnails;
- evidence discovery.

### Right
- title;
- merchant trust;
- price;
- condition;
- discount reason;
- variants;
- stock;
- CTA;
- fulfillment.

## Mobile order

Keep decision-critical information early:

1. image;
2. title;
3. merchant;
4. price;
5. condition/reason;
6. variants;
7. availability;
8. CTA.

## Purchase area

A stronger panel/tinted surface is allowed to establish hierarchy.

Do not add sticky behavior unless tested and beneficial.

## Variants

Selected/unavailable states must be obvious and not color-only.

## Evidence

Visually distinguish:

- normal product image;
- defect photo;
- packaging issue.

## B2C vs B2B

Do not render them as ambiguous equal primary actions.

---

# 21. Phase 10 — Merchant Store redesign

Target `Views/Store/Index.cshtml`.

Create a credible merchant storefront header using real information:

- business name;
- verified state;
- real trust signals;
- active listing context.

Do not create merchant-specific microsites.

Product cards must remain the shared marketplace cards.

---

# 22. Phase 11 — Identity visual integration

This phase is required.

The final Login/Register experience must not look like untouched ASP.NET Identity.

## Step 1 — inspect runtime

Determine if pages are:

- package-provided;
- scaffolded;
- custom;
- using the shared layout.

## Step 2 — safest path first

Try visual integration using:

- shared shell;
- Identity-aware CSS;
- Bootstrap form overrides;
- typography;
- background/layout;
- buttons;
- validation;
- links.

## Target auth composition

A professional auth page should have:

- Faed identity;
- clear title;
- concise support copy;
- balanced auth panel;
- polished fields;
- Faed primary CTA;
- validation styling;
- secondary account link;
- clean mobile stack.

A split-screen desktop auth layout is allowed if useful.

Do not use fake testimonials.

## If scaffolding is required

Scaffold only the minimum required pages.

Preserve authentication logic.

Do not:

- rewrite password handling;
- change sign-in behavior;
- add providers;
- add account fields;
- change Identity configuration;
- change validation rules.

If scaffolding occurs:

- document exact pages;
- verify behavior before/after;
- add regression checks where feasible;
- manually test Login/Register/Logout/AccessDenied.

## Manage pages

Reachable Manage pages should at least inherit Faed:

- shell;
- typography;
- links;
- buttons;
- forms;
- spacing.

---

# 23. Phase 12 — Buyer visual polish

## Checkout

Transform from plain form to commerce checkout.

Use clear groups:

- item summary;
- variant/quantity;
- fulfillment;
- buyer details where applicable;
- totals;
- confirmation.

The primary purchase action should be obvious.

## Orders

Improve scanability:

- page header;
- filters/status where present;
- order identity;
- merchant;
- amount;
- status;
- next action.

Use responsive table/card treatment.

## Order Details

Use strong sections:

- status;
- items;
- merchant;
- fulfillment;
- totals;
- available actions;
- disputes/reviews.

Do not invent timeline events.

## Disputes

Clarify:

- current state;
- issue;
- evidence/history;
- allowed action.

---

# 24. Phase 13 — Merchant workspace polish

The Merchant area should feel like a professional seller workspace.

## Merchant sub-navigation

Upgrade `_MerchantSubnav.cshtml`.

Requirements:

- current section obvious;
- compact responsive behavior;
- no raw Bootstrap pills;
- clear grouping.

Do not build a huge new shell if a lighter improvement achieves the goal.

## Verification

State and next action must dominate.

## Listings

Improve:

- Create Listing CTA;
- listing image/title;
- stock/channel;
- moderation status;
- actions.

## Create/Workspace

Visually chunk long forms into logical sections:

- basic product;
- category;
- condition;
- discount reason;
- variants;
- stock;
- pricing;
- photos/evidence;
- fulfillment/policies;
- review/submit.

Do not invent a multi-page wizard if it changes risk/flow.

## Inventory

Prioritize:

- SKU;
- options;
- available;
- reserved;
- adjustment action.

Keep operational density.

## Orders / Offers / Deals

Visually clarify:

- status;
- who acts next where supported;
- negotiation vs accepted deal;
- expiry;
- fulfillment.

## Analytics

Use refined KPI hierarchy.

Do not add charts merely to look professional.

## Store Settings

Use clean settings form sections.

---

# 25. Phase 14 — Admin workspace polish

Admin must feel efficient and serious.

## Admin sub-navigation

Upgrade `_AdminSubnav.cshtml` with:

- clear active state;
- compact layout;
- responsive behavior;
- Faed visual language.

## Overview

Use only meaningful operational data.

No decorative charts without analytical value.

## Queues

For verification, moderation, transactions, disputes, reviews, catalog, and audit:

1. title/context;
2. filter/search controls;
3. result table/list;
4. status;
5. row action;
6. pagination.

## Decision/detail pages

Visually separate:

- evidence/context;
- status/history;
- decision form;
- approval/reject/destructive actions.

Do not give approve/reject/destructive actions identical visual weight.

## Audit log

Keep dense and utilitarian.

---

# 26. Phase 15 — Responsive design QA

A visual redesign cannot be closed from source review alone.

Test real pages at minimum:

- 360 px;
- 390 px;
- 768 px;
- 1280 px;
- 1440 px.

Also inspect an intermediate laptop width if necessary.

## Required screens

### Public
- Home;
- Shop;
- Listing Details;
- Merchant Store;
- Privacy;
- 404/Error.

### Identity
- Login;
- Register;
- AccessDenied;
- reachable Manage page.

### Buyer
- Checkout;
- Orders;
- Order Details;
- Disputes.

### Merchant
- verification;
- listings;
- workspace/create;
- inventory;
- orders;
- offers;
- deals;
- analytics.

### Admin
- overview;
- one queue;
- one decision/detail;
- audit log.

Reject:

- page-level horizontal overflow;
- clipped menus;
- unusable tables;
- tiny tap targets;
- overlapping CTAs;
- huge dead space;
- broken hierarchy;
- unreadable labels;
- broken footer/nav stacking;
- clipped offcanvas/modal;
- filters that hide product discovery.

---

# 27. Phase 16 — Accessibility QA

Apply `faed-responsive-accessibility`.

Verify:

- one meaningful `h1`;
- logical headings;
- visible branded focus;
- labels;
- associated errors;
- semantic links/buttons;
- names for icon-only controls;
- accessible current nav state;
- accessible pagination;
- variant selection not color-only;
- statuses not color-only;
- practical contrast;
- keyboard operability;
- reduced motion;
- meaningful alt;
- decorative `alt=""`;
- Bootstrap modal/offcanvas focus behavior.

Do not break built-in Bootstrap accessibility while styling it.

---

# 28. Phase 17 — Front-end performance cleanup

Keep UI lightweight.

Audit:

- CSS duplication;
- obsolete scaffold CSS;
- unused custom selectors after changes;
- repeated inline styles;
- unnecessary JavaScript;
- console errors;
- image sizing;
- lazy loading;
- layout shift.

Do not delete vendor assets blindly.

No heavy visual framework may be added.

---

# 29. Inline style cleanup

Move repeated presentation rules into semantic Faed classes.

Keep dynamic inline values only when truly dynamic.

Goal:

- CSS owns presentation;
- Razor owns structure, content, and state.

Do not over-abstract one-off semantic markup.

---

# 30. JavaScript rules

JavaScript may enhance:

- gallery;
- variant selection;
- mobile filters;
- real submit/loading states;
- small interaction behavior.

Do not use JS for:

- authorization;
- business rules;
- server validation;
- data truth;
- hiding server mistakes;
- decorative animation better done with CSS.

No new JS dependency unless necessary and justified.

---

# 31. Cross-application consistency checklist

Verify one coherent treatment for:

- page titles;
- subtitles;
- section headers;
- breadcrumbs;
- buttons;
- forms;
- validation;
- panels;
- product cards;
- tables;
- status badges;
- alerts;
- pagination;
- filters;
- empty states;
- action bars;
- sub-navigation;
- trust indicators.

Public, Merchant, and Admin should share one language but different density.

---

# 32. UX copy polish

Do not change domain meaning.

Improve microcopy only where useful.

Prefer explicit verbs such as:

- `Browse the shop`
- `Apply as a merchant`
- `Order this item`
- `Make an offer`
- `Review application`
- `Update inventory`

Avoid vague `Submit` where a precise verb exists.

Do not expose internal enum/engineering terminology.

---

# 33. Visual QA evidence

Do not close TASK-013 from code review alone.

If screenshot/browser automation is available:

1. capture representative **before** screenshots;
2. implement;
3. capture matching **after** screenshots;
4. compare at least:
   - Home;
   - Shop;
   - Listing Details;
   - Login/Register;
   - Merchant Listings or Inventory;
   - Admin Overview or Queue;
   - mobile Home/Shop.

The improvement must be obvious side by side.

If screenshots are unavailable but a real browser is available:

- perform actual manual browser review;
- document exact pages and widths tested.

If real browser validation is impossible:

- do not claim plain `PASS`;
- use `PASS WITH DOCUMENTED LIMITATIONS`.

---

# 34. Role regression after visual changes

Retest:

## Anonymous
- public nav;
- Shop;
- Listing;
- protected routes.

## Buyer
- Buyer nav;
- checkout;
- orders;
- disputes/reviews eligibility.

## Merchant applicant
- state-sensitive navigation.

## Approved Merchant
- listings;
- inventory;
- orders;
- offers/deals.

## Admin
- Admin nav;
- no Buyer/Merchant operational links;
- queues/details.

---

# 35. State visual regression

Verify representative states:

- empty collection;
- no search results;
- pending verification;
- rejected verification/listing;
- live listing;
- sold out;
- limited/low stock where real;
- expired offer;
- accepted deal;
- dispute state;
- validation error;
- stale/concurrency result;
- 404/error;
- AccessDenied.

State meaning must not rely on color alone.

---

# 36. Final Faed visual quality gate

Run `.claude/skills/faed-ui-quality-gate`.

Reject completion if a major surface still looks like:

- default Bootstrap;
- default ASP.NET Identity;
- generic CRUD;
- generic AI dashboard;
- an inconsistent page from another product.

For each representative page ask:

## Visual distinctiveness
Does it look custom?

## Hierarchy
Is the primary CTA obvious?

## Commerce clarity
Are product, seller, price, condition, discount reason, and availability easy to scan?

## Trust
Does it feel credible without fabricated proof?

## Consistency
Do components belong to one system?

## Context
Does public feel more visual and dashboard more operational?

## States
Do actual states look intentional?

## Responsive/accessibility
Was real QA performed?

---

# 37. Visual acceptance criteria

TASK-013 cannot close until all applicable criteria pass.

## Global

- [ ] Navbar is clearly custom Faed UI.
- [ ] Footer is clearly custom Faed UI.
- [ ] Default ASP.NET/Bootstrap blue styling is gone from normal UI.
- [ ] Typography hierarchy is coherent.
- [ ] Spacing/radius/shadow system is consistent.
- [ ] Focus treatment is branded and accessible.

## Identity

- [ ] Login belongs visually to Faed.
- [ ] Register belongs visually to Faed.
- [ ] AccessDenied belongs visually to Faed.
- [ ] Reachable Manage pages inherit Faed styling.
- [ ] Authentication behavior remains intact.

## Public

- [ ] Home has clearly stronger marketplace identity.
- [ ] Shop feels like a polished browse experience.
- [ ] Product cards are polished.
- [ ] Listing Details feels like e-commerce.
- [ ] Merchant Store is credible and consistent.
- [ ] Privacy/Error/404 do not look scaffolded.

## Buyer

- [ ] Checkout looks like a checkout, not a raw form.
- [ ] Orders are visually scannable.
- [ ] Order Details have clear status/action hierarchy.
- [ ] Disputes/reviews states are intentional.

## Merchant

- [ ] Merchant subnav is polished.
- [ ] Listings/Inventory are operationally clear.
- [ ] Long forms are visually chunked.
- [ ] Orders/Offers/Deals are clearly differentiated.
- [ ] Analytics has intentional data hierarchy.
- [ ] Workspace no longer feels generic CRUD.

## Admin

- [ ] Admin subnav is polished.
- [ ] Overview/queues share one operational system.
- [ ] Decision pages have correct action hierarchy.
- [ ] Tables are readable and responsive.
- [ ] Admin looks like Faed, not a third-party dashboard.

## Responsive

- [ ] 360 px passes.
- [ ] 390 px passes.
- [ ] 768 px passes.
- [ ] 1280 px passes.
- [ ] 1440 px passes.
- [ ] no material horizontal overflow.
- [ ] navigation/filter/table/form behavior is usable.

## Accessibility

- [ ] keyboard critical flows pass.
- [ ] focus is visible.
- [ ] labels/errors are usable.
- [ ] statuses are not color-only.
- [ ] contrast is practical.
- [ ] reduced motion is respected.

## Quality

- [ ] final `faed-ui-quality-gate` passes.
- [ ] visual improvement is immediately obvious.
- [ ] no fake content was introduced.
- [ ] no heavy framework/dependency was added.

---

# 38. Build/Test/Schema regression gate

TASK-013 is visual, but all functional guarantees must remain intact.

Use the post-TASK-012 baseline from `PROJECT_STATUS.md`.

Known expected baseline from the TASK-012 completion report:

- `460 passed`
- `0 failed`
- `0 skipped`
- clean Debug build
- clean Release build
- no pending EF model changes

Run:

```text
dotnet build Faed.slnx -c Debug
dotnet build Faed.slnx -c Release
dotnet test Faed.slnx -c Release
dotnet ef migrations has-pending-model-changes
```

Use repository-established project/startup args if required for the EF command.

Rules:

- do not disable tests;
- do not accept a failure as “visual-only”;
- no migration/model drift is expected;
- investigate any new warning introduced by TASK-013.

---

# 39. Browser console gate

On representative pages verify:

- no JavaScript exceptions;
- no missing CSS/JS assets;
- no missing image requests caused by this task;
- no repeated console warnings introduced by new markup;
- Bootstrap dropdown/offcanvas/modal behavior still works.

---

# 40. Files expected to change

Exact changes depend on implementation, but likely visual files include:

## Shared

- `src/Faed.Web/Views/Shared/_Layout.cshtml`
- `src/Faed.Web/Views/Shared/_Layout.cshtml.css`
- `src/Faed.Web/Views/Shared/_LoginPartial.cshtml`
- `src/Faed.Web/Views/Shared/_ListingCard.cshtml`
- `src/Faed.Web/Views/Shared/_ShopBrowse.cshtml`
- `src/Faed.Web/Views/Shared/_Pagination.cshtml`
- `src/Faed.Web/wwwroot/css/site.css`
- `src/Faed.Web/wwwroot/css/faed.css`
- `src/Faed.Web/wwwroot/js/site.js` only if justified

## Public

- Home;
- Shop;
- Listing Details;
- Store;
- Privacy/Error/Status.

## Buyer

- Checkout;
- Orders;
- Disputes.

## Merchant

- `_MerchantSubnav`;
- core operational views.

## Admin

- `_AdminSubnav`;
- core queue/detail views.

## Identity

Only minimal files required by the selected safe styling/scaffolding approach.

Do not force every file to change if it already passes the visual quality gate.

---

# 41. Files that should normally not change

Unless a verified requirement demands it:

- domain models;
- EF mappings;
- migrations;
- business services;
- authorization policies;
- controllers;
- ViewModels;
- business contracts;
- unrelated tests.

This task should have a predominantly presentation-layer diff.

If controller/service changes become necessary, document the reason before changing them.

---

# 42. No commit/push

Do not commit or push unless explicitly requested later.

Keep changes logically understandable so they can be committed cleanly afterward.

---

# 43. Completion status rules

## `PASS`

Only if:

- build/tests/schema gate passes;
- required browser viewport QA was actually performed;
- final quality gate passes;
- major surfaces no longer look default/scaffolded.

## `PASS WITH DOCUMENTED LIMITATIONS`

Use if:

- implementation is safe and functionally clean;
- but browser/screenshot/device validation could not be fully performed;
- or a non-critical Identity/manage surface remains limited for a documented technical reason.

## `FAIL`

Use if:

- a P0/P1 visual/UX defect remains;
- functional regression exists;
- authentication/authorization became unsafe;
- responsive behavior remains materially broken;
- key surfaces still look default Bootstrap.

Do not report `PASS` merely because `dotnet test` succeeds.

---

# 44. Required completion report

Report:

## Final status

`PASS`, `PASS WITH DOCUMENTED LIMITATIONS`, or `FAIL`.

## Visual transformation

Summarize the biggest before/after differences.

## Shared shell

- Navbar;
- Footer;
- typography;
- design tokens;
- scaffold cleanup.

## Identity

- Login;
- Register;
- AccessDenied;
- Manage;
- whether scaffolding was used;
- confirmation that auth behavior stayed intact.

## Public

- Home;
- Shop;
- cards;
- Listing Details;
- Merchant Store.

## Buyer

- Checkout;
- Orders;
- Disputes/reviews.

## Merchant

- navigation;
- listings;
- inventory;
- forms;
- orders;
- B2B;
- analytics.

## Admin

- navigation;
- queues;
- details;
- tables;
- decision hierarchy.

## Responsive/accessibility

List exact widths/pages tested and fixes made.

## Visual QA evidence

State whether real browser review and/or before-after screenshots were performed.

## Regression evidence

Provide exact:

- Debug build result;
- Release build result;
- test count;
- failure/skip count;
- EF pending-model result.

## Files changed

List exact files.

## Remaining limitations

Only genuine remaining limitations.

---

# 45. Definition of Done

TASK-013 is complete only when both statements are true:

> A first-time user can open Faed and immediately perceive it as a coherent, intentionally designed, trustworthy marketplace — not as an ASP.NET Core/Bootstrap project with custom content.

and:

> Existing Faed business behavior, authorization, privacy, workflows, tests, and data semantics remain intact.

After a clean TASK-013 closure, do **not** open another general UI/UX task. Move to final documentation, ERD/delivery packaging, demo preparation, and deployment.
