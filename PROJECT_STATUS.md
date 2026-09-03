# Project Status

## Task status

| Task | Phase | Status |
|---|---|---|
| TASK-001 — Foundation | 0 | Completed |
| TASK-002 — Merchant Verification | 1 | Completed |
| TASK-003 — Catalog Foundations | 2 | Completed |
| TASK-004 — Listings, Variants, Inventory and Moderation | 3 | Completed |
| TASK-005 — Public Marketplace | 4 | Completed |

Execute tasks in queue order (`docs/00-SPEC-MAP.md`). Do not start TASK-006 until
explicitly requested.

## Current state

**Phase 4 — Public Marketplace complete (TASK-005).**

The anonymous-safe discovery experience (`IPublicMarketplaceService`) sits on top of the
TASK-003/004 catalog and listing aggregate with no schema changes: Home, Shop (filters +
paging), listing detail, and the merchant storefront all read exclusively through queries
scoped to `ListingStatus.Live` and `MerchantVerificationStatus.Approved`. See "TASK-005 —
Public Marketplace" below.

**Phase 3 — Listings, Variants, Inventory and Moderation complete (TASK-004).**

The `Listing` aggregate (options/values, sellable `ListingVariant`s, media, discount
reasons, reference-price evidence, moderation history), variant-level inventory with
`rowversion` concurrency and an `InventoryAdjustment` audit trail, and the merchant
listing workspace / admin moderation queue are implemented on top of the TASK-002/003
foundation. See "TASK-004 — Listings, Variants, Inventory and Moderation" below.

**Architecture restructure complete — single-project organized MVC.**

The former four-project solution (`Faed.Domain` + `Faed.Application` + `Faed.Infrastructure`
+ `Faed.Web`) was consolidated into a single `src/Faed.Web` project organized by folder
(`Models/{Entities,Enums,Identity}`, `Data/{Configurations,Migrations,Seed}`, `Services`,
`Areas`, `ViewModels`, `Authorization`). No behavior, schema, migration IDs or tests
changed — only namespaces and file locations moved (`Faed.Domain.* → Faed.Web.Models.*`,
`Faed.Application.* → Faed.Web.Services.*`, `Faed.Infrastructure.Persistence.* →
Faed.Web.Data.*`). See `docs/adr/0006-SINGLE-PROJECT-MVC.md`. `Faed.Domain`,
`Faed.Application` and `Faed.Infrastructure` were removed from the solution and repository.

**Phase 2 — Catalog Foundations complete (TASK-003).**

DB-driven taxonomy and disclosure reference data: hierarchical `Category`, the
`ConditionGrade` and `DiscountReason` reference tables, an optional admin-controlled
`Brand`, and an idempotent runtime `CatalogDataSeeder` that seeds condition grades A–D,
the eight PRD-approved discount reasons, and the launch taxonomy
(`Fashion Overstock` → Clothing, Shoes, Bags & Accessories). No catalog UI — full admin
catalog management is deferred to TASK-010.

**Phase 1 — Roles and Merchant Verification complete (TASK-002).**

Merchant application, private verification document handling, admin approval/rejection/
suspension, admin audit logging, and the `ApprovedMerchant` / `AdminOnly` authorization
policies are implemented on top of the TASK-001 foundation.

**Phase 0 — Foundation complete (TASK-001).**

The Visual Studio-generated `Faed.Web` baseline was audited and adopted, then the solution
foundation was completed around it.

### Phase 0 baseline audit — result: `PASS`

The Visual Studio baseline (commit `afe6003`, "chore: create MVC Identity baseline") was
correct and adoptable with no blocking issues. The structural changes made afterwards are
the expected TASK-001 Phase 2–3 foundation work, not corrections to a defective baseline.

| Audit item | Finding |
|---|---|
| Project template | ASP.NET Core Web App (Model-View-Controller), `Microsoft.NET.Sdk.Web` |
| Target framework | `net10.0`, nullable + implicit usings enabled |
| Authentication / Identity | Individual Accounts — `AddDefaultIdentity<IdentityUser>` + `Microsoft.AspNetCore.Identity.UI`; `Areas/Identity` present; Register/Login reachable |
| Generated database provider | **SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.11), LocalDB connection string — no SQLite, so no provider migration was needed |
| Solution structure | Repository root = solution root; `src/Faed.Web/`; no nested `Faed/Faed/` |
| Template migration | Legacy `00000000000000_CreateIdentitySchema` in `Faed.Web/Data/Migrations` |
| Build before restructuring | `dotnet build Faed.slnx` — succeeded, 0 warnings, 0 errors |
| Run before restructuring | App started; Home, `/Identity/Account/Register`, `/Identity/Account/Login` all HTTP 200; Identity schema applied to LocalDB |
| Git safety | `bin/`, `obj/`, `.vs/`, `*.user` correctly ignored; no secrets tracked |
| Blocking baseline issues | **None** |

### Post-audit foundation work (TASK-001 Phases 2–5)

> History note: TASK-001/002 used a four-project split; that structure was later
> consolidated into the single `src/Faed.Web` project (see "Current state" and
> `docs/adr/0006-SINGLE-PROJECT-MVC.md`). Paths below reflect the original layout.

EF/Identity moved out of the generated `Faed.Web` root into a persistence layer; user type
extended to `ApplicationUser` (`CreatedAtUtc`, `IsActive`); roles enabled and seeded
idempotently; template migration regenerated as `InitialIdentity`; `IClock` added;
unit + SQL Server integration test projects added. No product/marketplace features were
implemented.

## Active task

None. TASK-005 is closed.

Next: `tasks/TASK-006-B2C-ORDERS.md` (do not start until explicitly requested).

## TASK-005 — Public Marketplace

### Behaviour implemented

- `Services/Marketplace/IPublicMarketplaceService` + `PublicMarketplaceService` — the only
  code path the public pages use to read listing/merchant data. Every method filters to
  `ListingStatus.Live` (`GetHomePageAsync`, `BrowseListingsAsync`, `GetListingBySlugAsync`)
  and `MerchantVerificationStatus.Approved` (`GetMerchantStoreHeaderBySlugAsync`,
  and every card's `MerchantIsVerified` flag) — there is no query in this service capable of
  returning a Draft/PendingReview/Rejected/Hidden/SoldOut/Archived listing or a
  non-Approved merchant's storefront (docs/03-BUSINESS-RULES.md §2,
  docs/11-ACCEPTANCE-CRITERIA.md "Public sees only Live listings"). A category/condition/
  discount-reason/brand/merchant filter given as a slug or code that does not resolve to a
  real, active row returns zero results rather than being silently ignored — an
  unresolvable filter must never fall back to "show everything"
  (docs/06-ARCHITECTURE.md §12 "slugs are never authorization identifiers, but they are
  still real lookups"). Browsing is a two-phase read (filter/sort/page down to a bounded
  list of ids, then hydrate that page's rows against the small reference tables) so the
  filter query stays simple to translate while still touching each reference table once
  per call rather than once per row (docs/06-ARCHITECTURE.md §13).
- `PublicListingDetailView` / `ListingCardView` / `PublicMerchantProfileView` — display
  shapes deliberately separate from the merchant/admin `ListingDetailView`: moderation
  history, rejection notes, `HiddenByAdmin` and submission blockers are internal review
  state and have no field on the public shapes at all, so there is no way to accidentally
  render them into a public page (docs/08-SECURITY-AND-PRIVACY.md §3). Reserved/sold
  variant counters are likewise absent from the public variant view — only
  `AvailableQuantity`/`IsSellable`, matching faed-commerce-ux "avoid exact unit-count
  obsession" while still allowing an honest "only N left" / "sold out" state.
- `Controllers/{Shop,Listing,Store}Controller` — new public, anonymous, attribute-routed
  controllers (`/shop`, `/listing/{slug}`, `/store/{slug}`); `HomeController.Index` now
  renders the real marketplace home instead of the TASK-001 placeholder. A slug that does
  not resolve returns `NotFound()`, re-executed by a new
  `app.UseStatusCodePagesWithReExecute("/status/{0}")` into a branded empty state
  (`Home.StatusCodePage` / `Views/Home/StatusCode.cshtml`) instead of the framework's bare
  404 (docs/07-UI-UX-SPEC.md §12 "do not show generic blank pages").
- Views: `Home/Index` (hero, category navigation, featured listings, how-it-works,
  condition/discount transparency, merchant acquisition CTA), `Shop/Index` and
  `Store/Index` (share one `_ShopBrowse` partial — filters, sort, product grid, pagination,
  empty state — via the `IShopBrowsePageModel` interface so the two pages cannot drift
  visually or behaviourally), `Listing/Details` (gallery, price/condition/discount blocks,
  a vanilla-JS variant picker, availability state, a B2B block, and a defect/packaging
  disclosure section kept visually separate from ordinary product photos). Filters
  (`Areas`-free `ViewModels/Marketplace/ShopFilterModel`) round-trip through the query
  string; an unresolvable filter still renders the filter UI (so the visitor can change it)
  with a "no results" empty state, distinguished from the true "nothing live yet" empty
  state.
- The B2C "Add to Order" / B2B "Make an Offer" buttons are rendered `disabled` with a
  visible hint ("Online ordering isn't switched on yet") rather than posting to endpoints
  that do not exist yet — B2C/B2B ordering is TASK-006/007/008 scope
  (docs/10-IMPLEMENTATION-PLAN.md, faed-commerce-ux "Disabled CTAs must explain why").
  Nothing about checkout/cart/reservation was scaffolded ahead of that work.
- New CSS component layer appended to `wwwroot/css/faed.css` (hero, product card, price
  block, category grid, variant picker, gallery, filter panel, sort bar, pagination, store
  header, how-it-works/transparency sections) — reuses the existing design tokens, no raw
  Bootstrap card/button styling introduced (faed-ui-direction). The mobile filter drawer is
  Bootstrap 5.3's responsive `offcanvas-lg` component (a static sidebar at ≥lg, a slide-in
  drawer with its own close button below it), not a hand-rolled one.
- No schema/migration changes — every entity, index and enum this task reads
  (`Listing`, `ListingVariant`, `ListingMedia`, `ListingDiscountReason`, `Category`,
  `ConditionGrade`, `DiscountReason`, `Brand`, `MerchantProfile`) already existed from
  TASK-003/004.

