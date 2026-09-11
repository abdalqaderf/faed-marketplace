# Faed — Design Brief

> **Status: revised — 11 September 2026.** The 10 September lock on Direction A ("Trade
> Counter") is superseded by §5.0 below, in a new session as that lock required. The
> product owner reviewed the built home page and rejected it: no hero section, no
> introduction to the site, and the flat/square treatment read as dated rather than
> distinctive. §5.1 onward is amended in place rather than rewritten from scratch — most
> of Direction A survives (ink-for-acting/rust-for-disclosure, the battery-meter condition
> stamp, the overall layout of each of the five screens); what changes is the shape
> language (rounded, soft shadows instead of zero-radius/zero-shadow) and the addition of
> a real home hero. See §5.0 for exactly what changed and why.
>
> **Further revised — 11 September 2026, same day (§5.13).** The built §5.0 hero and card
> treatment were themselves reviewed and read as generic/templated; the public header and
> the signed-in header were also inconsistent. §5.13 corrects the ink/paper/rust palette,
> the card proportions, the hero's decorative visual, unifies the navbar structure, and
> restyles the (already-existing) split-screen Login/Register layout. See §5.13 for what
> changed, why, and the scope limits it was kept inside.

---

## 5.0 Revision — 11 September 2026

**What triggered this:** the product owner's own words on seeing the built home page —
"feels very old, there's no hero section, no introduction to the site; I want something
modern, close to the site's purpose, and comfortable to look at, with an appropriate card
size." That is a rejection of Direction A's core bet (zero-decoration, unforgiving flat
squares), not a request for a small tweak.

**What survives from §5.1–§5.11 unchanged:**
- Ink for acting, rust for disclosing — never swapped (§5.3).
- The condition stamp mechanic: one hue, four fill levels, battery-meter reading, one
  placement per screen (§5.4).
- Product photos as clean cutouts on the grey card background; defect photos never cut
  out (§5.6).
- The five-screen scope, the merchant response rate treatment (§5.5), the shop and
  storefront layout structure (§5.7, §5.8).
- `Archivo` / `Archivo Narrow` / `IBM Plex Sans`, and the `--faed-ink` / `--faed-paper` /
  `--faed-rust` / `--faed-card-bg` colour tokens.

**What changes, and why:**
1. **Shape language.** `--faed-radius` stays `2px` and untouched — but only for the
   workspace, admin and identity screens, which keep Direction A's flat look exactly as
   built (out of scope for this revision; token pass only, per §5.12). The five public
   screens now use a **separate set of tokens** — `--fx-radius-sm` (10px), `--fx-radius`
   (16px), `--fx-radius-lg` (24px), `--fx-radius-pill` (999px) — plus a soft-shadow scale
   (`--fx-shadow-xs/sm/md/lg`) and a soft border tint (`--fx-border-soft`,
   `rgba(22,24,26,0.11)` replacing solid 1px ink borders on cards). These are new tokens,
   not redefinitions — `wwwroot/css/faed.css`'s shared `--faed-radius*` tokens are
   unchanged, so this does not touch workspace/admin/identity screens at all.
2. **A real home hero.** The home page previously opened straight into the wordmark and a
   one-line tagline, with no image, no CTA, and no visible trust signal above the fold —
   this is the "no hero, no introduction" the owner flagged. The hero now has: an eyebrow
   label, a headline, the existing tagline, two calls to action (Browse the shop · Sell on
   Faed), a three-line trust strip (verified merchants · disclosed condition · cash at
   pickup, reusing the `shield-check` / `tag` / `eye` icons already used in the identity
   aside), and a decorative showcase collage on the right built from the same photo-tile
   and condition-stamp components a real listing card uses — not a stock photo. (The
   unused `wwwroot/images/home/faed-home-hero.webp` is a leftover fashion-sector asset —
   shoes, a handbag, folded clothing — wrong for an appliances/power-tools marketplace and
   was never wired into any view; it stays on disk, unreferenced, rather than being wired
   in or deleted, since deleting assets is out of scope for this revision.)
3. **Cards get room to breathe.** Card padding, grid gap, and category-card sizing all
   increased slightly; card images and the condition stamp gained soft shadows and a lift
   on hover (max ~4px, within the original motion budget in intent if not in the letter of
   §5.10); buttons, badges, search/filter controls and the identity avatar are now pill or
   rounded-square shaped rather than the original hard square.
4. **Avatar shape (reverses §5.9).** §5.9 fixed the identity avatar as "square, never a
   circle" — that rule is now reversed for the five public screens: `.fx-avatar` is a
   circle. It stays ink-filled with paper-coloured initials.
5. **`.fx-price__off` colour.** The "X% off" text now renders in rust rather than muted
   grey, since a discount percentage is disclosure information under §5.3's own test — not
   a new exception to the rule, a corrected application of it.

**What this revision does not do:** it does not reopen the merchant/buyer/admin flows,
the entity model, or anything in `CORE.md` / `BUSINESS-MODEL.md`; it does not touch
`faed.css` outside the self-contained "PHASE 12 — public surface" block at the end of the
file; and it does not change the workspace or admin screens, which keep §5.12's token-pass
treatment exactly as before.

---

## 1. The goal — read this first

**Faed is being finished for a university graduation defence, not a commercial launch.**
No real merchants, no real buyers, no payment integration, no legal review. Those are all
deliberately out of scope.

That single fact sets every priority here:

> **The demo is the product.** A panel will not read the code. They will watch a screen, click
> into corners, and judge with their eyes. Visual quality and a smooth walkthrough outrank
> everything else — including code hygiene.

What this **de-prioritises**: refactoring `faed.css`, email delivery, mobile polish beyond
"doesn't break", performance.

What this **elevates**: real-looking images, the home page, empty states, and the three
walkthrough paths (merchant publishes · buyer reserves · admin approves).

---

## 2. What Faed is

A marketplace where verified, subscribed merchants in Amman sell **open-box and ex-display**
appliances and power tools — items that were opened, returned, dented, scratched, or sat in a
showroom.

It is not a classifieds site. Every listing carries a **disclosed condition**, the **reason the
price is reduced**, and a **photo of the defect** where one exists. Buyers **reserve** online
and pay **cash at pickup** — the platform never handles money and never sees the transaction.
Revenue is a **monthly merchant subscription**, not commission.

| | |
|---|---|
| Market | Amman, Jordan |
| UI language | **English only** |
| Currency | JOD, three decimal places (`95.000 JD`) |
| Price band | 20 – 150 JOD (carryable, so pickup is realistic) |
| Categories | Small Kitchen Appliances · Home & Cleaning · Power Tools & Workshop |
| Roles | Buyer · Merchant · Admin (one role per user) |

