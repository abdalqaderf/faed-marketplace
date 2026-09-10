# Faed — Design Brief

> **Status: decided and locked — 10 September 2026.**
> This supersedes the earlier "Direction A vs Direction B" open question. Everything in
> §5 below is final. Do not reopen it without a new session.

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

**Square, `--faed-radius` (2px), ink fill, paper-colored initials — never a circle.** This was
inconsistent across this session's own mockups (the listing-detail merchant mini-card used a
circular avatar before this was written down) and is now fixed as a single rule: a circle
belongs to Direction B's soft-shelf language, not this one. Applies everywhere a merchant
identity renders — order page, listing detail, storefront header, admin merchant list.



| Surface | Treatment | Status |
|---|---|---|
| **Home** | Full redesign | Mocked (this session) |
| **Listing detail** | Full redesign | Mocked (this session) |
| **Reserve** | Full redesign | Mocked (this session) |
| **Shop (browse/filter grid)** | Full redesign | Mocked (this session) — see §5.7 |
| **Merchant storefront** | Full redesign | Mocked (this session) — see §5.8 |
| Merchant workspace · Admin workspace | Token pass only — colours, spacing, type. Do not rebuild them | Tokens defined (§5.2), not applied |
| `faed.css` (8,165 lines) | Do not audit or refactor. Invisible to the panel, and it works | — |

All five public screens are now fully specified in writing. Phase 11 has no remaining design
decisions — only §5.9's avatar-shape fix needs applying to any view where it was already
inconsistent.

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
| 11 | Identity avatar shape | Square, `--faed-radius`, ink fill — never a circle. Fixes an inconsistency this session introduced before it was written down |