### Accessibility fixes made during the mandatory review (faed-responsive-accessibility, faed-ui-quality-gate)

- The product card's verified checkmark was `aria-hidden` with only a mouse-hover
  `title`, so a screen-reader user got no verification signal at all; added a
  `visually-hidden` "Verified merchant." text alternative alongside the icon.
- Gallery thumbnail buttons had no accessible name (an empty decorative `alt` inside an
  unlabelled `<button>`); added `aria-label` per thumbnail and grouped them under
  `role="group" aria-label="Product photos"`.
- Variant option chip groups had a plain `<span>` label with no programmatic association;
  each group is now `role="group" aria-labelledby="option-label-{id}"`.
- The JS variant picker toggled an `is-unavailable` CSS class without updating
  `aria-disabled`, so a keyboard/assistive-tech user got a silent no-op click with no
  indication why; the picker now sets `aria-disabled` alongside the class.
- Selected variant chips were distinguished by background colour alone; added a
  non-colour checkmark glyph and bold weight so the selected state survives a
  colour-only reading (faed-responsive-accessibility "selected variant state is visible
  beyond color").
- Variant chips and gallery thumbnails were undersized for touch (well under 44px) and had
  no explicit focus-visible style; both now meet the same tap-target/focus conventions as
  `.faed-btn`.
- A CSS rule that hid the mobile filter drawer's close header relied on an accidental
  specificity tie-break to be re-shown below the `lg` breakpoint rather than an explicit
  media query; rewritten so the desktop-only hide is itself inside `@media (min-width: 992px)`.
- The Shop/Store empty state said "No listings match your filters" even when zero filters
  were active (i.e. the marketplace is simply empty) — split into two distinct, correctly
  worded empty states.

### Exit-criteria coverage (tasks/TASK-005)

| Exit criterion | Covered by |
|---|---|
| Anonymous user can understand a listing without hidden critical information | `Listing/Details.cshtml` renders merchant + verified state, condition + meaning, why discounted, defect evidence (separate, labelled section), reference price only when it passed moderation, variant availability, B2C/B2B availability and fulfillment/policy text above and immediately below the fold |
| Non-Live listings cannot be accessed publicly | `PublicMarketplaceServiceTests.GetListingBySlugAsync_OnlyEverReturnsALiveListing` (Draft/PendingReview/Hidden all `null`, same slug); `BrowseListingsAsync_ExcludesNonLiveListings_AndAnUnresolvableCategoryYieldsZeroResults`; manual verification (a raw-SQL Draft listing 404s on `/listing/{slug}` and is absent from `/shop` search) |
| Mobile layout checked | faed-responsive-accessibility pass above; `offcanvas-lg` filter drawer, single-column product grid/listing layout below their breakpoints |
| Accessibility baseline checked | faed-responsive-accessibility pass above (fixes listed) |

### Manual end-to-end verification (real MVC pipeline, real SQL Server)

Ran the app against LocalDB with a merchant/listing pair inserted directly (bypassing the
already-covered TASK-002/004 flows) to exercise the read side under real HTTP: Home shows
the featured listing, category counts and verified badge; `/shop` filters correctly by
category/condition/channel/price/search, and an unresolvable category returns a genuine
zero-result empty state (not "show everything"); `/listing/{slug}` renders price, the
40%-lower discount computed from reference vs. retail price, condition meaning, the B2B
block, and the variant-availability JSON payload; `/store/{slug}` shows the verified badge
and listing count; a Draft listing inserted at the same time returns 404 on its own slug and
is absent from `/shop` search results; `/listing/does-not-exist` and `/store/does-not-exist`
both render the branded 404 page. Test data removed afterwards.

### Not implemented (correctly deferred)

B2C cart/order placement, B2B negotiation/offers, real "How It Works" as a separate route
(folded into a Home section instead), per-facet result counts on the filter sidebar,
multi-select filters (each dimension is single-select via the query string) — none of these
are TASK-005 deliverables (docs/10-IMPLEMENTATION-PLAN.md Phase 4 scope).

### Post-review fixes (code review after initial TASK-005 completion)

A review of the initial TASK-005 implementation found two P1 defects and eight P2 issues.
All are fixed.

- **A suspended merchant's Live listings stayed fully public.** Every public read
  (`PublicMarketplaceService.GetHomePageAsync`/`BrowseListingsAsync`/`GetListingBySlugAsync`
  and `ListingMediaService.OpenImageAsync`) filtered only on `ListingStatus.Live` — suspending
  a merchant changes only `MerchantProfile.VerificationStatus`, so their listings (and photos)
  kept appearing to anonymous visitors while only the storefront page itself 404'd
  (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved"). Added a single
  `PublicLiveListings()` gate (`Status == Live` **and** the owning merchant is currently
  `Approved`) that every browse/home/detail query now goes through, and the equivalent check
  in `ListingMediaService.OpenImageAsync`. Regression-covered end-to-end:
  `PublicMarketplaceServiceTests.SuspendingTheMerchant_HidesTheirLiveListingEverywhere_*`
  (service layer) and `PublicMarketplaceHttpTests.LiveListing_IsReachableByAnonymousHttp_*`
  (real HTTP — detail page, store page and image all flip to 404/403 the moment the merchant
  is suspended, while the `Listing.Status` row is untouched).
- **A listing could publish with a disclosed defect and no evidence photo.** Submission
  already required *a* discount reason and *a* product photo, but never required a defect or
  packaging photo for Grade B ("packaging imperfection"), Grade D ("cosmetic imperfection"),
  or the `PackagingDamage`/`CosmeticDefect` discount reasons — an admin could approve a listing
  that claims a physical flaw with nothing showing it (docs/03-BUSINESS-RULES.md §3 "defects
  must be disclosed and visually evidenced where applicable"). `Listing.DescribeSubmissionBlockers`
  and `Listing.SubmitForReview` now take the resolved condition-grade code and discount-reason
  codes (the aggregate itself only ever stored catalog ids) and block submission until a
  `Defect` or `Packaging` photo exists; `MerchantListingService.SubmitForReviewAsync` resolves
  those codes from the database before calling either. Covered by 6 new
  `Faed.UnitTests.ListingTests` cases (grade B/D and each reason, both the throw and the
  succeeds-once-photographed path) and
  `PublicMarketplaceServiceTests.SubmitForReviewAsync_ConditionGradeClaimsAPhysicalImperfection_WithoutEvidence_IsRejected`
  against real SQL Server.
- **The marketplace was not actually bounded to the Fashion Overstock launch sector.** Home's
  category navigation, the Shop category facet, and browse itself considered every active
  non-root category and every Live listing globally — today's seed data happens to have only
  the three launch categories, but nothing stopped a category added under an unrelated future
  sector from immediately appearing in the MVP UI (AGENTS.md §3 "Do not expose unrelated
  sectors in the MVP UI"). Added `GetLaunchSectorCategoryIdsAsync`, which walks the category
  tree from `CatalogDataSeeder.RootCategorySlug`, and applied it to every category-facing
  query; a category slug outside that set now resolves the same as one that does not exist.
  Covered by `BrowseListingsAsync_NeverExposesACategoryOutsideTheLaunchSector`, which inserts a
  real second-root "Electronics → Phones" branch and proves it is invisible to Home, the Shop
  facet list, and direct-slug browsing alike.
- **The sort dropdown could get stuck on the previous choice.** The sort mini-form emitted a
  hidden `Sort` input carrying the *current* sort value and then, immediately after, a
  `<select name="Sort">` with the newly chosen one — two same-named controls in one form, and
  ASP.NET Core model binding takes the first value in the query string, so a change away from
  "Newest" silently kept losing to the stale hidden value. Fixed by excluding `Sort` from the
  hidden-field loop in that one form (pagination/other links still carry it via
  `ToFilterRouteValues()`, which is unaffected).
- **An out-of-range page produced an empty page with a positive total, and an unbounded
  multiplication.** `Page` was lower-bounded but never capped to the real last page, so a
  hand-edited `?Page=999999` returned zero items next to a nonzero `TotalCount` — which the UI
  then misreported as "No listings here yet" with no way back — and left `(page - 1) * pageSize`
  unbounded by anything but the caller's input. `BrowseListingsAsync` now clamps `page` to the
  computed `TotalPages` once the true count is known, before paging. Covered by
  `BrowseListingsAsync_OutOfRangePage_ClampsToTheLastRealPage_*`.
- **Size and colour filtering, required by docs/07-UI-UX-SPEC.md §4, did not exist.** Generic
  listing options have no shared reference table (each merchant names their own "Size"/
  "Colour"), so `ShopQuery` gained `SizeValue`/`ColorValue` matched case-insensitively against
  option name aliases, `ShopFacets` gained `Sizes`/`Colors` (distinct values actually in use,
  scoped the same way the Brand facet already was), and the Shop/Store filter panel renders
  both when any values exist. Covered by `BrowseListingsAsync_FiltersBySizeAndColour`.
- **Browsing only ever used `RetailPrice`, even for wholesale-only listings.** A listing with
  `AllowB2C == false` has no `RetailPrice` at all (docs/04-DOMAIN-MODEL.md §3), so it was
  invisible to every price filter, mis-sorted as free/priceless, and its card said "Price on
  request" despite carrying a real `WholesaleIndicativeUnitPrice`. Price filtering and sorting
  now use `RetailPrice ?? WholesaleIndicativeUnitPrice`; `ListingCardView` gained
  `EffectivePrice`/`EffectivePriceIsWholesale`, rendered on the card and on the listing-detail
  price block as "JOD X /unit · wholesale". Covered by
  `BrowseListingsAsync_AWholesaleOnlyListing_IsPriceFilterableAndSortable_ByItsIndicativePrice`
  and `BrowseListingsAsync_SortsByPrice_UsingTheEffectivePriceForBothDirections`.
- **The channel filter's labels contradicted its behaviour.** "Retail only" and "Wholesale
  only" implied exclusivity but matched inclusively (`AllowB2C`/`AllowB2B` alone), so a
  dual-channel listing appeared under both — arguably the more useful behaviour for a buyer
  checking "can I buy this at retail", but not what the labels promised. Kept the inclusive
  behaviour (excluding dual-channel listings from a retail-minded filter would be a worse
  result for that buyer) and relabelled the options "All listings" / "Retail available" /
  "Wholesale available", with a one-line hint that a listing can offer both. Regression test
  renamed to `BrowseListingsAsync_FiltersByChannel_Inclusively` to say what it actually proves.
- **Sold-out variant combinations were selectable in the picker.** `refreshDisabledStates`
  marked a combination "possible" whenever *any* variant matched it, ignoring
  `variant.sellable` — a combination that exists but is depleted was never disabled
  (faed-commerce-ux "disable unavailable combinations"). Fixed to require a *sellable* match;
  also added `aria-live="polite"` to the availability text so the change is announced, not
  only visible. Covered indirectly by
  `GetListingBySlugAsync_VariantAvailability_ReflectsDepletedStock` (the data source
  the picker consumes); the picker script itself has no test harness in this repo.
- **Fulfillment copy asserted capabilities nothing backs, and was wrong for B2B-only
  listings.** Every listing stated "Pickup and merchant delivery options are confirmed at
  checkout" regardless of channel — merchant pickup/delivery capability is not modelled yet
  (TASK-004/005 status), and B2B fulfillment is direct pickup or seller-arranged shipping, not
  a B2C checkout at all (docs/03-BUSINESS-RULES.md §12). Split into a retail line (shown only
  when `AllowB2C`, softened to "will be shown once ordering opens") and a wholesale line (shown
  only when `AllowB2B`).
- **TASK-005's tests only ever exercised the service layer.** Added
  `PublicMarketplaceHttpTests` (`WebApplicationFactory`, real MVC pipeline, no auth) covering
  `/shop`, `/listing/{slug}` (404 for unknown, 200 with content for Live), `/store/{slug}`
  (404 for unknown), and `/listing-images/{id}` — including the same merchant-suspension
  scenario as the service-level test, proving the HTTP surface (not just the service method)
  flips to 404/403. The pre-existing suspension test was also widened: it previously checked
  only that the storefront header disappeared, which would have missed the primary P1 defect
  entirely.

### Validation (TASK-005, after the fix pass)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **207 passed (143 unit, 64 integration)**, 0 failed, 0 skipped.
  `Faed.UnitTests.ListingTests` grew by 6 (defect-evidence rule); `Faed.IntegrationTests`
  grew by 13 (10 more `PublicMarketplaceServiceTests` + a new 4-test `PublicMarketplaceHttpTests`,
  net of the pre-existing suspension test being widened in place).
- No new migration — `dotnet ef migrations has-pending-model-changes` reports no drift (the
  fix is entirely query/domain-method-signature scoped, no schema change).
- Manual end-to-end re-verification against LocalDB with a retail listing (Colour option) and
  a wholesale-only listing (no retail price) seeded directly: Shop's filter panel shows the
  Colour facet and the corrected "All listings / Retail available / Wholesale available"
  labels; the wholesale-only card and its detail page both show "JOD 3.250 /unit · wholesale"
  instead of "Price on request"; its detail page shows only "Wholesale fulfillment", not a
  retail line; `?Sort=PriceLowToHigh` round-trips correctly; `?Page=9999` returns the real
  last page instead of an empty one.

### Post-review fixes (second code review — marketplace + moderation)

A second review found ten issues (two High, six Medium, two Low). All are fixed.

- **[High] Required defect evidence could be removed after publication, and approval never
  re-checked it.** Removing the last packaging photo was not treated as material, so a Grade
  B/D or `PackagingDamage`/`CosmeticDefect` listing could go (or stay) Live with no visual
  evidence, and `Listing.Approve` published whatever the listing currently was without
  re-running the submission checks. `Listing.RemoveMedia` now takes the resolved
  condition-grade / discount-reason codes and refuses to drop the last `Defect`/`Packaging`
  photo when `Listing.DisclosesAPhysicalImperfection` is true (same shape as the existing
  last-product-photo guard); `ListingModerationService` re-runs `DescribeSubmissionBlockers`
  on approve and returns a conflict if any blocker reappeared. The shared code-resolution
  helper moved to `ListingQueries.LoadDisclosureCodesAsync`. Covered by 3 new
  `ListingTests` cases and
  `ListingServiceTests.RemoveImage_TheLastDisclosurePhotoOfAGradeBListing_IsRejected_*`.
- **[High] Size/colour filters were not variant-aware.** The filter only checked that the
  option value existed somewhere on the listing, so "White + M" matched a listing stocking
  only White/L and Black/M, whose requested SKU cannot be bought. `BrowseListingsAsync` now
  requires a single active, in-stock `ListingVariant` carrying every requested value together,
  and the size/colour facets are built the same way (values on a sellable variant only).
  Covered by
  `PublicMarketplaceServiceTests.BrowseListingsAsync_SizeAndColour_MustBeSatisfiedByOneSellableVariant_*`.
- **[Medium] Card hydration could expose a listing after its visibility changed.**
  `HydrateCardsAsync` loaded the paged ids without re-checking status/merchant approval; it
  now re-applies `PublicLiveListings()`, so a moderation hide or merchant suspension between
  the id query and the hydration query drops the card instead of rendering it.
- **[Medium] The launch-sector root lookup was case-sensitive.**
  `GetLaunchSectorCategoryIdsAsync` compared the root slug with in-memory `==` after
  materialization while the seeder matches case-insensitively; a differently-cased existing
  root made Home and Shop appear empty. Now `StringComparison.OrdinalIgnoreCase`.
- **[Medium] Paging order was nondeterministic for ties.** Every sort now ends on `l.Id`, a
  unique final key, so listings tied on price and publication timestamp keep a stable order
  across page requests.
- **[Medium] Facet construction loaded the whole catalog into memory.** The size/colour facet
  query pulled every matching `Listing` and its full option graph; it now projects the
  distinct `(option name, value)` pairs in the database.
- **[Medium] Public filter input had no server-side validation.** `ShopFilterModel` gained
  `[Range]`/`[StringLength]`/`[EnumDataType]` attributes, and `ToQuery` now clamps negative
  prices, swaps a reversed min/max range, caps search text at 100 characters, and falls back
  to the default for an undefined `Channel`/`Sort` enum value.
- **[Medium] Defect evidence sat below the description and all policy content.**
  `Views/Listing/Details.cshtml` was reordered to the trust-first sequence from
  faed-commerce-ux: purchase block (with a short fulfillment summary) → disclosed-issue
  photos → description → details & policies → wholesale block.
- **[Low] Filter radio groups had no programmatic group label.** The Shop/Store filter panel
  now uses `<fieldset>`/`<legend>` per group instead of a visual `<span>` heading.
- **[Low] The mobile filter control showed only a bullet.** `ShopFilterModel.ActiveFilterCount`
  is rendered as a numeric badge on the "Filters" toggle so one vs several applied filters is
  visible.

No new migration — the changes are query, view, view-model and domain-method-signature
scoped, with no schema change.

### Post-review fixes (third pass — three TASK-005 blocking defects)

A follow-up review flagged three blocking defects still inside TASK-005 scope. All are fixed;
scope was held to these three findings.

- **The Fashion launch-sector restriction was not enforced on direct listing-detail access.**
  Home and Shop already scoped every query to the `Fashion Overstock` category subtree
  (`GetLaunchSectorCategoryIdsAsync`), but `PublicMarketplaceService.GetListingBySlugAsync`
  filtered only on `PublicLiveListings()` (Live + approved merchant). A Live listing filed
  under a category outside the launch sector — a future, unrelated sector added as data — was
  therefore a 404 in browse but fully readable by guessing its slug at `/listing/{slug}`, a
  hole straight through AGENTS.md §3 "Do not expose unrelated sectors in the MVP UI".
  `GetListingBySlugAsync` now resolves the launch-sector category set and requires
  `launchCategoryIds.Contains(l.CategoryId)`, exactly as Home/Shop do. Regression-covered by
  `PublicMarketplaceServiceTests.GetListingBySlugAsync_ForALiveListingOutsideTheLaunchSector_ReturnsNull`
  (service, with an in-sector control listing proven still reachable) and
  `PublicMarketplaceHttpTests.ListingDetail_ForALiveListingOutsideTheLaunchSector_Returns404`
  (real HTTP route, `Listing.Status` confirmed `Live` the whole time).

- **Product-image changes on a published listing bypassed moderation.** `Listing.AddMedia` /
  `Listing.RemoveMedia` treated only `Defect` photography as material; adding, replacing or
  removing a publicly visible `Product` photo on a `Live`/`SoldOut` listing only `Touch`ed it
  and left it public, so a merchant could swap the gallery a buyer judges the item by with no
  review (AGENTS.md §8 "Do not let a merchant edit a live listing … and bypass review").
  A new `Listing.IsMaterialMedia` predicate (`Product` **or** `Defect`) now gates both
  mutators: adding/removing a Product photo routes through `ApplyMaterialChange`, so a
  `Live`/`SoldOut` listing returns to `PendingReview` with a fresh `ListingModeration` row
  (preserving the prior approval in history, the same approved/public-version semantics every
  other material change already uses), and the mutator throws outright while the listing is
  `PendingReview`. Ordinary `Packaging` shots stay non-material (they keep their existing
  last-disclosure-photo guard). `ListingModerationService.ApproveAsync` already re-runs
  `DescribeSubmissionBlockers` on approve, so a re-review cannot publish a gallery that has
  dropped below one product photo. Regression-covered by `ListingTests`
  (`AddMedia_ProductPhoto_OnLiveListing_ReturnsToPendingReview_PreservingApprovalHistory`,
  `RemoveMedia_ProductPhoto_OnLiveListing_WhenAnotherRemains_ReturnsToPendingReview`,
  `AddMedia_ProductPhoto_WhilePendingReview_Throws`,
  `AddMedia_PackagingPhoto_OnLiveListing_StaysLive`) and, against real SQL Server + real file
  storage, `PublicMarketplaceServiceTests.AddImageAsync_AProductPhotoOnALiveListing_ReturnsItToPendingReview_AndDropsItFromPublicView`
  (listing 404s from the public detail path and the new image is not anonymously served while
  pending).

- **The variant picker trapped the buyer on disabled option values.** `refreshDisabledStates`
  in `wwwroot/js/listing-detail.js` marked an option chip unavailable whenever no *sellable*
  variant matched the chip's value **combined with the current selection in every other
  option group**. From a valid `Black/M`, every `White` chip then disabled itself against the
  selected size `M` (because `White/M` is not a real variant), even though `White/L` is
  perfectly sellable — so the buyer could not move from `Black/M` to `White/L` at all. The
  rule is now per-value: a chip is disabled only when *no* sellable variant carries that
  value, independent of the other groups' current selection. Impossible partial combinations
  (e.g. `White` + `M`) are surfaced by the existing availability line
  ("That combination is not available.") instead of a dead-end disabled chip. A depleted or
  deactivated value with no sellable variant left is still disabled, so the earlier
  "sold-out combinations are selectable" fix is preserved. The same rule is now also computed
  server-side — `PublicListingDetailView.SellableOptionValueIds` — and rendered into the
  initial chip markup so a no-JS page is correct too. Regression-covered by
  `PublicListingDetailViewTests` (the `Black/M` + `White/L` anti-trap case, the
  depleted/inactive-value exclusion, and the no-options case).

No new migration — every change is query, domain-method, view-model or client-script scoped;
`dotnet ef migrations has-pending-model-changes` reports no drift.

### Validation (TASK-005, third fix pass)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **222 passed (153 unit, 69 integration)**, 0 failed, 0 skipped,
  on a workstation with SQL Server LocalDB reachable. New since the previous pass: 7 unit
  (`ListingTests` ×4, `PublicListingDetailViewTests` ×3) and 3 integration
  (`PublicMarketplaceServiceTests` ×2, `PublicMarketplaceHttpTests` ×1).
- `dotnet ef migrations has-pending-model-changes` — no changes to the model since the last
  migration.
- Targeted verification only, scoped to the three findings and regressions their changes
  could cause (per the task instruction) — no fresh broad review.

## TASK-004 — Listings, Variants, Inventory and Moderation

### Behaviour implemented

- `Listing` aggregate (`Models/Entities/Listing.cs`) — owns `ListingOption`/`ListingOptionValue`,
  `ListingVariant` (+ `ListingVariantOptionValue` join), `ListingMedia`, `ListingDiscountReason`,
  `ListingReferencePriceEvidence` and `ListingModeration` as private backing-field collections.
  Holds no authoritative stock total (AGENTS.md Rule A); `AvailableUnits` is derived from
  active variants. Every mutator that changes a material claim (category, brand, condition,
  title, description, included/missing items, prices, sales channels, discount reasons,
  options/variants, defect photos) routes through `ApplyMaterialChange`, which takes a
  `Live`/`SoldOut` listing back to `PendingReview` and opens/extends a `ListingModeration`
  row, or returns a `Hidden` listing to `Draft` so `Restore` can never republish unreviewed
  content (AGENTS.md §8, docs/02-SCOPE-AND-DECISIONS.md "Listing moderation policy").
  Non-material fields (return policy, warranty, mixed-lot flag, ordinary/packaging photos,
  reference-price evidence additions, stock quantity) do not reopen moderation.
  `UpdateDetails` applies the business-detail fields *and* the discount-reason set as one
  atomic transition under a single `RequireMaterialEditAllowed()` check — see "Bug found and
  fixed" below.
- `ListingVariant` — the authoritative stock record (AGENTS.md Rule A, docs/adr/0002).
  `AvailableQuantity`/`ReservedQuantity`/`SoldQuantity`/`InitialQuantity` all `>= 0`, enforced
  by a domain guard in `AdjustAvailable` *and* a database check constraint
  (`CK_ListingVariants_Quantities_NonNegative`). SQL Server `rowversion` `RowVersion` is
  present from the migration that first creates the table. `OptionCombinationKey` is a
  deterministic, order-independent fingerprint of the variant's option-value set, backed by
  a unique index (`ListingId`, `OptionCombinationKey`) so a duplicate combination is rejected
  by the database even when two requests race each other, not only by the in-memory aggregate
  check.
- `ListingMedia` / `ListingDiscountReason` / `ListingReferencePriceEvidence` / `ListingModeration`
  / `InventoryAdjustment` — supporting entities per docs/04-DOMAIN-MODEL.md §3-5. Photos and
  evidence store only a private `IFileStorage` object key, never a public URL
  (docs/08-SECURITY-AND-PRIVACY.md §3). `InventoryAdjustment` records who/when/variant/before/
  after/reason for every manual stock correction (docs/03-BUSINESS-RULES.md §6); stock is
  never silently overwritten.
- `Services/Listings` — `MerchantListingService` (`IMerchantListingService`): create/edit a
  listing, manage options/values/variants, upload/remove product/defect/packaging photos and
  reference-price evidence, submit for review, hide/restore/archive. Re-resolves the owning
  Approved merchant from the database on every call (never trusts a route/form value,
  docs/08-SECURITY-AND-PRIVACY.md §6, §9); a guessed listing id reads as "not found" for a
  non-owner. Uploads are buffered, structurally validated with the same fail-closed inspector
  already hardened for merchant verification documents
  (`ListingImageValidator` → `VerificationDocumentValidator.ValidatePayload`,
  docs/adr/0007), and only then stored — an upload that fails after the bytes are written is
  cleaned up rather than left orphaned.
  `InventoryService` (`IInventoryService`): variant-level stock adjustments, each written
  together with its `InventoryAdjustment` row and a listing `RefreshAvailability` call inside
  one transaction (AGENTS.md §7); a stale `rowversion` surfaces as `Result.Conflict`, never a
  raw DB exception (docs/06-ARCHITECTURE.md §9). `ListingModerationService`
  (`IListingModerationService`): admin queue, approve/reject/hide, each decision written with
  its `AdminActionLog` entry in one transaction. `ListingMediaService`
  (`IListingMediaService`): resolves one listing image for a caller entitled to see it —
  anyone once the listing is `Live`/`SoldOut`, otherwise only the owning merchant or an admin.
- `Areas/Merchant/Controllers/{Listings,Inventory}Controller` and
  `Areas/Admin/Controllers/ListingModerationController` — thin controllers behind the
  `ApprovedMerchant` / `AdminOnly` policies; `Controllers/ListingMediaController`
  (`/listing-images/{id}`) is the only route that ever serves a listing image, allows
  anonymous requests, and defers every visibility decision to `IListingMediaService`.
- Merchant listing workspace (`Areas/Merchant/Views/Listings/{Index,Create,Workspace}.cshtml`)
  and inventory screen (`Areas/Merchant/Views/Inventory/Index.cshtml`), admin moderation queue
  and detail (`Areas/Admin/Views/ListingModeration/{Index,Details}.cshtml`) — Faed design
  system components (`faed-section`, `faed-thumb-grid`, `faed-chip`, `faed-stat`,
  `faed-blockers`, native `<dialog>` for the stock-adjustment form), DB-driven category/
  condition/brand/discount-reason choices (no hard-coded catalog values), submission blockers
  surfaced as plain sentences, defect photos visually distinguished from product/packaging
  photos.
- Migration `20260901141629_AddListingsAndInventory` — all TASK-004 tables; `RowVersion`
  present on `ListingVariants` from this first migration; `dotnet ef migrations
  has-pending-model-changes` reports clean.

### Bug found and fixed during manual verification

Running the full merchant → submit → admin-approve → material-edit flow through the real
MVC pipeline against SQL Server (registration, verification, listing creation, variant/photo
upload, submission, approval, then editing the live listing's title) surfaced a real defect
that the build and the unit-only pass had not: `MerchantListingService.ApplyDetails`
originally called `Listing.UpdateDetails(...)` and then a separate `Listing.SetDiscountReasons(...)`.
When the business-detail edit was material (for example a title change on a `Live` listing),
`UpdateDetails` correctly transitioned the in-memory aggregate to `PendingReview` — but the
very next call, `SetDiscountReasons`, re-checked `RequireMaterialEditAllowed()` and now saw a
`PendingReview` listing, throwing "This listing is being reviewed and cannot be edited until a
decision is made." even though the discount reasons themselves had not changed. The edit
silently failed (redisplayed the form with that error) whenever both a material field and the
existing discount reasons were submitted together — i.e. on every normal save from the
workspace form. Fixed by merging discount-reason assignment into `UpdateDetails` itself, so
every field the business-details form submits — including discount reasons — is validated and
applied under one `RequireMaterialEditAllowed()` check and one `ApplyMaterialChange` transition.
Regression-covered by `Faed.UnitTests.ListingTests.MaterialEdit_OnLiveListing_ReturnsToPendingReview_WithoutLosingApprovalHistory`
and, against real SQL Server, `Faed.IntegrationTests.ListingServiceTests.MaterialEdit_OnALiveListing_ReturnsItToPendingReview_AndPreservesTheApprovalHistory`.

### Manual end-to-end verification (real MVC pipeline, real SQL Server, real file storage)

Performed via HTTP against the running app (LocalDB `Faed`, `LocalFileStorage`), not only
through automated tests:
register → confirm email → apply as merchant → upload a genuine PDF (passes the fail-closed
inspector) → submit → admin approves → merchant creates a Draft listing → adds a `Size`
option with `M`/`L` values → adds variant `SNK-BLK-M` (5 units) → uploads a genuine PNG
product photo (passes the fail-closed inspector) → sets retail price + discount reason →
submits for review → admin approves → listing is `Live` and its photo is publicly reachable
anonymously → merchant adjusts stock (`+3`, audited, before/after correct) → merchant edits
the title on the `Live` listing → listing correctly returns to `PendingReview`, its photo
stops being publicly reachable, and the prior approval is preserved in history (this run is
what surfaced the bug above) → admin rejects the new version → an over-large negative stock
adjustment is rejected server-side without changing the stored quantity.

### Exit-criteria coverage (tasks/TASK-004)

| Exit criterion | Covered by |
|---|---|
| Variant combination is unique | `ListingTests.AddVariant_DuplicateOptionCombination_Throws` (aggregate); `ListingServiceTests.DuplicateOptionCombination_IsRejectedByTheDatabase_EvenAcrossTwoConcurrentContexts` (unique index, real SQL Server) |
| Stock is variant-level | `ListingTests.AddVariant_DistinctCombinations_BothSucceed` (Black/M, Black/L, White/M example); `ListingVariantTests` |
| Quantities cannot become negative | `ListingVariantTests.AdjustAvailable_NegativeDeltaExceedingStock_Throws`; DB check constraint; `ListingServiceTests.AdjustStock_CannotGoNegative_AndIsAudited`; manual verification |
| Live listing material edit requires moderation | `ListingTests.MaterialEdit_OnLiveListing_ReturnsToPendingReview_WithoutLosingApprovalHistory`; `ListingServiceTests.MaterialEdit_OnALiveListing_ReturnsItToPendingReview_AndPreservesTheApprovalHistory`; manual verification (the bug this caught) |
| Public cannot see non-Live data | `ListingServiceTests.NonLiveListing_ImageIsHiddenFromAnonymous_ButVisibleToOwnerAndAdmin_AndPublicOnceLive`; manual verification (anonymous 403 before approval, 200 after) |
| Defect media is distinguishable | `ListingMediaType.Defect` kept separate from `Product`/`Packaging`; workspace/admin views label it explicitly |
| Migration includes RowVersion from first variant creation | `20260901141629_AddListingsAndInventory` creates `ListingVariants.RowVersion` as `rowversion` in its `CREATE TABLE` |

### Additional coverage

- Manual stock adjustment is audited: `ListingServiceTests.AdjustStock_CannotGoNegative_AndIsAudited`.
- Optimistic concurrency on `ListingVariant.RowVersion`, proven against real SQL Server:
  `ListingServiceTests.AdjustStock_TwoConcurrentContexts_OnlyTheFirstSaveSucceeds` (AGENTS.md
  §7). The literal "two buyers race for the last unit" scenario is B2C order-flow scope
  (TASK-006); this proves the concurrency token itself stops a lost update on the variant it
  protects.
- Condition grade and discount reasons are independent on a listing:
  `ListingTests.ConditionGrade_And_DiscountReasons_AreIndependentOnAListing` (AGENTS.md Rule B
  — a Grade A item can still carry a past-season/overstock reason).
- Submission blockers are real, server-checked sentences, not just UI hints:
  `ListingTests.SubmitForReview_WithoutProductPhoto_Throws`,
  `SubmitForReview_WhenB2CWithoutRetailPrice_Throws`,
  `SubmitForReview_ReferencePriceWithoutEvidence_Throws`,
  `SubmitForReview_ReferencePriceNotHigherThanRetail_Throws`.
- `Approve` publishes as `Live` when stock exists and as `SoldOut` (addressable, not
  purchasable) when it does not: `Approve_WithStock_PublishesAsLive`,
  `Approve_WithNoStock_PublishesAsSoldOut_NotLive`.
- Editing while `PendingReview` is rejected server-side: `Edit_WhilePendingReview_Throws`.

### Not implemented (correctly deferred)

Public marketplace browsing/listing-detail pages (TASK-005), B2C ordering and reservation
(TASK-006), B2B negotiation/deals (TASK-007/008), demo listing seed data
(docs/12-SEED-DATA.md explicitly defers this until the phases that consume it exist).

### Post-review fixes (code review after initial TASK-004 completion)

A follow-up review of the initial TASK-004 implementation found six real gaps and four
lower-severity issues; all are fixed:

- **Reference-price evidence files were unreachable.** Uploads were stored and validated but
  had no read path — an admin could see an evidence *record* but never open the uploaded
  invoice/catalogue file itself, contradicting AGENTS.md §8 "the reviewing admin sees them
  all". Added `IListingMediaService.OpenReferencePriceEvidenceAsync` (owner/admin only, never
  public — unlike listing photography), `ListingMediaController.ShowEvidence`
  (`/listing-evidence/{id}`, `[Authorize]`), and a `DbSet<ListingReferencePriceEvidence>` on
  `IApplicationDbContext`/`ApplicationDbContext` to query it directly. Both the admin review
  screen and the merchant workspace now link to it.
- **A listing could publish while its merchant was no longer approved.** `ListingModerationService`
  re-checks `MerchantProfile.VerificationStatus == Approved` at the moment of `Approve`
  (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved") — a merchant
  suspended between submission and the admin's decision now fails the approval with a clear
  `Conflict` instead of silently publishing.
- **SoldOut listing photos were served to anonymous visitors.** `ListingMediaService.OpenImageAsync`
  treated `Live` and `SoldOut` as equally public; only `Live` is (docs/03-BUSINESS-RULES.md
  §2 "public users see only Live listings" — SoldOut is "addressable to authorized users",
  not anonymous traffic). Fixed to match `Listing.IsPubliclyVisible`.
- **A listing could be edited down to zero product photos while Live.** Removing an ordinary
  (non-defect) photo never re-ran the submission checks, so a merchant could delete every
  product photo from a published listing and it stayed `Live` with none — silently violating
  "at least one product photo". `Listing.RemoveMedia` now refuses to remove the last active
  `Product` photo outright (add a replacement first); `Defect`/`Packaging` photos are
  unaffected by this rule.
- **Category validation accepted the non-leaf sector root.** The reference-data list already
  excluded "Fashion Overstock" itself, but a crafted POST could still attach a listing to it.
  `MerchantListingService.ValidateDetailsAsync` now requires `ParentCategoryId != null`.
- **Archived listings still accepted stock adjustments.** The inventory screen already hid
  archived rows, but a direct POST to `InventoryService.AdjustStockAsync` worked regardless.
  Now rejected explicitly, mirroring `Listing.RequireNotArchived` on every other mutator.
- Lower-severity: `InventoryService.AdjustStockAsync` now catches a bare `DbUpdateException`
  (not only the concurrency subtype) so the `CK_ListingVariants_Quantities_NonNegative`
  backstop can never surface as an unhandled 500; `ListingReferencePriceEvidence.ReferenceUrl`
  is validated as an absolute `http`/`https` URL at the domain layer (it is rendered as a
  clickable link), closing a `javascript:`-scheme vector; `ListingModeration.AppendReason`
  compares exact `"; "`-separated segments instead of a whole-string substring check, so a
  new reason that happens to textually contain an old one can no longer be dropped (this
  branch is currently unreachable from any mutator — see the code comment — but the fix is
  correct defensively); removed three unused entity methods (`ListingVariant.Rename`,
  `ListingOption.Rename`, `ListingMedia.Describe`).
- New regression tests: `Faed.UnitTests.ListingTests.RemoveMedia_LastProductPhoto_Throws` (+2
  related), `AddReferencePriceEvidence_NonHttpUrl_Throws` (+1 valid-URL case); against real
  SQL Server, `Faed.IntegrationTests.ListingServiceTests.ReferencePriceEvidenceFile_IsRetrievableByOwnerAndAdmin_ButNotAnonymous`,
  `Approve_WhenTheMerchantIsNoLongerApproved_Fails_AndListingStaysPending`,
  `SoldOutListingImage_IsHiddenFromAnonymous_ButVisibleToOwnerAndAdmin`,
  `Create_WithTheNonLeafCategoryRoot_IsRejected`, `AdjustStock_OnAnArchivedListing_IsRejected`.
- The concurrency scope note from the original TASK-004 write-up stands: the literal "two
  B2C requests for the last unit" / "B2C vs accepted B2B deal" scenarios
  (AGENTS.md §7, docs/09-TEST-STRATEGY.md §3) require the B2C/B2B order flow and are
  correctly out of scope until TASK-006/007/008; what TASK-004 owns — the `ListingVariant`
  `rowversion` token itself stopping a lost update — is proven in
  `AdjustStock_TwoConcurrentContexts_OnlyTheFirstSaveSucceeds`.

### Post-review fixes, round 2

A second review pass (after round 1 above was already fixed and re-verified) found one
medium-severity gap, three low-severity issues and three nits; all are fixed:

- **A merchant could reverse an admin takedown.** `Listing.HideByAdmin` and the merchant's
  own `Hide` funnelled into the same `Hide()`, recording nothing about *who* hid the listing.
  `Restore()` then let the merchant republish any `Hidden` listing whose last review was an
  approval — exactly the state an admin's hide leaves it in, so a merchant could immediately
  undo an admin takedown with no signal to the admin that it happened
  (docs/16-PERMISSIONS-MATRIX.md "Moderate listing — Admin only"). Added a
  `Listing.HiddenByAdmin` flag (migration `20260901153402_AddListingHiddenByAdmin`), set by
  `HideByAdmin` and cleared only by the new `RestoreByAdmin`; the merchant's own `Restore`
  now throws when the flag is set. `IListingModerationService.RestoreAsync` (+
  `AdminActionType.ListingRestored`, audited) exposes admin restoration via a new
  Admin/ListingModeration/Restore action and a "Restore to the marketplace" button on the
  admin Details page; the merchant workspace shows "An administrator hid this listing.
  Contact Faed support…" instead of a Republish button whenever `HiddenByAdmin` is set.
- **`RefreshAvailability` could leave a Live listing advertising zero stock.** Two requests
  each depleting a *different* variant on the same listing each computed
  `AvailableUnits` from the `Variants` navigation loaded at the start of their own request, so
  neither saw the other's fresh depletion and neither flipped the listing to `SoldOut` (only
  the touched variant's own `rowversion` was checked; the untouched sibling's staleness never
  triggered a conflict). Self-healing on the next adjustment, as originally noted, but now
  mitigated: added `Listing.RefreshAvailability(int currentAvailableUnits, DateTime nowUtc)`,
  and `InventoryService.AdjustStockAsync` supplies a total computed from a fresh, untracked
  database query over the *other* active variants plus the just-applied value for the one it
  adjusted, instead of trusting the collection loaded at the top of the request. This closes
  the window from before the request started; true simultaneous commits are a separate,
  harder problem the reservation flow (a later task) will need to solve properly.
- **The multipart upload ceiling ignored the listings image cap.** `Program.cs` sized
  `FormOptions.MultipartBodyLengthLimit` from `MerchantVerification:MaxDocumentBytes` alone;
  TASK-004 added a second upload path (`Listings:MaxImageBytes`) with its own independent
  cap that the global ceiling never accounted for. Worked today only because the listings
  cap happens to be smaller. Now sized from `Math.Max` of both configured limits.
- **No-option single-variant listings were impossible through the UI.** The domain already
  permitted a variant with an empty option set (one plain SKU, no Size/Colour), but the
  workspace only ever showed "Add variant" once at least one option existed. A merchant
  selling one undifferentiated product had to invent an option to work around it. The
  "Add variant" form now also renders with zero options defined.
- Nits: the admin defect-photo warning now matches the discount reason's stable `Code`
  (`CosmeticDefect`) instead of its display `Name`, so a future catalog-management rename
  cannot silently disable it (`ListingDetailView` gained `DiscountReasonCodes` alongside
  `DiscountReasonNames`); the moderation queue's pending count uses a dedicated
  `GetPendingCountAsync` (`SELECT COUNT(*)`) instead of running the full queue query twice;
  the workspace's empty-variants-table row computes its `colspan` from whether the Actions
  column is actually rendered instead of a hard-coded `7`.
- New regression tests: `Faed.UnitTests.ListingTests.HideByAdmin_MarksTheListing_*`,
  `Restore_AfterTheMerchantsOwnHide_Succeeds`, `RestoreByAdmin_LiftsAnAdminTakedown_*`,
  `RestoreByAdmin_WhenNotHidden_Throws`, `RefreshAvailability_WithAnExplicitTotal_*`,
  `AddVariant_WithNoOptionsDefined_*`; against real SQL Server,
  `Faed.IntegrationTests.ListingServiceTests.MerchantRestore_AfterAnAdminTakedown_Fails_AndOnlyAdminRestoreWorks`,
  `AdjustStock_DepletingTwoVariantsAcrossSeparateRequests_ListingBecomesSoldOut` (its doc
  comment is explicit that this covers sequential-but-separate requests, not genuinely
  simultaneous commits — see the unit-level test for the deterministic proof of the
  underlying `RefreshAvailability` overload behaviour).
- Not implemented, deliberately: the review's other medium finding — that a rejected material
  edit to a `Live` listing overwrites the previously-approved content in place, with no
  retained approved/submitted version to revert to, and the re-reviewing admin sees only a
  comma-list of which fields changed rather than their before/after values — is a genuine
  partial gap against AGENTS.md §8 ("preserve merchant draft; submitted version; …
  approved/public version semantics"), but implementing full version snapshotting (or an
  automatic revert-to-last-approved on rejection) is a product-policy decision, not a bug fix,
  and the review's own text flagged it as needing team confirmation rather than a clear
  requirement. Left for the product owner to decide before it is built (AGENTS.md §12 "Do not
  silently resolve an unresolved product decision").

### Validation (TASK-004)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — **184 passed (137 unit, 47 integration), 0 failed, 0 skipped**
  on a workstation where SQL Server LocalDB (`MSSQLLocalDB`) is reachable, stable across
  repeated runs; the integration suite is `[SkippableFact]` by design and skips rather than
  fails with no reachable SQL Server (docs/09-TEST-STRATEGY.md §2).
- `dotnet ef database update` — `AddListingsAndInventory` and
  `AddListingHiddenByAdmin` both apply from the existing schema;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- Manual end-to-end verification against the real running app, real SQL Server LocalDB and
  real (local) file storage — including the admin hide → merchant-restore-blocked →
  admin-restore → merchant-can-self-hide-again round trip, with the audit log confirmed
  (`ListingApproved`, `ListingHidden`, `ListingRestored`).

## TASK-003 — Catalog Foundations

### Behaviour implemented

- `Category` — self-referencing hierarchy (`ParentCategoryId`, `Name`, `Slug`, `SortOrder`,
  `IsActive`). Globally unique `Slug`; `OnDelete(Restrict)` so a populated branch is never
  cascade-removed. The sector is a data row, not an enum — future sectors are added as data
  (AGENTS.md §3, docs/14-FUTURE-EXPANSION.md).
- `ConditionGrade` — DB reference table (not an enum, docs/19 "Enums vs tables"), unique
  `Code`. Seeded A–D only; there is no Grade E in the schema or seed.
- `DiscountReason` — DB reference table, unique `Code`, optional `Description`. Kept
  independent of `ConditionGrade` — no FK or navigation between them
  (docs/adr/0003-CONDITION-VS-DISCOUNT-REASON.md).
- `Brand` — optional, admin-controlled only (no merchant-authored brands), unique `Slug`.
  Table created; no brands seeded (docs/13-OPEN-QUESTIONS.md items 5–6).
- `CatalogDataSeeder` (`Data/Seed`) — runtime idempotent seeder invoked from `Program.cs`
  after `IdentityDataSeeder`, in every environment. Matches each row on its natural key
  (`Code` / `Slug`), case-insensitively to match SQL Server's default collation, and
  inserts only when missing — so re-running never duplicates and never overwrites a later
  admin edit, even one that changed a key's casing. Schema must already be applied — the
  app does not migrate on startup.
- `IApplicationDbContext` extended with the four catalog `DbSet`s for later use-case
  services (TASK-004+).

### Decisions recorded (docs/13-OPEN-QUESTIONS.md)

- Item 4 (taxonomy depth): seed root + three launch categories only; deeper taxonomy
  deferred. The lower-level tree in docs/12-SEED-DATA.md is dev/demo data for a later task.
- Items 5–6 (Brand): optional everywhere, admin-controlled only.
- docs/12-SEED-DATA.md updated to list all eight discount reasons (adds
  `Other Approved Reason`) and to note the deferred sub-categories.

### Not implemented (correctly deferred)

Catalog admin UI / CRUD (TASK-010), listings, options, variants, reference-price evidence
(TASK-004), brand seed data.

## TASK-001–003 review hardening

A review of the completed TASK-001/002/003 work produced the following fixes. No new
feature scope; TASK-004 remains not started.

1. **Verification uploads — active/executable content (fails closed).**
   `VerificationDocumentValidator.ValidatePayload` inspects the whole buffered upload rather
   than trusting a signature. Images are parsed structurally: JPEG marker by marker with
   `EOI` required as the last byte, PNG chunk by chunk with every CRC32 verified, `IEND`
   required last, and the raster inflated and matched against the `IHDR` geometry. No file
   may contain a `<script` / `<?php` marker or an embedded ZIP/RAR/7z/ELF/PE/second-PDF
   payload anywhere. The PDF active-content scan (`/JavaScript`, `/Launch`, `/EmbeddedFile`,
   `/RichMedia`) runs over the raw bytes, over a copy with PDF name hex-escapes resolved
   (`/Java#53cript` → `/JavaScript`), and over the decoded content of every Flate stream —
   the filter name is de-escaped first, so `/Flate#44ecode` is still recognised, and a
   `/DecodeParms` predictor is reversed so the bytes scanned are the bytes a reader consumes.
   A PDF is **rejected** (not trusted) when any part cannot be inspected: encryption, LZW,
   an external stream source, an unrecognised/indirect filter or an irreversible predictor, a
   Flate stream that will not inflate, or exhausting the 64 MB / 512-stream inflate budget.

   The structural rules are set at the point where full inspection is still possible, not
   tighter: multiple `%%EOF`/`startxref` markers (linearized and incrementally-saved PDFs),
   PDF 1.5+ cross-reference streams and PNG ancillary chunks are all accepted, because every
   byte is scanned regardless. An earlier, stricter revision refused all of them and thereby
   rejected 8 of 10 real PDFs and 6 of 7 real PNGs — friction with no safety gain. Measured
   on unmodified real-world files the validator now accepts 10/10 PDFs, 3/3 JPEGs and 7/7
   PNGs while every hostile fixture in `VerificationDocumentValidatorTests` is still refused.
   Recorded as `docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md`
   (docs/08-SECURITY-AND-PRIVACY.md §3 "no executable content"). Test fixtures use a real
   minimal PDF instead of the string `"%PDF-1.4 fake"`.
2. **Privileged service methods now recheck the actor.** `IUserRoleService.IsInRoleAsync`
   was added; `MerchantVerificationService` approve/reject/suspend/reinstate and
   `OpenVerificationDocumentAsync` return `Forbidden` unless the supplied user id actually
   holds the `Admin` role — the MVC `AdminOnly` policy is no longer the only gate
   (docs/08-SECURITY-AND-PRIVACY.md §2).
3. **Approval is atomic with the Merchant-role grant.** The status change, its audit row
   and the `AddToRoleAsync` call now run inside one `IApplicationDbContext.BeginTransactionAsync`
   transaction. A permanent role-sync failure rolls the whole decision back (returning a
   retryable `Conflict`) instead of leaving an approved profile that can never be
   re-approved (AGENTS.md §7).
4. **Merchant concurrency conflicts return `Conflict`, not HTTP 500.** `SaveDraftAsync` and
   `RemoveDocumentAsync` now catch `DbUpdateConcurrencyException` and surface a conflict
   result, matching `SubmitForReviewAsync` and the admin decision path.
5. **Catalog root lookup is collation-independent.** `CatalogDataSeeder` loads existing
   categories once and matches every slug — the `fashion-overstock` root included — with an
   ordinal-ignore-case comparer, instead of delegating the root lookup to the database
   where a case-sensitive server collation could miss it and allow a second root insert.
6. **Admin decision form no longer overflows narrow screens.** The reject form's inline
   `min-width: 18rem` was replaced with a `.faed-decision-row__grow` rule
   (`flex: 1 1 16rem; min-width: 0`) so the panel stays within a 320px viewport.
7. **Upload validation is field-level.** `VerificationController.UploadDocument` re-renders
   the verification page with the error bound to the document-type or file field
   (`asp-validation-for`) instead of a single `TempData` banner after a redirect
   (docs/07-UI-UX-SPEC.md, faed-responsive-accessibility "field-level errors").
8. **Integration tests fail (not skip) when SQL Server should have been there.** The suite
   still skips gracefully on a developer workstation that has no reachable SQL Server and was
   never told where to find one (unit tests run, `dotnet test` stays green with a lower count
   — the documented behaviour, docs/09 §2). It hard-fails when `CI=true` *or* when
   `Faed_TEST_CONNECTION` is set but unreachable, so neither a green pipeline nor a typo in
   that variable can silently omit the SQL Server proof. README documents pointing
   `Faed_TEST_CONNECTION` at a container when LocalDB is unavailable.

### Post-audit fixes

A second, evidence-based audit of the work above found three defects in it. All are fixed.

9. **The hosted integration tests were writing to the application database.**
   `DependencyInjection.AddPersistence` read `ConnectionStrings:DefaultConnection` eagerly
   during service registration, before `WebApplicationFactory`'s configuration override was
   merged — so the whole web test host used the application's `Faed` catalog while the
   disposable `Faed_WebTests` catalog was created, migrated and dropped unused. The
   connection string is now resolved from the built `IConfiguration` when the context options
   are created, the factory re-registers the context against the test catalog as defence in
   depth, and `TestHostDatabaseTargetTests` asserts the hosted context really targets
   `Faed_WebTests`. This violated docs/09-TEST-STRATEGY.md §2 ("never read the application's
   normal connection string") and made every web test order- and history-dependent.
10. **Two of the finding-4 regression tests did not pass.**
    `SaveDraft_WhenAnotherMerchantClaimsTheSlug_RetriesWithTheNextSlug` and
    `SaveDraft_WhenAnotherRequestCreatesTheUsersFirstApplication_ReturnsConflict` failed
    because leftover rows in the shared application database made the injected "racing" write
    collide with an earlier run instead of with the test's own insert. Fixing item 9 makes
    both deterministic; the service logic itself was already correct.
11. **A deactivated administrator still passed the service-level role recheck.**
    `UserRoleService.IsInRoleAsync` now requires `ApplicationUser.IsActive`, since disabling
    an account leaves its role rows in place (docs/08-SECURITY-AND-PRIVACY.md §2).

CI is now real rather than hypothetical: `.github/workflows/ci.yml` runs restore, build,
unit tests and integration tests against a SQL Server service container on push and pull
request (docs/09-TEST-STRATEGY.md §6).

### Responsive / accessibility review (findings 6–7)

- Responsive: fixed one horizontal-overflow source (the admin decision form on ~320px).
  No other overflow, tap-target or small-width spacing issues found in the merchant/admin
  verification views.
- Accessibility: upload errors are now field-level with `.faed-field__error` messages and a
  model-only validation summary; labels, semantic headings, focus-visible outlines and
  non-color status text were already in place and unchanged.

## TASK-002 — Merchant Verification

### Behaviour implemented

- `MerchantProfile` aggregate (1:1 with the Identity user) with the verification state
  machine `Draft → PendingReview → Approved / Rejected / Suspended` and
  `Suspended → Approved` (reinstate). A user can never self-assign `Approved`; every
  decision is a guarded domain transition that records the reviewing admin, timestamp and
  reason.
- `MerchantVerificationDocument` — private evidence files. Only a storage object key +
  metadata are stored; there is no public URL. Removing a document soft-deactivates it so
  history is retained (safe reversible default for open question item 3).
- `AdminActionLog` — append-only audit of merchant approve/reject/suspend/reinstate and
  every verification-document access.
- `IFileStorage` abstraction + `LocalFileStorage` development implementation: writes to a
  private directory outside `wwwroot` (`{ContentRoot}/App_Data/private-storage` by
  default), generates the object key server-side, validates keys on read against path
  traversal.
- Service layer: `IMerchantVerificationService` with use-case methods, a `Result`
  pattern (Validation / NotFound / Forbidden / Conflict), server-side upload validation
  (size, content type, extension), and `IApplicationDbContext` as the persistence seam
  (not a generic repository).
- Authorization policies `ApprovedMerchant` (DB-checked verification state, not a role)
  and `AdminOnly`, plus policy name constants (`Authorization/FaedPolicies`) and role name
  constants (`Models/Identity/FaedRoles`).
- Merchant self-service UI (`/Merchant/Verification`) and admin queue/detail/decision UI
  (`/Admin/MerchantVerification`) using a new Faed design-token CSS layer.
- The `Merchant` role is granted on approval for nav/UI convenience; selling capability
  is always governed by verification state.

### Open questions handled with reversible defaults

- Accepted Jordanian document types (item 1): flexible `MerchantVerificationDocumentType`
  enum — `CommercialRegistration`, `TaxRegistration`, `Other`. No personal/national
  identity document is offered (docs/08-SECURITY-AND-PRIVACY.md §14).
- Email confirmation before application (item 2): none added beyond the existing
  `RequireConfirmedAccount`; any authenticated user may start an application.
- Rejected-document retention (item 3): documents are retained (soft-deactivated), never
  auto-deleted.

### Post-review hardening (TASK-002 code review)

- Uploaded document contents are now validated by byte signature (real `%PDF-`, JPEG,
  PNG magic bytes), the content type must pair with the file extension, and admin
  downloads are served as attachments (`Content-Disposition: attachment`, `nosniff`),
  never inline.
- `MerchantProfile` carries a SQL Server `rowversion` concurrency token; competing admin
  decisions surface as a `Conflict` result instead of a silent last-write-wins.
- Rejection/suspension reason length is validated (≤ `MerchantProfile.MaxDecisionReasonLength`
  = 1000) before persistence in both the domain and the application service.
- `LocalFileStorage` is registered only outside Production (Production resolves a
  fail-fast `IFileStorage`); the configured storage root is rejected if it resolves
  inside the web root.
- Undefined document-type enum values are rejected (`Enum.IsDefined` in the validator,
  `[EnumDataType]` on the input model).
- Verification timestamps render in Asia/Amman via `AmmanTime`, not raw UTC.
- The multipart upload ceiling tracks `MerchantVerification:MaxDocumentBytes`
  (`FormOptions.MultipartBodyLengthLimit`) and the merchant view shows the configured
  limit instead of a hard-coded "10 MB".

### Not implemented (correctly deferred)

Categories, listings, variants, orders, B2B, storefront pages, email provider, cloud
storage provider. `MerchantLocation` / `MerchantDeliveryZone` are in the domain doc but
belong to fulfilment phases and were not modelled.

## Solution structure

```text
Faed.slnx
src/
└── Faed.Web/
    ├── Models/
    │   ├── Entities/       # MerchantProfile, MerchantVerificationDocument, AdminActionLog,
    │   │                   # Category, ConditionGrade, DiscountReason, Brand,
    │   │                   # Listing (+ Option/OptionValue/Variant/VariantOptionValue/Media/
    │   │                   # DiscountReason join/ReferencePriceEvidence/Moderation),
    │   │                   # InventoryAdjustment
    │   ├── Enums/          # MerchantVerificationStatus, *DocumentType, AdminActionType,
    │   │                   # ListingStatus, ListingMediaType, ListingModerationStatus,
    │   │                   # ReferencePriceEvidenceType, InventoryAdjustmentType
    │   ├── Identity/       # ApplicationUser, FaedRoles
    │   └── DomainException.cs
    ├── Data/
    │   ├── ApplicationDbContext.cs   # + IApplicationDbContext, shared with Identity
    │   ├── Configurations/           # IEntityTypeConfiguration<T> for each entity
    │   ├── Migrations/               # EF Core migrations
    │   └── Seed/           # IdentityDataSeeder, CatalogDataSeeder (both idempotent)
    ├── Services/
    │   ├── Abstractions/   # IApplicationDbContext, IFileStorage, IUserRoleService, IClock
    │   ├── Common/          # Result.cs, Slug.cs
    │   ├── Merchants/      # IMerchantVerificationService + implementation, models, validator, slug
    │   ├── Listings/        # IMerchantListingService, IInventoryService,
    │   │                    # IListingModerationService, IListingMediaService + implementations,
    │   │                    # ListingOptions, ListingImageValidator, ListingQueries, models
    │   ├── Marketplace/     # IPublicMarketplaceService + implementation (anonymous-safe reads)
    │   ├── Storage/        # LocalFileStorage
    │   ├── UserRoleService.cs
    │   └── SystemClock.cs
    ├── Authorization/      # FaedPolicies, ApprovedMerchant handler, ClaimsPrincipal ext.
    ├── Controllers/         # Home, Shop, Listing, Store (public marketplace),
    │                        # ListingMediaController (public/private image serving)
    ├── Areas/{Admin,Merchant,Identity}/
    │   ├── Admin/Controllers/       # MerchantVerificationController, ListingModerationController
    │   └── Merchant/Controllers/    # VerificationController, ListingsController, InventoryController
    ├── ViewModels/         # ErrorViewModel, Marketplace/ (Shop/Store/Listing page + filter
    │                       # models; area-local view models under each Area/ViewModels)
    ├── Rendering/          # AmmanTime, MerchantStatusDisplay, ListingStatusDisplay (view-only helpers)
    ├── DependencyInjection.cs   # AddFaedPlatform composition helper
    └── Program.cs
tests/
├── Faed.UnitTests/         # MerchantProfile / Listing / ListingVariant state machines,
│                           # upload validator, slug, foundation
└── Faed.IntegrationTests/  # SQL Server persistence; merchant-verification and listing
                            # services + MVC authorization (WebApplicationFactory + test
                            # auth scheme); listing/variant/moderation/inventory concurrency
```

Both test projects reference `src/Faed.Web` directly. There are no other production
projects and no project-reference layering.

## Migrations

- `20260831174908_InitialIdentity` (`src/Faed.Web/Data/Migrations`) — ASP.NET Core Identity
  schema for `ApplicationUser` (adds `CreatedAtUtc` default `SYSUTCDATETIME()`, `IsActive`
  default `true`). Replaces the Visual Studio template's `00000000000000_CreateIdentitySchema`.
- `20260831205644_AddCatalog` (`src/Faed.Web/Data/Migrations`) — `Categories` (unique
  `Slug`, self-referencing FK `OnDelete(Restrict)`, index on `(ParentCategoryId, SortOrder)`),
  `ConditionGrades` (unique `Code`), `DiscountReasons` (unique `Code`), `Brands` (unique
  `Slug`). All Guid keys `ValueGeneratedNever`. `has-pending-model-changes` reports clean
  after build.
- `AddMerchantVerification` (`src/Faed.Web/Data/Migrations`) — `MerchantProfiles` (unique
  `UserId`, unique `PublicSlug`, indexed `VerificationStatus`; enum stored as text;
  `rowversion` concurrency token; restricted delete from `AspNetUsers`),
  `MerchantVerificationDocuments` (cascade from profile), `AdminActionLogs`. All Guid keys
  are `ValueGeneratedNever` (assigned by the entity constructor). Migration IDs are
  unchanged by the restructure; `dotnet ef migrations has-pending-model-changes` reports
  clean.
- `20260901141629_AddListingsAndInventory` (`src/Faed.Web/Data/Migrations`) — `Listings`
  (unique `Slug`, restricted FKs to `MerchantProfiles`/`Categories`/`ConditionGrades`/`Brands`,
  `rowversion`), `ListingOptions`/`ListingOptionValues` (unique per-listing name / per-option
  value), `ListingVariants` (`rowversion` present from this first migration; check constraint
  `CK_ListingVariants_Quantities_NonNegative`; unique `(ListingId, Sku)` and
  `(ListingId, OptionCombinationKey)`), `ListingVariantOptionValues`, `ListingMedia`,
  `ListingDiscountReasons` (restricted FK to `DiscountReasons`), `ListingReferencePriceEvidence`,
  `ListingModerations`, `InventoryAdjustments` (restricted FK to `ListingVariants` — the audit
  trail outlives the variant edit that produced it). `has-pending-model-changes` reports
  clean after build.
- `20260901153402_AddListingHiddenByAdmin` (`src/Faed.Web/Data/Migrations`) — adds
  `Listings.HiddenByAdmin` (`bit NOT NULL DEFAULT 0`), distinguishing an admin takedown from
  the merchant's own hide so only an admin can lift the former. `has-pending-model-changes`
  reports clean after build.

## Persistence

- One application `DbContext` (`ApplicationDbContext`) in `src/Faed.Web/Data`, shared
  with Identity and exposed to services through `IApplicationDbContext`.
- SQL Server; local development uses LocalDB database `Faed` (non-secret connection
  string in `appsettings.json`).
- EF Core `rowversion` concurrency is introduced with the inventory model in a later task.

## Private file storage

- `IFileStorage` (`Services/Abstractions`) with `LocalFileStorage` (`Services/Storage`) for development.
- Root defaults to `{ContentRoot}/App_Data/private-storage` (gitignored) or
  `FileStorage:LocalRootPath` when set; startup fails if the resolved root is inside the
  web root. Object keys are server-generated and validated against traversal on read.
- `LocalFileStorage` is registered only outside Production. In Production `IFileStorage`
  resolves to a fail-fast stub until a cloud object store is bound to the interface
  (docs/06-ARCHITECTURE.md §8).

## Identity

- Individual Accounts (ASP.NET Core Identity) preserved from the Visual Studio baseline.
- `AddDefaultIdentity<ApplicationUser>().AddRoles<IdentityRole>()`.
- Roles `Buyer`, `Merchant`, `Admin` seeded idempotently at startup. The `Merchant` role
  is additionally granted to a user on verification approval (idempotent).
- Optional development admin seeding: only when `Faed:AdminSeed:Email` +
  `Faed:AdminSeed:Password` are supplied (user secrets / env) and the environment is not
  Production. No password is stored in source control. Seeds for the other roles belong to
  the phase that first needs one (AGENTS.md §12).
- Merchant verification is a domain state, not an Identity role. Policies: `AdminOnly`
  (role check), `ApprovedMerchant` (per-request DB check of verification status).

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

## Current validation (TASK-002 + single-project architecture audit)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — 52 passed (30 unit, 22 integration), 0 failed. Integration
  tests run against allow-listed LocalDB test databases (`Faed_IntegrationTests`,
  `Faed_WebTests`) which they create and drop; they skip when no SQL Server is reachable,
  override any configured catalog, and never touch the app connection string. Coverage
  includes Home/Login/Register availability, seeded roles, destructive database guards,
  byte-signature rejection of renamed
  uploads, competing-admin concurrency conflict, over-length reason rejection, and a
  non-admin POST to every decision action (Approve/Reject/Suspend/Reinstate) returning
  403 through the real MVC pipeline with no state or audit change.
- `dotnet ef database update` — both migrations apply from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs (Development); Home renders; anonymous `/Merchant/Verification` and
  `/Admin/MerchantVerification` redirect to login; role seeding idempotent; admin seed
  correctly skipped when unconfigured.

### Exit-criteria coverage (tasks/TASK-002)

| Exit criterion | Covered by |
|---|---|
| Buyer cannot access merchant-only workflow | `AdminQueue_Buyer_IsForbidden`, `SellingProbe_*` (pending → 403) |
| Pending merchant cannot submit listings | `ApprovedMerchant` policy DB-checks status; `IsApprovedMerchant` false for pending; `SellingProbe_PendingMerchant_IsForbidden` |
| Admin can approve/reject | `Approve_MovesToApproved_*`, `Reject_RequiresReason_AndRecordsIt`, `AdminQueue_Admin_IsAllowed` |
| Non-admin cannot approve/reject/suspend/reinstate | `AdminDecisions_PostedByBuyer_AreForbidden_AndChangeNoStateOrAudit`, `Decisions_WithoutAnAdminUserId_AreForbidden_AndChangeNothing` |
| Private document URL is not public | no route exposes the storage key; `VerificationDocument_Anonymous_IsChallenged` |
| Unauthorized document request fails | `VerificationDocument_Buyer_IsForbidden` |
| Audit entry is persisted | `Approve_*_WritesAudit`, `OpenVerificationDocument_*_AuditsAccess` |
| Relevant tests pass | full suite green |

## Current validation (TASK-003)

- `dotnet build Faed.slnx` — succeeds, 0 warnings, 0 errors.
- `dotnet test Faed.slnx` — after the TASK-001–003 review hardening and the post-audit
  fixes: **135 passed (100 unit, 35 integration), 0 failed, 0 skipped** on a workstation
  where SQL Server LocalDB (`MSSQLLocalDB`) is reachable. `dotnet build Faed.slnx` succeeds
  with 0 warnings and 0 errors. The integration tests are `[SkippableFact]` by design
  (docs/09-TEST-STRATEGY.md §2): on a machine with **no** reachable SQL Server they skip
  rather than fail, so a green run there reports fewer executed tests — the SQL Server exit
  criteria for TASK-001/002/003 are only proven on a run where the integration suite
  actually executes. New coverage: catalog entity invariants; EF model shape
  (condition/discount-reason independence, self-referencing `Category`, unique
  slug/code indexes); startup seeds A–D + eight reasons + launch taxonomy; no Grade E;
  seeder is idempotent on re-run and when an existing slug differs only by casing; a second
  root `Category` persists to SQL Server with no schema change; DB-level unique-slug
  enforcement for `Category` and `Brand`. Review hardening added: `ValidatePayload` rejects
  `%%EOF`-less PDFs, JavaScript/Launch/EmbeddedFile/RichMedia PDFs (including markers hidden
  behind PDF name hex-escapes or inside a Flate stream, and filter names hex-escaped to
  `/Flate#44ecode`), encrypted / LZW / unrecognised-filter / non-inflatable-stream PDFs,
  and script-carrying image polyglots, while still accepting a normal compressed PDF and an
  image-only-stream PDF; a non-admin actor calling the verification service directly is
  `Forbidden` and changes no state or audit.
  Post-audit coverage: the hosted `ApplicationDbContext` targets the disposable
  `Faed_WebTests` catalog and never the application database; the slug and first-application
  unique-index races resolve to the right result while an unrelated `DbUpdateException` still
  propagates; `/JavaScript` visible only after a `/DecodeParms` predictor is reversed is
  rejected, as is JavaScript introduced by an appended PDF revision, a script marker hidden
  in a compressed PNG `zTXt`, an archive in a compressed `iTXt`, a `zTXt` that will not
  inflate, an unknown *critical* PNG chunk, and `/DecodeParms` carrying an unknown key —
  while an incrementally-saved PDF, a cross-reference-stream PDF, a predictor-encoded stream,
  clean content behind a hex-escaped `/Flate#44ecode` filter name, and a PNG carrying `tEXt`,
  `iCCP` and vendor provenance chunks are all accepted.
- `dotnet ef database update` — `AddCatalog` applies from the existing schema;
  `dotnet ef migrations has-pending-model-changes` reports no drift (after build).
- App runs (Development); Home and `/Identity/Account/Login` return 200; `CatalogDataSeeder`
  populates `ConditionGrades` (4), `DiscountReasons` (8) and `Categories` (4) and is a no-op
  on subsequent starts.

### Exit-criteria coverage (tasks/TASK-003)

| Exit criterion | Covered by |
|---|---|
| Migration applies from an empty database | `SqlServerPersistenceTests` (all migrations), `dotnet ef database update` |
| Seed runs repeatedly without duplication | `CatalogSeedTests.Seed_RunAgain_AddsNoDuplicateRows`, `Seed_IsIdempotent_WhenAnExistingSlugDiffersOnlyByCasing` |
| A second root category can be added by data alone | `CatalogSeedTests.SecondRootCategory_CanBeAddedByDataAlone_WithNoSchemaChange` |
| Condition and discount reason separate; Grade E absent | `CatalogModelTests.ConditionGrade_And_DiscountReason_AreIndependent`, `CatalogSeedTests.ConditionGrades_DoNotIncludeGradeE` |
| No catalog/condition/reason values hard-coded in code or views | reference tables + `CatalogDataSeeder`; no views added |
| Catalog unit/integration tests pass | full suite green |
| `dotnet build` succeeds; `PROJECT_STATUS.md` updated | this document |

## Validation (TASK-001)

- `dotnet build Faed.slnx` — succeeds, 0 warnings.
- `dotnet test Faed.slnx` — 3 passed (2 unit, 1 SQL Server integration against LocalDB
  `Faed_IntegrationTests`, which the test creates and drops via its own
  `Faed_TEST_CONNECTION` variable — never the app connection string).
- `dotnet ef database update` — `InitialIdentity` applies from an empty database;
  `dotnet ef migrations has-pending-model-changes` reports no drift.
- App runs; Home renders; Register/Login reachable; registration creates a user with
  `CreatedAtUtc`/`IsActive` populated; role seeding is idempotent across restarts.
