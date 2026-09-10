# Audit — Phase 14 (Final audit & defence readiness)

Branch `faed-core`. Audit performed against `CLAUDE.md`, `docs/CORE.md`, `docs/PHASE-PLAN.md`
(Phase 14 as amended, working-tree copy).

**Status: audit only. No code has been changed except where noted below under
"Fixed immediately".** The repository is unchanged apart from the working-tree edit to
`docs/PHASE-PLAN.md` (the Phase-14 addition) and this new file.

---

## 0. Executive summary

| Step | Result |
|---|---|
| 1 — Secrets / public-repo safety | **No secret, credential or uploaded document is tracked.** Demo emails/phones are `.local` / `.example`. Two *non-secret* concerns: real hosting infrastructure in `Properties/PublishProfiles/`, and the demo password is not documented in `README.md` (only in `docs/DEMO-SCRIPT.md`). |
| 2 — Clean build | **`dotnet build Faed.slnx -warnaserror` passes: 0 warnings, 0 errors.** No fix required. Note: `Directory.Build.props` sets `TreatWarningsAsErrors=false`, so a plain `dotnet build` does *not* enforce it. |
| 3 — Migration / schema state | **One migration + snapshot.** `migrations list` agrees. `has-pending-model-changes` = none. A scaffolded `__probe` migration came out empty (Up/Down both empty) and was deleted. App does not migrate on startup (`Program.cs`). |
| 4 — Dead vocabulary / dead code | Two real leaks in `_Layout.cshtml` (auth-page sidebar): **"Retail + wholesale routes"** and **"discount reason"** shown to logged-out visitors. One dead script file. One stale admin-copy reference to deleted `Brand`. |
| 5 — Files that should not exist | **~11 tracked paths proposed for deletion** (see §Deletion list): `plan.md`, `design-previews/`, `Claude outputs/`, `Properties/PublishProfiles/`, `Properties/serviceDependencies*.json`, `wwwroot/js/listing-detail.js`. Plus a keep-list conflict to resolve (see BLOCKER-1). |
| 6 — Tests | **46 tests, all green, none vacuous.** The five plan themes are all genuinely asserted. The 12/48/72-hour sweeps and the response-rate threshold *are* covered (the plan's Step 6 wording implies they are not). "Actually collected" is **not a test gap — the feature does not exist** (HIGH-2). |
| 7 — Invariants | **All eight hold**, each with an enforcing file (§7). Both carried-across rules verified. Nothing from the "Do not add" list crept in. |
| 8 — Documentation drift | **`docs/04-DOMAIN-MODEL.md` is completely stale** (still B2B/disputes/fashion). `DEPLOYMENT.md` is stale (B2B config sections, "no email sender", "storage stub throws" — all now false). `README.md` and the entity count need updates. Phases 11–13 not yet back-filled into `PHASE-PLAN.md`. |
| 9 — Defence rehearsal | **Clone → build → DB-from-empty → demo-seed → run → unauthenticated HTTP smoke pass: all clean.** Fresh clone builds in ~40 s (0/0); `Demo data set seeded.` with no error; `/`, `/shop`, listing detail, storefronts, images, 404 page all 200/404 with no exception; auth gates redirect correctly; "New seller" vs "67% response" split is correct. **Not done:** the authenticated three-role click-through with per-step timings — needs a human run (curl + antiforgery is impractical), then a re-run after the Step 5 deletions. One demo product image is 1.97 MB (unoptimised). |

**Blocker before any deletion work:** BLOCKER-1 (keep-list omits three docs that `CLAUDE.md`
and `README.md` actively depend on — needs your decision). The deletion commit must also be
followed by a repeat of the Step 9 rehearsal.

---

## Fixed immediately (per Phase 14 working rule 1)

**Nothing.** Step 1 found no secret to remove. Step 2's `-warnaserror` build was already
clean, so no compile fix was needed. The only tree mutation during the audit was the
scaffold-and-delete of the empty `__probe` migration for Step 3; the working tree is back to
where it started.

---

## Findings by severity

### BLOCKER-1 — The Step 5 keep-list omits three documents the codebase depends on

*Files:* `docs/DESIGN-BRIEF.md`, `docs/DEMO-SCRIPT.md`, `docs/CREDITS.md`

The Phase 14 Step 5 keep-list table lists ten markdown files and says "Everything else in
`.md` goes." Three omitted files are not stale scratch notes — they are referenced and
required:

- **`docs/DESIGN-BRIEF.md`** — `CLAUDE.md` ("UI principles") says *"`docs/DESIGN-BRIEF.md` is
  the authority on colour, type and the visual system from Phase 11 onward — tokens, the
  ink/rust colour rule, and the condition-stamp treatment are locked there. Read it before
  touching any Razor view or `faed.css`."* It is cited by `faed.css`, `_Layout.cshtml`,
  `ConditionStamp.cs`, `DemoDataSeeder.cs` and `CREDITS.md`. It holds locked decisions (the
  `--faed-*` token values, §5.3 colour semantics, §5.5 New-Seller rule, §8 photography rules)
  that no keep-list document restates in full.
- **`docs/DEMO-SCRIPT.md`** — `README.md` points to it to run the defence demo; Phase 14
  Step 9 itself says *"Confirm the Phase 13 demo script still matches the actual screens."*
  Deleting it contradicts the step that references it.
- **`docs/CREDITS.md`** — attribution for the CC BY / CC BY-SA product photos in the demo
  seed. Several licences (`CC BY-SA 3.0/4.0`, `CC BY 2.0`) **legally require** attribution;
  the brief keeps it out of the UI, so this file is where it lives. `README.md` and
  `DemoDataSeeder.cs` reference it.

*Proposed fix:* add all three to the keep-list in `PHASE-PLAN.md` Step 5 (and Phase 14's
acceptance bullet). Do **not** delete them. If you want the count lower, `DEMO-SCRIPT.md`
could be folded into a `README.md` section and `CREDITS.md` into a `DEMO-SCRIPT.md` section,
but each still has to exist somewhere. Recommendation: keep all three as-is; this is a
spec gap, not a cleanup opportunity.

---

### HIGH-1 — Two-sided / B2B copy still shown to logged-out visitors

*File:* `src/Faed.Web/Views/Shared/_Layout.cshtml:226-235` (the `isAuthPage` context sidebar,
rendered on `/Identity/Account/Login`, `/Register`, etc.)

```
<strong>Clear listing context</strong>
<small>Condition and discount reason stay visible before you buy.</small>
...
<strong>Retail + wholesale routes</strong>
<small>Use the buying route that each listing actually supports.</small>
```

*Why it matters:* the panel logs in during the demo. "Retail + wholesale routes" is
vocabulary from the removed two-sided version — Phase 2 deleted B2B entirely and Step 4's
grep list includes `wholesale`. "discount reason" is internal vocabulary the merchant and
buyer are never supposed to see (`CLAUDE.md` UI principles; `CORE.md` §3.3).

*Proposed fix:* rewrite the two list items. Drop the wholesale one (replace with something
true, e.g. *"Reserve online, pay cash at pickup"* or the verified-condition disclosure).
Reword the other to *"The condition and the reason for the discount stay visible before you
reserve."*

---

### HIGH-2 — Phase 9 feature "Actually collected" (NoShow → Completed) is absent

*Files:* `src/Faed.Web/Models/Entities/Order.cs` (`Complete()` accepts only `ReadyForPickup`
/ `OutForDelivery`; `MarkNoShow()` has no inverse), `Services/Ordering/OrderService.cs`
(methods: `ConfirmAsync`, `MarkReadyForPickupAsync`, `MarkOutForDeliveryAsync`,
`CompleteAsync`, `MarkNoShowAsync`, `CancelAsMerchantAsync` — no recovery method),
`Areas/Merchant/Controllers/OrdersController.cs` (6 POST actions, none for this),
`Areas/Merchant/Views/Orders/Details.cshtml` (no such button).

Phase 9 "Do" list: *"Add a merchant action **'Actually collected'** on a `NoShow` order,
moving it to `Completed`. The sale usually did happen and the merchant simply never opened
the dashboard; this repairs the record and unlocks the review without the platform inventing
a transaction."* This does not exist anywhere in the code. Phase 14 Step 6 lists it as a
*test* gap, which presumes the feature is present.

*Why it matters:* it is a stated Phase-9 acceptance item, and without it a `NoShow` order is
a dead end — the review it should unlock can never be left. Whether the panel notices is
another question.

*Proposed fix:* this is a genuine spec-vs-code gap, and Phase 14 is explicitly "no new
features". Your call between: (a) treat it as a deliberate Phase-14 exception and implement
the single transition + button + one test; or (b) record in `PHASE-PLAN.md` §8 back-fill
that it was cut from Phase 9 and why. Do not let it pass silently.

---

### HIGH-3 — `docs/04-DOMAIN-MODEL.md` is entirely stale

*File:* `docs/04-DOMAIN-MODEL.md`

Still documents `MerchantDeliveryZone`, `Brand`, `InventoryAdjustment`, all five B2B
entities, `Dispute`/`DisputeEvidence`, `AdminActionLog`, the fashion category tree
("Fashion Overstock → Clothing/Shoes/Bags"), `Listing.AllowB2C/AllowB2B/WholesaleMinQuantity/
WarrantyText/BrandId`, `Order.DeliveryZoneId/DeliveryFeeSnapshot`, and §13 "Explicitly out of
scope: … subscriptions". None of that is true after Phases 2–6.

*Why it matters:* it is on the keep-list, and Step 8 requires it regenerated. As-is it
directly contradicts the schema in front of anyone who reads it.

*Proposed fix:* regenerate from the 16 `DbSet`s in `ApplicationDbContext` plus
`ApplicationUser` and the owned option/variant types — every entity, key fields,
relationships, delete behaviour, and a truthful count (see MEDIUM-4).

---

### MEDIUM-1 — `DEPLOYMENT.md` describes infrastructure that no longer matches the code

*File:* `DEPLOYMENT.md` (repo root; keep-list calls it `docs/DEPLOYMENT.md`)

Stale statements:
- §2 config table lists `B2BNegotiation`, `B2BDeal`, `Trust` (dispute evidence) sections —
  all deleted.
- §3.2: *"No `IEmailSender` is registered, so ASP.NET Core Identity uses its no-op sender."*
  — false. `Program.cs:74` registers `BrevoEmailSender` via `AddHttpClient<IEmailSender,…>`.
- §1 table + §3.1: non-Development `IFileStorage` is *"a stub that throws on first use"* —
  false. `DependencyInjection.cs:199` registers the real `R2FileStorage` (backed by the
  `AWSSDK.S3` package).
- §3.3 names `B2BOfferExpiryService` / `B2BDealExpiryService` as hosted services — deleted.

*Proposed fix:* update §2/§3 to the current three background services
(`OrderDeadlineService`, `SubscriptionExpiryService`) and the real Brevo + R2
implementations; drop the B2B/Trust rows. Consider moving the file to `docs/` to match the
keep-list path (code comments reference it by basename, so they still resolve).

---

### MEDIUM-2 — `README.md` is behind the code

*File:* `README.md`

- "13 phases from branch setup to demo readiness" / "Phases 11–12 … are in place" — Phase 13
  is done and Phase 14 is running.
- Documentation table omits `docs/COMMITS-AND-COMMENTS.md`, `docs/04-DOMAIN-MODEL.md`,
  `docs/B2B-DESIGN.md`, `DEPLOYMENT.md`, `docs/DEMO-SCRIPT.md`, `docs/CREDITS.md`.
- Demo section says "a password is supplied out of band" but does not list the demo accounts
  or the shared password. Phase 14 Step 1 requires seeded demo credentials to be
  *"obviously fake and documented in `README.md`"* — right now they are only in
  `docs/DEMO-SCRIPT.md`.

*Proposed fix:* refresh the status/phase text, complete the doc table, and add a short
"Demo accounts" block (or link `DEMO-SCRIPT.md` explicitly and state the password is
`Demo!Pass123`, Development-only).

---

### MEDIUM-3 — Stale reference to the deleted `Brand` entity in admin copy

*File:* `src/Faed.Web/Areas/Admin/Views/Catalog/Index.cshtml:14`

> "Manage the taxonomy, condition grades, discount reasons and **controlled brands**.
> Reference rows are …"

`Brand` was deleted in Phase 3; the Catalog screen has no brands section. Admin-facing, so
low visibility, but it is dead vocabulary the Step 4 grep is meant to catch.

*Proposed fix:* drop "and controlled brands" from the sentence.

---

### MEDIUM-4 — The documented entity count ("17") does not match a countable artefact

*Files:* `docs/CORE.md` §6 (title "Entities — seventeen", then lists 18 bullet points),
`docs/PHASE-PLAN.md` "Expected result" table (`Entities | 30 | 17`), `docs/CORE.md` §10.

Countable reality: 16 `DbSet<>` properties in `ApplicationDbContext` + `ApplicationUser`
(Identity) = **17 aggregate roots**; plus four owned/join types configured but not exposed as
`DbSet`s (`ListingDiscountReason`, `ListingOption`, `ListingOptionValue`,
`ListingVariantOptionValue`) = **21 EF entity types** / **20 files** in `Models/Entities/`.
CORE §6's own bullet list already contains 18 names, one more than its title.

*Proposed fix:* pick one definition ("17 aggregate roots, 21 mapped types"), state it in
`04-DOMAIN-MODEL.md`, and make CORE §6 / §10 and the PHASE-PLAN table agree.

---

### MEDIUM-5 — Dead script file `wwwroot/js/listing-detail.js`

*File:* `src/Faed.Web/wwwroot/js/listing-detail.js` (the only file in `wwwroot/js/`)

Not referenced by any view or `_Layout`. `Views/Listing/Details.cshtml:171` ships its own
inline `@section Scripts` gallery script using different selectors (`#fx-hero`,
`.fx-gallery__thumb`, `data-full-src`) than the file's (`[data-gallery-hero]`,
`[data-gallery-thumb]`). The Phase 11/12 rebuild replaced the markup and inlined the JS,
orphaning this file.