Full scope: `CORE.md`. Reasoning behind every rule: `BUSINESS-MODEL.md`. Repo rules: `CLAUDE.md`.

---

## 3. Locked decisions — do not reopen

These were argued and settled before the visual system existed. A design must work **within**
them.

**Merchant listing form** — one page, **nine fields**: photos · title · category · condition ·
warranty · price · original price (optional) · quantity · description (optional, last). Only
eight ask for a decision.

**The condition question** — one question, four cards. One click sets both the condition grade
and the discount reason; the merchant never sees either term.

| Card | Merchant sees | Stored as |
|---|---|---|
| 1 | **Sealed** — new, unopened box | Grade A + Overstock |
| 2 | **Box opened or damaged** — item is new and unused | Grade B + PackagingDamage |
| 3 | **Customer return** — opened and checked, never used | Grade C + CustomerReturn |
| 4 | **Ex-display** — light scratches or marks from showroom use | Grade D + DisplayItem |

Cards 2 and 4 reveal a **required defect-photo upload inline**, the moment they are picked.

**Merchant vocabulary** — the merchant sees exactly two lifecycle words: **Pause** and
**Delete**. Never "variant", "SKU", "condition grade", "discount reason", "archive", "hide".

**Buyers Reserve, never check out.** No cart, no payment language anywhere. The reserve page is
a three-line confirmation: where to collect, that it expires in 12 hours if the shop does not
confirm, and that payment is cash at the shop.

**Shop controls** — four filters (category, condition, price range, text) plus one sort control
(Newest · Price low–high · Price high–low). No advanced-filter drawer. Default order is newest
first, with merchant response rate as the tie-breaker among same-day listings.

**Merchant response rate** — shown on the storefront and under each listing. See §5.5 for the
exact treatment, including the New Seller state.

**Order lifecycle** — Pending → Confirmed (within 12 h) → Ready for pickup → Completed, with
Cancelled and NoShow. Contact details (address, phone, WhatsApp button) are revealed **only
after the merchant confirms**; before that the buyer sees the shop's area only.

**Subscription plans** — Basic 35 JD / 15 listings · Standard 50 JD / 40 · Pro 80 JD / 120.
Monthly only.

---

## 4. The condition color principle

**The condition indicator is never a red-to-green scale.**

A traffic light says three of the four conditions are a warning. But nothing on Faed is faulty —
only its history differs, and a sealed unit is not *safer* than an ex-display one, merely newer
to the shelf. Red would push buyers away from most of the catalogue and undo the disclosure the
platform exists for.

So: **one hue, four levels of fill** — like a battery meter. Information, not alarm. §5.4
below fixes which hue and where it appears.

### 4.1 The general case

The condition indicator is the sharpest instance of a rule that holds everywhere: **the
written label carries the meaning.** Colour and icons reinforce a status; they never carry it
alone. Any state a user must read — an order status, an approval, a validation error — is
legible in plain text with the colour and the icon removed.

---

## 5. Visual identity — final

### 5.1 Direction: A, modified

**Direction A ("Trade Counter")** — square, ruled, high-contrast, zero-decoration — is the
final direction. Direction B ("Calm Shelf") was rejected.

Reasoning:

- **A matches the product's actual claim.** Faed's core value is mandatory, structured
  disclosure. A's hard-edged stamp treatment for the condition indicator *is* an inspection
  label, literally — the form follows the function instead of decorating it.
- **B was already flagged as the weaker choice in the original brief**, which described it as
  "looks like a thousand other shops." For a graduation defence partly judged on
  distinctiveness, a self-acknowledged generic direction is the wrong pick.
- **B's serif display type, pill shapes and soft shadows read as boutique retail** — the same
  visual family as the abandoned fashion-sector tokens this project moved away from. Appliances
  and power tools call for the trade-counter register, not a calm shelf.

A's known risk — unforgiving of weak product photography — is mitigated in §5.6.

### 5.2 Tokens (final)

| Token | Value | Use |
|---|---|---|
| `--faed-ink` | `#16181A` | Text, rules, borders, **all buttons and CTAs** |
| `--faed-paper` | `#F4F4F2` | Page background |
| `--faed-card-bg` | `#E7E5DE` | Product photo card background (not pure white — see §5.6) |
| `--faed-rust` | `#B23A00` | **Disclosure only** — see §5.3. Never a button color |
| `--faed-success` | `#25754A` | System feedback only — a Reserve/Publish/Confirm action succeeded. Not a condition-grade colour; §4's rule governs the condition indicator specifically, not system status messages |
| `--faed-danger` | `#AD352D` | System feedback only — an action failed, or inline field validation. Same carve-out as `--faed-success` |
| `--faed-radius` | `2px` | All cards, inputs, buttons |
| Display font | `Archivo` (weight 600) | Headings, wordmark |
| Price font | `Archivo Narrow` (weight 600) | Prices only |
| Body font | `IBM Plex Sans` (weights 400/500) | Everything else |

**Retired:** the original brief's `#E2610A` hot orange and `0px` radius (superseded by the
values above), and the pre-existing `#f7f3eb` / `#0d6957` / `12px` fashion-sector tokens
(already retired in the original brief, confirmed retired here). `--faed-buyer-surface` is
also retired — it was a workspace-only warm-white value with no counterpart in this system.

**Exception:** CSS `background-image` data-URIs (e.g. the `.fx-select` chevron) cannot read
custom properties. A literal hex matching the current token value is the accepted exception —
not a violation — as long as it's a data-URI and nothing else.

### 5.3 Color semantics — the rule that must not drift

> **Ink is for acting. Rust is for disclosing. They never swap.**

- `--faed-ink` is the color of every button, every CTA, every actionable link: Reserve,
  Publish, Pause, Delete, Confirm, Search.
- `--faed-rust` appears in exactly two places: the condition stamp fill, and the "defect
  photo" tag on a thumbnail. It never appears on anything the user clicks to take an action.

If a new component needs an accent color and the designer reaches for rust, that is the signal
the component is disclosure-related, not action-related — check the assumption before coding.

**One primary action per task group.** Every button is ink, so weight and wording do the work
colour cannot: one action reads as dominant, the rest step back, and same-weight buttons never
compete. A destructive action — the merchant's **Delete** sitting beside **Pause** — is set
apart by its wording and its treatment, never by an icon alone, and never wears the plain
primary style.

### 5.4 Condition indicator — one location, no duplicates

The condition indicator appears **in exactly one place per screen**: a stamp in the corner of
the primary product photo, on every card and on the listing detail page alike. It is never
duplicated as a second colored badge elsewhere on the same screen.

- **Mechanic:** one hue (rust), four fill levels, rendered as small filled/unfilled bars next
  to the grade code (`GRADE A`–`GRADE D`) — a battery-meter reading, not a stoplight.
- **Supporting text** (the plain-language condition name and reason, e.g. "Customer return —
  checked, unused") may appear near the title in secondary-colored plain text. It is text, not
  a second colored badge, and it never repeats the stamp's role.
- **The stamp never gets its own hover or focus state.** It sits inside the clickable card
  photo by design (this is what "corner of the primary product photo" in §5.4 means), so rust
  pixels legitimately land inside a link. Giving the stamp its own hover affordance — a scale,
  shadow, or outline — would make it look like a second, independent click target inside the
  same card. It stays visually inert; the card is the click target, not the stamp.

### 5.4a Screen classification — public vs. workspace

A screen's visual system is determined by **what it is**, not by which `Areas/` folder its
controller lives in. Reserve is a public, fully-redesigned screen (§6) even though its
controller sits under `Areas/Buyer` — it must render with `faed-public-page`, never
`faed-workspace-page` or `faed-buyer-page`. Folder location is a routing convenience, not a
signal for which layout class to apply.

### 5.5 Merchant response rate — including the New Seller state

**Standard state** (5+ orders in the rolling 30-day window): plain text under the merchant name
— *"Usually responds within 2 hours · 94% response rate"*. No badge, no color fill.

**New Seller state** (fewer than 5 orders in the window): the response-rate text is fully
replaced by a neutral outline badge reading **"New seller"** — ink-outline, transparent fill,
no rust, and **no percentage appears anywhere on the same card or page** in this state.

The badge occupies the identical position the response-rate line would occupy, so a merchant
crossing the 5-order threshold doesn't shift any surrounding layout.

### 5.6 Product photography treatment

Two deliberately different treatments, per §8:

- **Product photos** are clean cutouts placed on the `--faed-card-bg` grey card
  (`#E7E5DE`), not pure white. This masks the quality variance that comes from sourcing photos
  from free stock libraries rather than a single studio shoot — a consistent card background
  reads as intentional even when the source images vary.
- **Defect photos** are never cut out. They must show a real background — a shelf, a counter, a
  torn carton — and are tagged with the rust "DEFECT" label from §5.3/§5.4. The visual contrast
  between a clean product cutout and a rough defect photo is what tells the buyer *this exact
  unit, not a catalogue photo* — the premise the whole platform depends on.

---

### 5.7 Shop (browse/filter grid) — layout

Single row, no drawer: text search (flexible width) first, then `Category`, `Condition`,
`Price` as bordered dropdown controls in that order, then a results count line
("`42 items`", muted, 11px) directly under the row. `Sort` sits at the far end of the same
row, visually separated from the four filters by pushing to the row's trailing edge — it reads
as a distinct control, not a fifth filter, matching the rule in `CORE.md` §4.2 that sort is not
a filter. Grid below: 3 columns of the same card component used on Home (§ mockup, this
session) — same stamp, same card border, same typography. No secondary layout for the grid.

### 5.8 Merchant storefront — layout

Header block, full width, bordered bottom: identity avatar (§5.9) · shop name (Archivo 600) ·
a `Verified` outline badge beside the name, ink border, same treatment as the New Seller badge
in §5.5 · area and category line beneath, muted · response-rate line (or New Seller badge)
beneath that. Listing count and "on Faed since" sit right-aligned in the same header block, not
their own section. Filter row beneath the header: `Condition` and `Price` only — no `Category`
filter, since a storefront is usually one merchant's own category — plus `Sort`, same trailing
placement as §5.7. Grid below: 4 columns, same card component, smaller (matches a denser
single-shop view rather than the marketplace-wide Shop grid).

### 5.9 Identity avatar — one shape, no exceptions

**Superseded by §5.0 (11 September revision) for the five public screens: `.fx-avatar` is
now a circle** (`--fx-radius-pill`), ink fill, paper-colored initials — the "never a
circle" rule below is what the original brief said and is kept here for history only.
Workspace/admin avatars (if any share the class) are untouched.

~~Square, `--faed-radius` (2px), ink fill, paper-colored initials — never a circle.~~ This was
inconsistent across this session's own mockups (the listing-detail merchant mini-card used a
circular avatar before this was written down) and was fixed as a single rule: a circle
belongs to Direction B's soft-shelf language, not this one. Applies everywhere a merchant
identity renders — order page, listing detail, storefront header, admin merchant list.

### 5.10 Motion budget

Direction A is nearly still. The whole allowance is a 2–4px lift on a card and soft colour,
shadow and border transitions. No parallax, no bouncing CTAs, no entrance animations, no
element that moves when nothing has been touched.

### 5.11 Feedback and empty states

Every success, error and empty state says two things: **what happened, and what the person can
do next.** An empty screen names what would fill it and points at the action that starts;
an error names the cause and the way forward. Never a bare "No results" or "Something went
wrong".

### 5.12 Surface map

| Surface | Treatment | Status |
|---|---|---|
| **Home** | Full redesign · hero added 11 Sep (§5.0) · hero reworked again 11 Sep (§5.13) | Implemented |
| **Listing detail** | Full redesign · rounded/soft-shadow pass 11 Sep | Implemented |
| **Reserve** | Full redesign · rounded/soft-shadow pass 11 Sep | Implemented |
| **Shop (browse/filter grid)** | Full redesign · rounded/soft-shadow pass 11 Sep | Implemented — see §5.7 |
| **Merchant storefront** | Full redesign · rounded/soft-shadow pass 11 Sep | Implemented — see §5.8 |
| **Identity (Login · Register · password flows)** | Token pass — conventional `label` above input, no floating-label duplication, Faed form system not scaffold defaults. Split-screen layout (form + "why Faed" panel) restyled 11 Sep (§5.13) | Tokens defined (§5.2); split-screen visual restyle implemented, form-control token pass not re-verified this round |
| Merchant workspace · Admin workspace | Token pass only — colours, spacing, type. Do not rebuild them. Explicitly **not** touched by §5.13's palette or navbar-content changes | Tokens defined (§5.2), not applied |
| `faed.css` (8,700+ lines) | Do not audit or refactor. Invisible to the panel, and it works | — |

### 5.13 Revision — Phase 15, 11 September 2026 (unified navbar, "Inspection Tag" palette correction, denser cards, hung-tag hero, split-screen auth)