*Proposed fix:* delete the file and the now-empty `wwwroot/js/` directory (see Deletion
list). Update the stale comment at
`src/Faed.Web/Services/Marketplace/PublicMarketplaceModels.cs:167` which still says the
client-side logic lives "in `listing-detail.js`".

---

### MEDIUM-6 — Phases 11–13 not yet recorded in `PHASE-PLAN.md`

*File:* `docs/PHASE-PLAN.md` (§"Phases 11–13 — Visual rebuild (complete)" is a 3-row summary
table only)

Step 8 requires them back-filled as completed sections in Do / Acceptance / Verify format,
written from what the code does. Also record Phase 14's own outcome once the fixes land.

---

### LOW-1 — Real hosting infrastructure committed for a project with "no live deployment"

*Files:* `src/Faed.Web/Properties/PublishProfiles/abdalqaderf-001-site1 - FTP.pubxml`,
`… - Web Deploy.pubxml`

No passwords (`<_SavePWD>false</_SavePWD>`; the `.pubxml.user` that would hold the encrypted
password is covered by `.gitignore`'s `*.user`). But they expose a real hosting username
(`abdalqaderf-001`), real endpoints (`win6046.site4now.net:21`, `…:8172/msdeploy.axd`,
`abdalqaderf-001-site1.ltempurl.com`) in a public repo. `README.md` says there is no live
deployment.

*Proposed fix:* delete `Properties/PublishProfiles/` (Deletion list). Nothing references it.

---

### LOW-2 — `Directory.Build.props` disables warnings-as-errors globally

*File:* `Directory.Build.props:6` — `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`

The Phase 14 gate (`dotnet build -warnaserror`) passes only because the flag is passed
explicitly. A normal `dotnet build` — and CI that forgets the flag — will not enforce it.
The tree is currently 0-warning, so flipping it to `true` is free.

*Proposed fix:* set it to `true` in `Faed.Web` (keep the test project exempt if xUnit
analyzers are noisy), or leave as-is and document that `-warnaserror` is mandatory in the
release checklist (`DEPLOYMENT.md` §5 already says "0 warnings").

---

### LOW-3 — `CanPlaceB2COrder` / "B2C" naming outlives the B2B/B2C distinction

*Files:* `src/Faed.Web/Authorization/FaedPolicies.cs`, `Program.cs:123`,
`Services/Ordering/*` ("B2C ordering" comments)

Not wrong, but "B2C" only means something in contrast to "B2B", which is gone. Cosmetic.

*Proposed fix (optional):* rename to `CanPlaceOrder` and drop "B2C" from comments, or leave
it — it is internal and harmless.

---

### LOW-4 — `docs/COMMITS-AND-COMMENTS.md` teaches an attribution line that is now disallowed

*File:* `docs/COMMITS-AND-COMMENTS.md` (every example ends `Co-Authored-By: Claude Opus 5
<noreply@anthropic.com>`)

Current session guidance is to add no attribution lines to commits. This is a keep-list
file, so flag rather than rewrite: decide whether the doc's convention still stands.

---

### LOW-5 — `site.css` contains Bootstrap-template leftovers

*File:* `src/Faed.Web/wwwroot/css/site.css` (referenced by `_Layout.cshtml:61`, so **not**
dead) — the `.form-floating > .form-control::placeholder` rules are default-scaffold RTL
tweaks; Faed's forms use `faed-field`, not `form-floating`. Out of scope if you treat it as
adjacent to the `faed.css` "do not touch" rule; noting it only for completeness.

---

## Deletion list

Every entry is a **tracked** file (`git ls-files`), with proof it is dead. After the
deletions, `dotnet build Faed.slnx -warnaserror && dotnet test` must pass **and the Step 9
rehearsal must be re-run** (a missing partial/image/script fails at runtime only).

Keep these deletions in **one commit** so a single `git revert` undoes them.

| # | Path | Proof it is dead | Notes |
|---|---|---|---|
| D1 | `plan.md` (1,701 lines) | Pre-`faed-core` "Final UI/UX Production Polish Plan"; describes "fashion-related inventory", treats "Cloudflare R2, Brevo email … as complete". Superseded by `docs/DESIGN-BRIEF.md` + Phases 11–13. Not referenced by `Faed.slnx`, `*.csproj`, `README.md`, `CLAUDE.md`, or any `docs/` file (except `PHASE-PLAN.md` naming it as an example to delete). | **Decision-scan first** (rule 5 / prior loss history): skim for any rule not already in `CORE.md` / `BUSINESS-MODEL.md` / `DESIGN-BRIEF.md`; move it before deleting. Recommend you eyeball §§20–24 before approving. |
| D2 | `design-previews/` (8 files: `index.html`, `home.html`, `shop.html`, `listing-detail.html`, `reserve.html`, `storefront.html`, `assets/cards.js`, `assets/preview.css`) | Phase 11 mockups. `faed.css:8050` comment: fx- components were *"ported from the approved /design-previews"* — i.e. the port is done. No `.cs`/`.cshtml`/build reference. Step 5 explicitly names "mockup `.html` files" for deletion. | After deleting, the `faed.css:8050` comment becomes a dangling reference — recommend a one-line comment fix (needs approval under rule 3). |
| D3 | `Claude outputs/COMMITS-AND-COMMENTS.md` + the `Claude outputs/` directory | Byte-identical duplicate of `docs/COMMITS-AND-COMMENTS.md` (`diff` = no output). `CLAUDE.md` references the `docs/` copy. Directory holds nothing else. | No decision lost — identical content. |
| D4 | `wwwroot/js/listing-detail.js` + the `wwwroot/js/` directory | See MEDIUM-5. Not referenced by any view/layout; superseded by the inline script in `Views/Listing/Details.cshtml`. | Update the comment in `PublicMarketplaceModels.cs:167`. |
| D5 | `src/Faed.Web/Properties/PublishProfiles/` (2 `.pubxml`) | See LOW-1. Visual Studio publish profiles; nothing in the repo references them; project has no live deployment. | — |
| D6 | `src/Faed.Web/Properties/serviceDependencies.json`, `serviceDependencies.local.json` | Visual Studio "Connected Services" scaffolding for the LocalDB connection. Not read by the app (the connection string comes from `appsettings*.json` / env). Not referenced anywhere. | Low-value IDE cruft. |

**Not proposed — asked instead (rule 5):**

- `docs/DESIGN-BRIEF.md`, `docs/DEMO-SCRIPT.md`, `docs/CREDITS.md` — see BLOCKER-1. Keep-list
  says delete; the code says keep. **Your decision.**
- `tools/demo-images/` (script + 18 source `.jpg`s + `manifest.json`) — **recommend keep.**
  `docs/CREDITS.md` names `tools/demo-images/sources/` and `manifest.json` as the licence
  provenance for the committed demo PNGs, and `generate-demo-images.ps1` is how those PNGs
  are regenerated. It is build-time-only (never published — `.csproj` sets
  `CopyToPublishDirectory=Never` on the PNGs) but it is the audit trail for CC-licensed
  images. Confirm you want it kept.
- `docs/CREDITS.md` note: it references `tools/demo-images/manifest.json` for attribution
  data — that file **is** tracked, so the reference resolves.

**Editor/OS droppings, tracked build output, backups, `*-v2`/`Index2`/`Class1` files,
framework `WeatherForecast*` leftovers, archives/media dumps, empty dirs:** none found.
`.gitignore` already covers `bin/ obj/ *.user *.suo .vs/ .idea/ TestResults/ *.log
appsettings.Local.json` etc., and `git ls-files` confirms none are tracked.

**`faed.css` (rule 3):** not audited or refactored. Reported only: the retired fashion-era
token literals `#f7f3eb` / `#0d6957` are still present as fallback comments per
`DESIGN-BRIEF.md` §5.2 — **left in place, not deleted.**

---

## Step 6 — Test detail

46 `[Fact]`/`[Theory]` cases, all green (run before the SDK broke). Seven files:

| Plan theme | File | Non-vacuous? |
|---|---|---|
| Listing status transitions | `Models/Entities/ListingTests.cs` (`Lifecycle_*`, `Approve_WithoutFirstSubmitting_Throws`, `SubmitForReview_WhenAlreadyLive_Throws`, `Hide_WhenStillDraft_Throws`) | Yes — asserts each transition and each illegal one throws `DomainException`. |
| Submission blockers | same file (`DescribeSubmissionBlockers_*`, `SubmitForReview_WithABlocker_ThrowsReportingIt`) | Yes — each blocker (photo / condition / variant / original-price / defect-photo) asserted individually by `SubmissionBlockerFields`. |
| Three-check publish gate | `Services/Subscriptions/PublishGateTests.cs` (`UnapprovedMerchant_CanBuildDrafts_ButEachGateConditionBlocksPublishingInTurn`) | Yes — drives all three conditions in turn and asserts a distinct message ("approved" / "subscription" / "plan limit") for each. |
| Reservation expiry | `Services/Ordering/ReservationExpiryTests.cs` | Yes — 12 h cancel + stock release, concurrent-update-as-handled, 48 h `NoShow`, 72 h auto-close. |
| Quota rule | `PublishGateTests.cs` (`MerchantAtQuota_CannotSubmit_ButCanAfterPausingOne`, `Renewal_RestoresLapsedListings_NewestFirst_CappedAtQuota`) | Yes — quota block, pause-then-publish, and lapse→renew restore capped at the new quota with `HiddenBySubscriptionLapse` asserted. |

`ReservationExpiryTests.cs:50-51` still documents the InMemory `rowversion` limitation. ✔

**Coverage that the plan's Step 6 wording implies is missing but is actually present:**
the 12/48/72-hour sweeps (`ReservationExpiryTests`) and the response-rate below/above the
5-order threshold (`Services/Ordering/MerchantResponseServiceTests.cs` —
`Merchant_WithEnoughOrders_ShowsRateAndBucketedTime`,
`Merchant_BelowFiveOrders_IsNewSeller_WithNoPercentage`, plus a window-cutoff test).

**Genuine test gap:** "Actually collected" recovery — but see HIGH-2, the feature itself is
missing, so there is nothing to test yet.

---

## Step 7 — Invariants

| # | Invariant | Holds? | Enforced by |
|---|---|---|---|
| 1 | `rowversion` on stock quantity | ✔ | `Models/Entities/ListingVariant.cs:85` (`byte[] RowVersion`) + `Data/Configurations/ListingVariantConfiguration.cs:35` (`.IsRowVersion()`). Also `MerchantSubscription.RowVersion`. `OrderService` handles `DbUpdateConcurrencyException` (tested). |
| 2 | Order snapshots | ✔ | `Models/Entities/OrderItem.cs:46-53` captures `UnitPriceSnapshot`, `LineTotalSnapshot`, `ListingTitleSnapshot`, `VariantSnapshot`, `ConditionGradeSnapshot`, `DiscountReasonSnapshot` at construction; all `private set`. |
| 3 | No physical deletes of transactional history | ✔ | `Data/Configurations/OrderConfiguration.cs` (FKs `Restrict`, lines 70/77/82/121/126), `ReviewConfiguration.cs` (`Restrict` ×3), `ListingConfiguration.cs` (`Restrict` to Category/Merchant). `Listing.Delete` / `Order` terminal states deactivate, never remove. |
| 4 | Moderation history append-only | ✔ | `Models/Entities/Listing.cs:919` (`_moderations.Add(new ListingModeration(...))`); decisions call `PendingModeration.Resolve(...)` on the *open* row only; no code path updates or removes a resolved row. `ListingModerationService.cs:185` comment confirms "appends a new row". |
| 5 | Business rules in the entity | ✔ | `Listing.cs` is a rich aggregate: `SubmitForReview`, `Approve`, `Reject`, `DescribeSubmissionBlockers`, `SetVariantStock`, `OpenModeration` with encapsulated `_moderations`/`_variants`/`_media`. Services orchestrate, they do not hold the rules. |
| 6 | Nothing waits on a human forever | ✔ | `Services/Ordering/OrderDeadlineService.cs` (hosted, `DependencyInjection.cs:66`) sweeps 12 h/48 h/72 h; `Services/Subscriptions/SubscriptionExpiryService.cs` (hosted, `:80`) sweeps subscription expiry. Both skipped only under the `Testing` environment. |
| 7 | Publishing needs two independent conditions | ✔ | Publish gate checks approved verification **and** active subscription **and** quota headroom as three separate checks with three messages — `MerchantListingService.SubmitForReviewAsync` via `SubscriptionService`; proven by `PublishGateTests`. |
| 8 | Money never touches the platform | ✔ | No `Payment`/`Wallet`/`Balance`/`Escrow`/`Invoice` entity exists (grep = none). `MerchantSubscription.PaymentReference` is a free-text admin-entered string; `ManualSubscriptionBilling` only records it. |

**Carried-across rules:**

- *Quantity-only edit must not reopen moderation* — ✔ `Listing.SetVariantStock` (`Listing.cs:~790-820`) changes stock without touching `_moderations`; the rule is commented there ("That rule outlived the deleted `IInventoryService`…"). Tested:
  `ListingTests.SetVariantStock_OnALiveListing_NeverReopensModeration`.
- *`HiddenBySubscriptionLapse` distinct from a merchant Pause* — ✔ separate boolean on
  `Listing` mirroring `HiddenByAdmin`; `SubscriptionExpiryService` sets it, renewal clears it
  newest-first up to quota. Tested:
  `PublishGateTests.Renewal_RestoresLapsedListings_NewestFirst_CappedAtQuota`.

**"Do not add" list:** no new NuGet packages beyond the fixed stack + `AWSSDK.S3` (pre-existing,
used by `R2FileStorage`); no frontend framework or build step; no in-app messaging (WhatsApp
`wa.me` only — `Rendering/WhatsAppLink.cs`); no payment/escrow/wallet; no B2B; no dispute
system; no auctions/shipping/warehouse. Nothing crept in during Phases 11–13.

---

## Step 9 — Defence rehearsal

**Partly done — an unauthenticated clone-to-running smoke pass completed clean. The
authenticated three-role click-through with per-step timings was not driven and still needs
a human run.**

Mid-session the `dotnet` launcher stopped resolving on `PATH` (it still works when invoked
by full path, `C:\Program Files\dotnet\dotnet.exe` — a PATH/lookup fault, not a missing
binary; `dotnet --version` = `10.0.401`). Steps 2/3/6 finished before that. The rehearsal
below was run via the full path.

### What was run

| Step | Command | Result |
|---|---|---|
| Fresh clone | `git clone <repo> faed-rehearsal` → `git checkout faed-core` (HEAD `ab6f3a3`) | OK |
| Build | `dotnet build Faed.slnx -warnaserror` | **0 warnings, 0 errors — ~40 s** |
| DB from empty | `dotnet ef database drop -f` (did not exist) → `dotnet ef database update` (target `FaedRehearsal`, a throwaway catalog so the working DB was untouched) | **Created in one command.** `Done.` |
| Reference-data seed | app startup | `Seeded 19 catalog reference row(s).` = 3 plans + 4 condition grades + 8 discount reasons + 4 categories (root + 3 launch). Matches the acceptance list. |
| Demo seed | `Faed__DemoSeed__Enabled=true`, `Faed__DemoSeed__Password=Demo!Pass123` | `Demo data set seeded.` — **no error.** Confirms `DemoDataSeeder` drives the real services end-to-end without throwing. |
| App start | `dotnet run --no-launch-profile`, `ASPNETCORE_URLS=http://localhost:5199` | `Now listening on: http://localhost:5199` · `Application started.` |

### HTTP smoke pass (unauthenticated)

| URL | Status | Notes |
|---|---|---|
| `/` | 200 | Renders 3 category blocks + `GRADE A`–`GRADE D` stamps in the grid |
| `/shop` | 200 | 10 seeded listings, condition filter (`?condition=D`) 200 |
| `/listing/upright-vacuum-cleaner` | 200 | "Disclosed condition" + rust `DEFECT` tag present; response line *"67% response … responds within …"* (Amman Kitchen Co, ≥5 orders) |
| `/store/petra-power-tools` | 200 | **"New seller"** badge, no percentage — matches `DESIGN-BRIEF.md` §5.5 and `DEMO-SCRIPT.md` |
| `/store/amman-kitchen-co` | 200 | — |
| `/listing-images/{id}` | 200 `image/png` | Demo product photos serve. **One was 1.97 MB** — unoptimised; fine locally, worth a note. |
| `/Identity/Account/Login`, `/Register`, `/Home/Privacy` | 200 | — |
| `/Buyer/Reservation?listingId=…` | 302 → `/Identity/Account/Login?ReturnUrl=…` | Auth gate correct |
| `/Merchant/Listings`, `/Admin`, `/Buyer/Orders` | 302 → login (200 after follow) | Auth gate correct — not a data leak |
| `/status/404`, any unknown path | 404 | Branded status page, no stack trace |

No unhandled exception, no developer exception page, no `Internal Server Error` string in any
response body. The rehearsal database was dropped and the app stopped afterwards.

### Still to do (human run, then again after the Step 5 deletions)

1. Log in as each seeded account (`Demo!Pass123`) and **time**: merchant publishes an item ·
   admin approves verification + listing · buyer reserves · merchant confirms · contact
   reveal · pickup completes · buyer reviews.
2. Watch for console errors and slow pages during that walk.
3. Re-seed twice in one process lifetime to confirm idempotency beyond the reference-data
   guard already observed (the seeder reads `ConditionGrades`/`DiscountReasons`/`Categories`
   before inserting).
4. Diff the live screens against `docs/DEMO-SCRIPT.md` (seeded order references such as
   `#Z3ZVKV`, the lifecycle-state orders).

Static pre-checks (in place of driving every screen):
- Every controller action named in the demo script resolves to a route.
- All three category images + the home hero return 200; no fashion-era placeholder survives.
- No Razor view without a backing action — the Phase 8/9/12 merges left no orphan
  (`Checkout/`, `Workspace.cshtml`, `Create.cshtml` are all gone).

---

## State of the repo

| Metric | Value |
|---|---|
| Tracked files (`git ls-files`) | 385 |
| Proposed for deletion | 6 paths / ~14 files (see Deletion list); +3 docs pending your decision |
| Entity types | 16 `DbSet`s + `ApplicationUser` = 17 aggregate roots; 21 mapped EF types; 20 files in `Models/Entities/` |
| C# lines (`src/`, `.cs`) | 21,230 |
| Razor views (`src/`, `.cshtml`) | 51 |
| Migrations | 1 (`20260909112228_InitialCreate`) + model snapshot |
| Tests | 46 `[Fact]`/`[Theory]` across 7 files — all green |
| Build warnings (`-warnaserror`) | 0 |
| Secrets tracked | 0 |

---

## Recommended order of work (after your review)

1. **BLOCKER-1** — amend the keep-list (or decide otherwise). Nothing else in Step 5 should
   move until this is settled.
2. **HIGH-2** — decide: implement "Actually collected", or document it as cut.
3. **HIGH-1**, **HIGH-3**, **MEDIUM-1/2/3** — copy + doc fixes, one commit each, each ending
   `dotnet build Faed.slnx && dotnet test`.
4. **Deletion commit** (D1–D6) — its own commit; decision-scan `plan.md` first.
5. **MEDIUM-4/6**, **Step 8** doc regeneration (`04-DOMAIN-MODEL.md`, Phases 11–13 back-fill,
   `README.md`).
6. **LOW-1** (fold into the deletion commit), **LOW-2/3/4/5** — optional.
7. **Step 9 authenticated walk-through** (with timings) — then the whole rehearsal again
   after the deletion commit.

*End of audit. Awaiting approval before any code change.*