**What triggered this:** three rounds of product-owner feedback on the built site, worked
through in the same session: (1) the public pages and the signed-in/workspace pages used
two visibly different header components, so navigating from Home to Sign in felt like
leaving the site; (2) the §5.0 hero and card treatment, seen live, still read as a generic
templated layout ("لا اريد ان يظهر التصميم وكان الذكاء الاصطناعي قام به" — "I don't want the
design to look like AI made it"), and cards were judged too narrow/tall; (3) the Login/Register
pages were requested split into two halves, image and form. The owner then had to step away
and explicitly delegated the remaining implementation decisions ("خذ افضل القرارات" — "take
the best decisions") rather than pausing the phase.

**What survives from §5.0–§5.12 unchanged:**
- Ink-for-acting / rust-for-disclosure (§5.3) — not touched, only the ink and rust hex
  values themselves are corrected (below).
- The condition-stamp mechanic, one placement per screen (§5.4) — the stamp is now a
  literal tag silhouette (a `clip-path` polygon plus a punched hole) instead of a plain
  rounded rectangle, but the battery-meter mechanic, placement and never-hover rule are
  unchanged.
- The five-screen scope, shop/storefront layout structure (§5.7, §5.8), circular avatar
  (§5.9), motion budget (§5.10).
- `--faed-radius`, `--fx-radius*` and every workspace/admin/identity-management token —
  unchanged, and this revision does not extend to those screens at all (see scope note
  below).

**What changes, and why:**

1. **Unified navbar structure.** The plain `fx-site-header` (public pages: wordmark + three
   text links, no search, no account state) is removed. Every route now renders the same
   `faed-navbar` component; only its *content* — which primary links show, whether the
   search box renders, the account cluster — still varies by auth state and role, never by
   which area of the app the visitor is in. **The navbar's own established colour scheme
   (the green `--faed-brand` hover/active treatment) is deliberately left unchanged** —
   this revision unifies structure, not the navbar's colour identity, to avoid either
   recolouring the workspace/admin chrome (out of scope) or a mismatch between the navbar
   and the page body's new palette. A `Sell on Faed` link, previously only reachable from
   inside the account dropdown, is now a top-level nav item for any visitor who isn't
   already a merchant or admin.
2. **"Inspection Tag" palette, corrected and rescoped.** The §5.0 token values are replaced
   for the **public and auth surfaces only** — `--faed-ink: #1e2b28` (teal-charcoal,
   replacing near-black `#16181a`), `--faed-paper: #f4f0e5` (warm kraft, replacing plain
   cream `#f4f4f2`), `--faed-card-bg: #e7dfc9` (deeper kraft, replacing `#e7e5de`),
   `--faed-rust: #a67425` (burnished brass, replacing red-orange `#b23a00`), plus a new
   `--faed-rust-text: #815a1d` for the two places rust is actual small text
   (`.fx-price__off`, `.fx-defect__tag`) rather than an icon or border — `--faed-rust`
   alone clears only ~3:1 there, below the 4.5:1 AA text minimum; `--faed-rust-text`
   clears 5.4:1+. `--faed-text-muted` is separately corrected to `#5a635f` for these
   surfaces, since the shared `#5c5f63` was tuned for the old, cooler paper/card-bg and
   falls to ~3.6–4.2:1 against the new, warmer ones. **Scope is load-bearing, not
   cosmetic:** `--faed-ink` etc. are shared custom properties consumed by hundreds of
   workspace/merchant/admin/identity-management selectors sitewide. Redeclaring them at
   `:root` would repaint the entire application. Instead they are redeclared only inside
   `body.faed-public-page, body.faed-auth-page { … }` — the two body classes
   `_Layout.cshtml` already assigns to exactly the pages this feedback was about
   (Home/Shop/Listing/Reserve/Storefront and Login/Register/forgot/reset). Every
   workspace/admin/account-settings page keeps the original §5.2 values via ordinary CSS
   inheritance. Do not move these to `:root` without a separate, explicit decision to
   re-theme the workspace/admin UI — that was never asked for here.
3. **Cards: width restored, height reduced (not both cut).** The §5.0 pass was read as
   "too narrow, too tall." `.fx-photo`'s aspect ratio changes from `1/1` to `4/3`
   (shorter without narrowing), and the fixed 3/4-column grids merge into
   `repeat(auto-fill, minmax(240px, 1fr))` (a comfortable minimum width that still adds
   columns on wide viewports, instead of forcing a fixed count that shrank each card on
   the same row width). Card body padding and internal gaps are tightened to match.
4. **Hero: a single hung tag over a blueprint grid, not a floating-card collage.** The §5.0
   hero's decorative half — a blurred colour "blob" behind two overlapping, offset photo
   cards — is the generic "floating SaaS dashboard cards" pattern named in the "doesn't
   look AI-made" feedback. It is replaced with one physical object: a single inspection-tag
   card (`.fx-hero__tagcard`), rotated slightly, hung from a punched hole and a length of
   string (`.fx-hero__hole`, `.fx-hero__string`), over a faint two-line blueprint-grid
   ground (`.fx-hero__visual`). The photo slot inside the tag still uses the existing
   `CategoryGlyph` stroke icon and `ConditionStamp.Render("A")` — there is no real photo
   asset yet, and an unfinished "temporary" placeholder must not ship to production; the
   `<svg>` is meant to be swapped for a real `<img>` once one exists (see item 6 below and
   the comment at the top of `Views/Home/Index.cshtml`). Hero padding, title size and the
   spacing under the tagline are all reduced slightly, matching the "compact hero" request.
5. **Split-screen Login/Register.** The two-panel `.faed-auth-layout` grid (form on one
   side, a "why use Faed" aside on the other) already existed in the codebase before this
   round — it was not built from scratch. Only `.faed-auth-context`'s background changed:
   the previous radial-gradient blobs and circle outlines on `--faed-brand-deep` (the
   pattern named as generic) are replaced with `--faed-ink` plus the same faint
   blueprint-grid used behind the home hero, so the account pages read as the same
   considered surface as the rest of the public site. **No literal photo was inserted** —
   the aside is already filled with working trust copy (verified merchants · disclosed
   condition · inspect before paying) and no real photo asset exists yet; the background
   restyle is the fix applied to the "make it feel less generic" half of the original
   request, and a photo can be added into `.faed-auth-context__inner` once one exists.
   Because `Areas/Identity/Pages/Account/Login.cshtml` doesn't exist locally (Login is
   served by the default ASP.NET Identity UI Razor Class Library), no scaffolding was
   needed for it to pick up this layout — `_ViewStart.cshtml` already points every
   Identity page at `_Layout.cshtml`, which already wraps `isAuthPage` routes in
   `.faed-auth-layout` regardless of where the page itself lives.
6. **Condition stamp becomes a literal tag shape.** `.fx-stamp`/`.fx-stamp--lg` gain a
   `clip-path: polygon(...)` tag silhouette and a `::before` punched-hole pseudo-element —
   pure CSS on the existing markup; `Rendering/ConditionStamp.cs` is unchanged.

**Scope discipline (explicit, since this work was completed without the product owner
present to confirm each call):** every change in this revision is scoped to
`body.faed-public-page` and `body.faed-auth-page` for colour, and to shared partials
(`_Layout.cshtml`, `_LoginPartial.cshtml`) for the navbar *structure* only. No workspace,
admin, or account-settings screen's colours change. The navbar's own green hover/active
colour scheme is unchanged everywhere. This was a judgment call made in the product
owner's absence, opting for the smaller blast radius; it can be revisited if a stronger,
navbar-colour-inclusive unification is wanted later.

**Verification note:** this round of changes was made and reviewed manually — reading
every edited file in full after editing, a brace-balance check and a grep for orphaned
class references across `faed.css`, and an explicit WCAG 2.1 contrast computation for
every new colour pairing (§5.13 item 2). **No `dotnet build`, `dotnet test`, or `git diff`
review was possible in the session that made these edits** (no .NET SDK or local shell
access from that environment). A build and a visual pass through the five public screens
plus Login/Register is the one verification step this revision has not had, and should be
done before treating it as final.

**What this revision does not do:** it does not touch the navbar's colour scheme; it does
not add a real hero or auth photo (both slots are explicitly marked as pending a real
asset); it does not re-run a build or test pass; it does not touch `CORE.md` or
`BUSINESS-MODEL.md`; and it does not change anything about the workspace, admin, or
account-settings screens beyond the navbar-structure and `_LoginPartial.cshtml` markup
changes that render on every page (their colours and layout are unaffected).

### 5.14 Revision — Phase 16, 11 September 2026 (sitewide colour-token promotion)

**What triggered this:** direct feedback that §5.13's scoping — the new palette on the five
public pages and Login/Register only, the old palette everywhere behind sign-in — was
itself an inconsistency: "make the whole site look like one site." That is a fair reading
of §5.13's own disclosed trade-off; this revision closes it for colour.

**What changed:** the corrected values §5.13 declared inside
`body.faed-public-page, body.faed-auth-page { … }` are promoted to the shared `:root`
declarations at the top of `wwwroot/css/faed.css`, replacing the old values there instead
of overriding them per body class. Concretely (old → new, sitewide):
`--faed-ink`/`--faed-text`/`--faed-border-strong`/`--faed-brand`: `#16181a` → `#1e2b28`;
`--faed-paper`/`--faed-bg`: `#f4f4f2` → `#f4f0e5`; `--faed-card-bg`/`--faed-surface-muted`/
`--faed-brand-tint`: `#e7e5de` → `#e7dfc9`; `--faed-rust`: `#b23a00` → `#a67425`, with the
new `--faed-rust-text` (`#815a1d`) promoted alongside it for the two places rust is actual
text rather than an icon/border; `--faed-text-muted`: `#5c5f63` → `#5a635f`;
`--faed-border`: `#d5d4cf` → `#d9d0b8`; `--faed-focus` and the public-surface
`--fx-border-soft`/`--fx-shadow-*` tokens' RGB triples updated from the old ink's `22,24,26`
to the new ink's `30,43,40` so every ink-derived translucency stays in the same family. The
now-redundant `body.faed-public-page`/`body.faed-auth-page` override block is retired (kept
as a comment for the WCAG rationale and revision history, not deleted outright) rather than
simply removed, since every page now reads the same values by inheritance. Contrast was
re-checked at the `:root` level, not just for the public pages: new ink on new paper is
12.9:1, new ink on white 14.7:1, new text-muted on the new card-bg 4.67:1, new text-muted on
white 6.2:1 — all comfortably clear AA, and higher than the values §5.13 already recorded
for the public-only case, since these are the same colours simply applied more broadly.

**What this deliberately does not touch, and why:** the extensive, individually hand-tuned
hex values inside workspace/merchant/admin/buyer COMPONENT rules — dispute cards, evidence
upload, choice cards, tables, subnav hover states, several decorative gradients — were left
exactly as they were. A full colour audit of `faed.css` (a Python scan for every literal hex value in the file)
found 173 distinct hex values across 302 occurrences, the large majority of them outside the
ink/paper/rust token system entirely — a deliberately hand-tuned, cool-toned micro-palette
that predates this phase and does not reference the shared tokens at all. Rewriting that
micro-palette to match the
warmer new base would touch the CSS backing the buyer/merchant/admin transaction flows —
exactly the surface DESIGN-BRIEF.md §5.12 already locks: *"`faed.css` — Do not audit or
refactor. Invisible to the panel, and it works."* Doing that work with no way to render the
result in the session that would have made the edits (see the verification note) would be
guessing at a part of the app the design brief explicitly protects. The trade-off this
leaves: the shared base tokens (page background, primary text, borders, disclosure colour)
are now identical everywhere, while a handful of untouched, workspace-only component accents
sit on a very slightly warmer base than before. That is a materially smaller and less visible
gap than the one this revision closes — the base identity no longer differs page group to
page group — and it keeps the one part of the codebase the product owner has already asked
not to be rebuilt exactly as it was.

**What this also does not do:** it does not add a real photo to the home hero or the
Login/Register aside. No image-generation tool is available in the environment that made
this revision, and fetching a third-party photo to embed in the app carries a licensing
question no session should resolve unilaterally on the product owner's behalf. This stays a
manual step: drop a real photo in and swap the `<svg>` placeholder for an `<img>` (see the
comment at the top of `Views/Home/Index.cshtml`, and `.faed-auth-context__inner` for the
aside) — a small, mechanical change once a licensed image exists, not a design decision.

### 5.15 Revision — Phase 17, 11 September 2026 (hero shows the marketplace's idea, not one listing)

**What triggered this:** explicit product-owner feedback on §5.13/§5.14's hero visual — a
single hung tag styled as one listing ("ex-display air fryer," a price, a grade stamp) does
not communicate what the site *is*; a first-time visitor should read the concept (a graded,
disclosed, multi-category open-box marketplace) from the hero, not one item's price. The
owner authorized restructuring the hero markup/CSS if that's what showing the concept needs.

**What changed:** `.fx-hero__tagcard`/`__hole`/`__string`/`__card-photo`/`__card-info`/
`__card-title`/`__card-price` — the single-listing tag card, complete with a fake price and
title — are retired. In their place, `.fx-hero__scene` is a wider panel (`aspect-ratio: 5/4`,
`max-width: 460px`, up from the old card's 260px) meant to hold one real photograph of
several different tagged, open-box items together — visually closer to the "Trade Counter"
concept §5.1 named the whole direction after than to a single product tile. The inspection-
tag motif is not dropped, only rescaled: one small hole-punched tag (`.fx-hero__scene-tag`,
reading "Every item, graded & disclosed") is pinned to the photo's corner as an accent,
instead of being the entire visual. Until the real photo exists, the panel falls back to
three of the same `CategoryGlyph` icons already used in the category grid just below it
(home-cleaning, power-tools, small-kitchen) spread across the panel — still "several
categories, all disclosed" as a concept, not an empty box or a single icon standing in for
one product. Swapping in the real photo is a one-line change: replace the
`.fx-hero__scene-glyphs` `<div>` with `<img class="fx-hero__scene-photo" src="~/images/
home/hero-scene.jpg" alt="" />`.

**The photo brief given to the product owner** (for generation with an AI image tool, since
none is available in this environment): a documentary/product-style photograph of a trade
counter or table holding several different open, tagged items together — at least one small
kitchen appliance, one power tool, one cleaning appliance — each with a visible kraft
inspection tag (hole + short string) attached, at least one tag showing a visible condition
grade mark, warm directional lighting, background and tones matching the site's palette
(warm kraft `#f4f0e5`/`#e7dfc9`, teal-charcoal shadow `#1e2b28`, brass accent `#a67425`),
landscape or near-square framing suitable for a rounded panel roughly in a 5:4 ratio, no
text or logos in frame, realistic photography rather than illustration or 3D render.

**What this does not do:** it does not touch the Login/Register aside photo gap (§5.14 item
32, unchanged), the sitewide token work from §5.14, or any workspace/admin/merchant/buyer
screen. It was made and reviewed the same way as §5.13/§5.14 — full re-reads of the edited
files, a brace-balance check on `faed.css` — with no `dotnet build` possible in the session
that made the edit (see the standing verification note above), so the panel's actual
rendering (icon spacing, the corner tag not clipping, the 460px panel fitting the hero grid
at common widths) has not been visually confirmed and should be checked once a build is run.

### 5.16 Revision — Phase 18, 11 September 2026 (navbar hover/active colour)

**What triggered this:** direct feedback that the navbar's interaction colour — its hover
and active-state tint — was never brought into the "Inspection Tag" identity, even after
§5.14 promoted the base ink/paper/rust tokens sitewide.

**What was actually wrong, found by tracing every colour literal touching the navbar:** the
named tokens the navbar reads (`--faed-brand`, `--faed-brand-strong`) already resolved to
the new ink and to black — that part was already consistent. The leftover was three
hard-coded `rgba(13, 105, 87, …)` hover-background washes — `13, 105, 87` is the RGB of
`#0D6957`, the "fashion-era teal" this file's own top comment already says was retired when
the token pass renamed `--faed-brand` to the ink/paper system. Those three literals were
never updated to match at the time, so hovering a nav link, a navbar action, or the account
menu trigger tinted the background a leftover teal green instead of the site's own ink.

**What changed:** those three literals — `.faed-navbar .nav-link:hover`,
`.faed-navbar-action:hover`, `.faed-account-menu__trigger:hover` — now use
`rgba(30, 43, 40, …)`, the new ink's RGB at the same opacities (`0.055`/`0.06`/`0.06`) they
already had, matching the ink-tinted hover treatment already used elsewhere (e.g.
`.fx-btn--ghost:hover`). No layout, spacing, or structural change — a value-only fix.

**Follow-up, same day:** two more occurrences of the same `rgba(13, 105, 87, …)` literal —
button glow shadows on `.faed-btn--primary` (used sitewide) and the auth-form submit button
— were outside what "fix the navbar's interaction colour" asked for, so they were flagged
rather than changed. The product owner then asked for those fixed too; both now use
`rgba(30, 43, 40, …)` at their original opacities. No `rgba(13, 105, 87` or `#0D6957`
literal remains anywhere in `faed.css` outside the historical comments describing this fix.

### 5.17 Revision — Phase 19, 11 September 2026 (border-radius audit)

**What triggered this:** a direct question — is `border-radius` consistent across the whole
application — after the product owner had already been told the two-scale split
(`--faed-radius*`, locked at `2px` flat, for workspace/admin/identity vs. `--fx-radius*`,
`10`–`24px`/pill, for the five public+auth screens) is a pre-existing §5.0 decision, not
something this session introduced.

**What was checked:** every one of the 195 `border-radius` declarations in `faed.css` was
classified as (a) referencing `--faed-radius*`, (b) referencing `--fx-radius*`, (c) a
`999px`/`50%` full-round shape (badges, avatars, dots — a shape category independent of
either scale), or (d) a literal value bypassing both token families. Cross-contamination
between the two token families was checked directly: no `.fx-*` selector references
`--faed-radius*` and no selector outside `.fx-*` references `--fx-radius*` — the split holds
with zero mixing. That left 12 literal-value declarations across 9 selectors as the open
question, and each one was traced against the actual, current `Views`/`Areas` tree (freshly
staged from the product owner's machine, not a cached copy) to see whether the selector is
even rendered by any page.

**What was found:** 9 of the 12 belonged to selectors with zero references anywhere in the
codebase — `.faed-hero__discovery`, `.faed-home-category-shell` (both its base rule and its
mobile-breakpoint override), `.faed-shop-search`, `.faed-product-card__media-label`,
`.faed-product-card__discount` (the second, public-surface override of that name),
`.faed-product-card__condition-badge`, and `.faed-home-empty__icon` (both its base rule and
its icon-sizing companion) — all leftover CSS from before the home/shop pages were rewritten
onto `.fx-*` classes (§5.0 onward). Their radius values were not an inconsistency in
anything a visitor can see; they were dead code. The other 3 were live: `.faed-thumb__remove`
(the photo-remove control on the merchant listing form) had a hard-coded `6px` against a
`2px`-flat surface; `.faed-admin-page .faed-admin-filterbar a` computed `calc(var(--faed-radius-sm) - 2px)`
= `0px`; and `.faed-auth-layout` (the login/register split panel) had a hard-coded
`clamp(1rem, 2vw, 1.5rem)` that happened to track the `--fx-radius`/`--fx-radius-lg` range
without referencing it.

**What changed:** the 9 dead declarations were deleted (not re-tokenised — fixing a value
nothing renders would only have dressed up dead code) and replaced with a one-line removal
comment pointing to this section, for anyone who goes looking for the class later. The 3 live
ones were tokenised: `.faed-thumb__remove` now uses `var(--faed-radius)`;
`.faed-admin-filterbar a` now uses `var(--faed-radius-sm)` directly (no `calc()`); and
`.faed-auth-layout` now uses `clamp(var(--fx-radius), 2vw, var(--fx-radius-lg))`, keeping its
fluid behaviour but tied to the public-surface tokens instead of a coincidental literal.

**What this does not do:** it does not touch `.faed-wordmark__mark`'s asymmetric corner (a
deliberate logo shape), `.fx-stamp__bar`'s `1px` (a decorative meter graphic), or
`.fx-card .fx-photo`'s `border-radius: 0` (an intentional flush-photo override inside a
rounded card) — none of these are inconsistencies. It also does not touch the wider dead-code
footprint discovered alongside this audit — the original (still-tokenised) `.faed-product-card`
block, `.faed-product-grid`, `.faed-price--sm`, and the entire `.faed-category-card`/
`.faed-category-grid`/`.faed-hero__category-*`/`.faed-hero__promise-*` family are also
unreferenced by any current view, but none of them had a border-radius problem, so removing
them was out of scope for this pass and was left for the product owner to decide on
separately.

**Verification note:** confirmed by grep against the full, freshly-staged `Views`/`Areas`
tree (40 `.cshtml` files) that every deleted selector has zero references, and by a brace-
balance check on the edited `faed.css` (1603 open, 1603 close). Not verified: visual
rendering — no `dotnet build`/`dotnet run` was possible in this session.

---

## 7. Technical constraints

- ASP.NET Core MVC on .NET 10, **Razor views + Bootstrap 5 + vanilla JavaScript**
- **No frontend framework, no build step, no new NuGet packages**
- Custom layer lives in `wwwroot/css/faed.css` — extend its `--faed-*` variables with the
  values in §5.2 rather than inventing a parallel system
- Icons are inline SVG, stroke-based, one consistent style. **Never emoji**
- English only, no localisation framework

---

## 8. Images

**Sourcing:** free stock (Unsplash, Pexels) or CC-licensed photos from Wikimedia Commons /
StockSnap. Backgrounds are removed for product shots only — the demo pipeline
(`tools/demo-images/`) does this with the remove.bg API, not by hand, and composites the
cut-out onto `--faed-card-bg`. Search terms that work: `air fryer`, `microwave`,
`vacuum cleaner`, `power drill`, `blender`, `angle grinder`, `kettle`, `toaster` — prefer
shots already on white. Every source photo and its licence is listed in `docs/CREDITS.md`;
CC BY / CC BY-SA attribution lives there, not in the UI.

**Two rules — see §5.6 for the token-level implementation:**

1. **Do not cut out the defect photos.** Product shots are clean cut-outs on the grey card
   background. Defect shots must look like **phone photos with a real background**.
2. **Stock has no defect photos.** Six phone photos of any scuffed appliance or damaged box are
   enough for the whole demo, and need not match the products.

**Format and encoding.** Photographic assets ship as WebP where practical, sized for where
they actually render. No embedded text, logos or watermarks in any image; crops keep the
subject's focal point safe across breakpoints. Every image declares fixed dimensions or an
aspect ratio so the layout does not shift while it loads.

---

## 9. The remaining phases

| Phase | Work | Status |
|---|---|---|
| **11** | Design system (tokens) + mockups of the five public screens | **Complete — all five screens specified in `docs/DESIGN-BRIEF.md` §5.** Phase 11 in `docs/PHASE-PLAN.md` is now a mechanical CSS/token application, not a design task |
| **12** | Implement: public surface rebuilt to the mockups · workspaces token pass · an empty state for every screen · one responsive check | Not started |
| **13** | Demo readiness: real images in `DemoDataSeeder` · written demo script for the defence · full walkthrough of all three roles | **Done** — photography via `tools/demo-images/` (credits in `docs/CREDITS.md`); walkthrough in `docs/DEMO-SCRIPT.md` |

Before starting Phase 12: `git tag phase-11-complete` — a named rollback point, once the
remaining two screens are mocked and approved.

---

## 10. Two things that have gone wrong twice — watch for them

**Numbers in the docs are markers of intent, not the intent.** "Eight fields", "four filters",
"under 250 lines" describe a goal: one page, no jargon, few decisions. When a number collides
with the goal, **the goal wins** — say so and fix the doc rather than working around the number.

**Code carries undocumented decisions.** A rule that exists only as a comment on a file being
deleted disappears with it. Before removing anything with explanatory comments, ask whether it
holds a business rule the docs do not.

---

## 11. Accessibility baseline — recorded, not audited

§1 de-prioritises mobile and code hygiene; this is the small set that stays because a
panellist may keyboard through the demo. New or rebuilt screens are expected to keep:

- a working skip link,
- a visible keyboard focus ring,
- every input associated with its `label`,
- a sensible heading order,
- meaningful `alt` text on content images,
- `prefers-reduced-motion` respected,
- no critical information available on hover alone.

This records a standard; it does not authorise an accessibility audit. Do not sweep the
codebase against it. A violation noticed in passing is logged in `AUDIT-PHASE-14.md` and
fixed only if the fix is one line.

---

## Appendix — decision log, this session

| # | Decision | Resolution |
|---|---|---|
| 1 | Visual direction | Direction A, modified — not B, not a hybrid |
| 2 | Accent color | `#B23A00` (rust) — replaces the original `#E2610A` hot orange |
| 3 | Corner radius | `2px` — replaces the original `0px` |
| 4 | Color semantics | Ink for all actions; rust exclusively for disclosure; never mixed |
| 5 | Condition indicator | One placement only — photo-corner stamp. No duplicate badge |
| 6 | Description field (listing detail) | Full-width text section below the photo/price block |
| 7 | Merchant response rate — New Seller state | Neutral outline badge, same position as the response-rate line, no percentage ever shown alongside it |
| 8 | Product vs. defect photography | Product = cutout on grey card. Defect = real background, never cut out |
| 9 | Shop grid layout | Search + 3 filters inline, sort trailing-aligned and visually separate, 3-column card grid |
| 10 | Merchant storefront layout | Header block (identity, verified badge, response rate) · 2-filter row (no category) · 4-column grid |
| 11 | Identity avatar shape | Square, `--faed-radius`, ink fill — never a circle. Fixes an inconsistency this session introduced before it was written down. **Reversed 11 Sep, see §5.0/§5.9** |

## Appendix — decision log, 11 September revision (§5.0)

| # | Decision | Resolution |
|---|---|---|
| 12 | Direction A's flat/zero-shadow treatment | Rejected by product owner on review of the built home page — read as dated, no hero, no site introduction |
| 13 | Public-surface shape tokens | New `--fx-radius-sm/`-`/`-`lg`/`-pill` and `--fx-shadow-xs/sm/md/lg` tokens, additive only — `--faed-radius*` unchanged, workspace/admin/identity untouched |
| 14 | Home hero | Added: eyebrow, headline, tagline, two CTAs, three-fact trust strip, decorative photo-tile + condition-stamp showcase (no stock photo) |
| 15 | Identity avatar shape | Reverses decision #11 — circle, not square, on the five public screens only |
| 16 | `fx-price__off` colour | Rust, not muted grey — a discount percentage is disclosure information under §5.3's existing test |
| 17 | `faed-home-hero.webp` | Left on disk, unreferenced — it is fashion-sector stock (shoes/handbag/clothing), wrong for this sector; not wired in, not deleted (out of scope here) |

## Appendix — decision log, Phase 15 revision (11 September 2026, §5.13)

| # | Decision | Resolution |
|---|---|---|
| 18 | Header component | One `faed-navbar` on every route, replacing the public-only `fx-site-header`. Content (links/search/account state) still varies by auth/role, never by area |
| 19 | Navbar colour scheme | Left unchanged everywhere — this revision unifies structure only, not the navbar's own green hover/active palette |
| 20 | §5.2 ink/paper/card-bg/rust/text-muted values | Corrected — first scoped to `body.faed-public-page, body.faed-auth-page` only; promoted sitewide in §5.14/decision #29 the same day |
| 21 | New `--faed-rust-text` token (`#815a1d`) | Added for the two places rust is small text or a text-bearing background, not an icon/border/fill — `--faed-rust` alone (`#a67425`) does not clear 4.5:1 AA there |
| 22 | Card aspect ratio | `.fx-photo` `1/1` → `4/3` — shortens the card without narrowing it, correcting "too narrow, too tall" feedback on §5.0's cards |
| 23 | Card grid columns | Fixed 3/4-column grids merged into `repeat(auto-fill, minmax(240px, 1fr))` — restores a comfortable minimum card width while still adding columns on wide viewports |
| 24 | Hero decorative visual | Single hung inspection-tag card over a blueprint grid, replacing a blurred-blob-behind-two-offset-cards collage flagged as a generic/AI-templated pattern |
| 25 | Login/Register split-screen | No new layout built — the existing `.faed-auth-layout` two-panel grid already satisfied the structural request. Only `.faed-auth-context`'s background (gradient blobs → ink + blueprint grid) changed |
| 26 | Literal photo in the auth aside | Not added — no real photo asset exists yet; the aside's generic-gradient background was treated as the actual defect and fixed instead. Photo insertion point left open for when an asset exists |
| 27 | Condition stamp shape | `clip-path` tag silhouette + punched-hole `::before`, pure CSS on the existing `.fx-stamp` markup — no `ConditionStamp.cs` change |
| 28 | Build/test verification (§5.13) | Not run in the session that made those edits — no .NET SDK or local shell access from that environment. Manual review only |

## Appendix — decision log, Phase 16 revision (11 September 2026, §5.14)

| # | Decision | Resolution |
|---|---|---|
| 29 | §5.2/§5.13 token scope | Promoted from `body.faed-public-page, body.faed-auth-page` to the shared `:root` — every screen, including workspace/merchant/admin, now inherits the same ink/paper/border/rust identity |
| 30 | Workspace/merchant/admin/buyer component-level hex values | Left untouched — 173 distinct literal hex values (302 occurrences) found sitewide in `faed.css`, the majority a pre-existing, hand-tuned micro-palette unrelated to the token system. Rewriting it would violate §5.12's explicit "do not audit or refactor" lock on this file and could not be visually verified in this session |
| 31 | Navbar hover/active accent | Left unchanged again — reaffirms decision #19, not revisited by the sitewide token promotion |
| 32 | Real hero/auth photography | Still not added — no image-generation tool available, and sourcing a third-party photo raises a licensing question outside what a session should decide unilaterally. Remains a manual step for the product owner |

## Appendix — decision log, Phase 17 revision (11 September 2026, §5.15)

| # | Decision | Resolution |
|---|---|---|
| 33 | Hero visual subject | Changed from one styled listing (photo + fake price + grade stamp) to a wider "trade counter" scene meant to hold several tagged items — the concept, not a product |
| 34 | Inspection-tag motif in the hero | Kept, rescaled — one small hole-punched tag pinned to the photo's corner, not the entire visual |
| 35 | Fallback content before a real photo exists | Three existing `CategoryGlyph` icons (cleaning/power-tools/kitchen) spread across the panel, not an empty box or a single icon |
| 36 | Hero photo licensing | Not sourced by this session — no image-generation tool available; an AI-image-tool prompt was given to the product owner instead, matching the site's actual token colours |

## Appendix — decision log, Phase 18 revision (11 September 2026, §5.16)

| # | Decision | Resolution |
|---|---|---|
| 37 | Navbar hover/active tint | Fixed — three `rgba(13,105,87,…)` literals (leftover `#0D6957` fashion-era teal) on nav-link/navbar-action/account-menu hover replaced with `rgba(30,43,40,…)`, the new ink's RGB, at the same opacities |
| 38 | Same leftover literal on primary-button shadows | Fixed on request, same day — `.faed-btn--primary` and the auth-form submit button's glow shadow also moved from `rgba(13,105,87,…)` to `rgba(30,43,40,…)`. No `rgba(13, 105, 87` or `#0D6957` literal remains anywhere in `faed.css` outside historical comments |

## Appendix — decision log, Phase 19 revision (11 September 2026, §5.17)

| # | Decision | Resolution |
|---|---|---|
| 39 | The `--faed-radius*` vs `--fx-radius*` split itself | Left untouched — confirmed as a pre-existing §5.0 decision, not a bug, and confirmed with zero cross-contamination between the two token families |
| 40 | 9 literal-radius selectors found to be dead code | Deleted rather than re-tokenised, since fixing a value nothing renders is cosmetic only — each replaced with a one-line comment pointing to §5.17 |
| 41 | 3 literal-radius selectors found to be live | Tokenised: `.faed-thumb__remove` → `var(--faed-radius)`, `.faed-admin-filterbar a` → `var(--faed-radius-sm)` (calc() removed), `.faed-auth-layout` → `clamp(var(--fx-radius), 2vw, var(--fx-radius-lg))` |
| 42 | Wider dead-code footprint (`.faed-product-card` base block, `.faed-category-card*`, `.faed-hero__category-*`/`__promise-*`, etc.) | Left in place — none of it had a border-radius problem, so removing it was out of scope for this pass |
