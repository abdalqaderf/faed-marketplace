# Faed — Final UI/UX Production Polish Plan

**Status:** FINAL UI/UX PHASE  
**Previous phases:** `DONE`  
**Project state:** Production hosting, SQL Server, migrations, Cloudflare R2, Brevo email, Identity, Merchant/Admin functionality, and prior redesign phases are treated as complete and must not be reopened unless a real regression is discovered.  
**Primary source of truth:** The latest current Faed codebase.  
**Scope:** Presentation, usability, accessibility, responsiveness, visual consistency, and final production polish only.

---

# 1. Mission

Perform one final, source-backed UI/UX audit of the **current** Faed application and improve every remaining presentation issue that makes the product feel oversized, visually inconsistent, confusing, default-looking, inefficient, or less polished than a modern production marketplace.

The final result should feel like:

> **A warm, premium, trustworthy marketplace for buyers + a clear operational workspace for merchants and admins.**

Faed V1 currently focuses on fashion-related inventory, but the brand and interface must remain structurally ready for future product categories without redesigning the identity from scratch.

The guiding principle is:

> **Reduce visual and cognitive complexity without reducing system capability.**

---

# 2. Non-Negotiable Safety Boundaries

This phase must **not intentionally change**:

- Database schema
- Entity models
- Migrations
- `ApplicationDbContext`
- Existing business rules
- Existing service behavior
- Existing controllers/actions unless a strictly presentation-safe adjustment is unavoidable
- Routes
- Authentication behavior
- Authorization policies
- Roles
- Merchant verification logic
- Listing moderation logic
- Inventory logic
- B2C/B2B transaction logic
- Checkout/order logic
- Dispute/review logic
- Existing search semantics
- Existing filtering semantics
- Existing sorting semantics
- Existing pagination semantics
- Cloudflare R2 integration
- Brevo integration
- SQL Server production configuration
- Production environment variables
- Bootstrap admin behavior
- Hosting/deployment configuration

Do **not** create a migration during this phase.

If a UI improvement appears to require a backend/schema/business-rule change, **stop and report it before implementing it**.

---

# 3. Technology Constraints

Continue using the existing stack:

- ASP.NET Core MVC / Razor
- Bootstrap
- Existing CSS
- Existing vanilla JavaScript
- Existing Faed shared partials/components

Do not introduce:

- React
- Vue
- Angular
- Tailwind
- Another UI framework
- Large animation libraries
- Unnecessary JavaScript dependencies

Prefer shared Razor/CSS improvements over page-specific duplication.

---

# 4. Source-Backed Audit Before Editing

Before changing code, inspect the **latest project** rather than relying only on old task documents.

Audit at minimum:

- `Views/Shared/_Layout.cshtml`
- `Views/Shared/_LoginPartial.cshtml`
- Shared product/listing cards
- Shared pagination
- Shop browse/filter partials
- Home page
- Listing details
- Store page
- Buyer pages
- Merchant workspace/layout/sidebar
- Admin workspace/layout/sidebar
- Identity pages
- `wwwroot/css/faed.css`
- `wwwroot/css/site.css`
- Relevant JavaScript
- Current responsive breakpoints
- Current role-aware navigation
- Existing Cart destination/count behavior
- All paginated list/grid pages
- All long forms
- All table-heavy pages
- All empty/error/success states

For every issue, classify it as:

1. **Must fix** — materially harms UX or visual quality.
2. **Should fix** — meaningful polish improvement.
3. **Leave as-is** — current implementation is already appropriate.

Do not rewrite working UI simply for the sake of changing it.

---

# 5. Final Visual Direction

Maintain the established Faed identity:

- Deep teal as the primary brand color
- Warm ivory / soft cream backgrounds
- White/light surfaces
- Dark charcoal/deep teal text
- Restrained warm sand accents
- Controlled borders
- Limited subtle shadows
- Consistent radius scale
- Premium but approachable imagery
- Generous whitespace in public marketplace pages
- Higher information density in Merchant/Admin workspaces

Avoid:

- Excessive gradients
- Glassmorphism everywhere
- Large decorative shadows
- Giant cards
- Giant empty spacing
- Overly tall sections
- Excessively rounded components
- Borders around every element
- Multiple icon styles
- Decorative animations
- Permanent fashion-only branding
- UI elements that look like default ASP.NET scaffolding

---

# 6. FINAL NAVBAR — HIGHEST PRIORITY

The navbar must receive the strongest UX improvement in this phase.

## 6.1 Core principles

The final navbar must be:

- Sticky
- Compact
- Search-first
- Role-aware
- Easy to scan
- Keyboard accessible
- Mobile friendly
- Free of duplicated links
- Free of inaccessible links for the current role
- Able to support future marketplace categories

Target desktop navbar height:

```text
~64–72px
```

Do not let the navbar consume unnecessary vertical space.

Use a subtle border/elevation change on scroll only if it improves separation.

---

## 6.2 Desktop information architecture

Preferred hierarchy:

```text
[Faed]   [Shop/Browse] [Categories*]     [Search marketplace................]     [Role/Workspace] [Cart] [Account]
```

`* Categories` should only be a direct navbar control if it can use existing category routes/data without adding backend complexity. Otherwise keep discovery inside Shop and do not invent new data plumbing.

### Home link

The Faed logo already returns to Home. On desktop, consider removing a redundant text `Home` link if doing so makes the navbar cleaner. Home can remain in the mobile menu if useful.

### Search

Search should be a visually central utility:

- Use the existing Shop search/query behavior.
- Preserve the existing query parameter semantics.
- Do not create a second search implementation.
- Give the search field enough width on desktop.
- Use concise placeholder text such as `Search products` or `Search marketplace`.
- Keep submit/search button obvious but compact.
- Preserve focus visibility.

Target search behavior:

- Flexible width
- Rough maximum around `520–640px` on large desktop
- Must gracefully shrink on smaller laptop widths

---

## 6.3 Cart — direct navbar access

**Move the existing Cart access out of the Account dropdown and place it directly in the main navbar for users who can buy.**

Rules:

- Do not leave a duplicate Cart link inside the Account menu.
- Use a recognizable cart/bag icon plus accessible text/label.
- If an existing real cart count is available, show it as a compact badge.
- If no count is already supported, do **not** invent a fake count or new backend query.
- Use the existing Cart route/action only.
- Ensure the control has at least a ~44px touch target.
- `aria-label` should communicate Cart and count when applicable.

If the latest source reveals that there is no persistent Cart feature and only direct Checkout exists, do **not** fabricate a Cart feature. Report that mismatch before changing behavior.

---

## 6.4 Role-aware navbar matrix

### Guest

Preferred desktop structure:

```text
Faed | Shop/Browse | Search | Sign in | Create account
```

- `Create account` may be the primary compact CTA.
- `Sign in` should remain lower visual weight.

### Buyer

Preferred structure:

```text
Faed | Shop/Browse | Search | Cart | Account
```

Account menu may contain:

- My Orders
- My Disputes
- Account settings
- Merchant onboarding/merchant path only if it is currently a valid route
- Sign out

Do not bury Cart inside this menu.

### Merchant

Merchants can also participate in marketplace buying where existing authorization allows it.

Preferred structure:

```text
Faed | Shop/Browse | Search | Merchant Center | Cart | Account
```

`Merchant Center` should become a clear workspace switcher/entry point rather than being hidden deep inside the user menu.

Account menu should focus on personal/account actions rather than duplicating the full Merchant navigation.

### Admin

Preferred structure:

```text
Faed | Marketplace access as appropriate | Admin Workspace | Account
```

- No Cart if Admin cannot buy under existing rules.
- `Admin Workspace` should be clear and easy to reach.
- Do not pollute the public navbar with Admin operational links.

---

## 6.5 Account menu

Improve the current account dropdown:

- Show the user’s actual display name/full name if already available in the current model/view context.
- Avoid displaying the raw email as the main label unless necessary.
- Avatar can use initials.
- Truncate safely for long names.
- Group links by purpose.
- Keep `Sign out` separated at the bottom.
- Do not repeat workspace navigation that already has a direct top-level entry.
- Do not repeat Cart.
- Maintain keyboard and screen-reader behavior.

Recommended grouping:

```text
Account identity
----------------
Orders / Disputes (buyer-capable users)
Account settings
----------------
Sign out
```

Workspace access should preferably live outside this personal menu.

---

## 6.6 Mobile navbar

Do not simply compress the desktop navbar until it overflows.

Preferred mobile structure:

```text
Row 1: [Faed]                         [Cart] [Account] [Menu]
Row 2: [Search marketplace...............................]
```

or another equally compact pattern if the latest markup supports it more cleanly.

Requirements:

- Search remains easy to access.
- No horizontal overflow.
- Cart remains one tap away.
- Menu uses an accessible Bootstrap collapse/offcanvas pattern.
- Role/workspace links remain clear.
- Minimum touch target around 44px.
- Collapsed menu closes predictably after navigation.
- Long user names do not break layout.
- Sticky mobile header must not cover page content.

---

# 7. HERO — REDUCE HEIGHT AND IMPROVE FIRST-VIEWPORT UX

The current experience should no longer allow the hero to feel like **one and a half screens**.

The hero should remain cinematic, but product/category discovery must begin quickly.

## 7.1 Desktop sizing target

Target direction, adjusted to the actual layout:

```text
Large desktop: ~68–78svh, generally capped around 680–720px
Laptop:        ~58–70svh, generally capped around 580–640px
Tablet:        content-driven / auto height
Mobile:        content-driven / auto height, never forced to 100vh
```

Avoid using a huge `vw`-driven `min-height` that becomes disproportionately tall.

### Critical acceptance rule

At common laptop/desktop sizes, the first viewport should expose at least the **start of the category/discovery section below the hero**.

The user should understand within the first screen:

1. What Faed is.
2. What to do next.
3. That there is marketplace content below.

---

## 7.2 Hero content density

Keep:

- Category-neutral headline
- Short supporting copy
- Primary marketplace CTA
- Secondary Merchant CTA
- A small number of useful trust signals
- Cinematic image

Reduce or simplify:

- Excessively large vertical padding
- Oversized title
- Too many stacked informational blocks
- Decorative overlays that compete with the product image
- Large gaps between hero elements

Preferred headline direction remains category-neutral:

> **Good stock deserves another route to market.**

Do not permanently position Faed as a clothing-only brand.

---

## 7.3 Hero responsive behavior

- Desktop can retain split text/image composition.
- Tablet/mobile may stack.
- Keep image focal point meaningful.
- Never let overlay cards cover the primary subject or CTA.
- Avoid a giant image-only block after the text on mobile.
- Use `svh/dvh` carefully where useful, but prefer content-driven mobile sizing.

---

# 8. PRODUCT CARDS — REDUCE OVERSIZING

Product cards should no longer feel close to the height of an entire screen.

The marketplace should feel browsable and information-dense enough to compare multiple products quickly.

## 8.1 Size and density goals

Reduce card visual height approximately **20–30% where the current implementation is oversized**, while preserving readability and product photography quality.

Preferred general direction:

- Wide desktop: usually 4 cards per row where container width allows.
- Laptop: 3–4 cards depending width.
- Tablet: 2–3 cards.
- Mobile: usually 2 cards if content remains readable; fall back to 1 only when necessary.

Do not force a card to `100vh` or any viewport-like height.

Use a controlled image ratio such as:

```css
aspect-ratio: 4 / 5;
```

or a slightly shorter ratio if the current imagery works better.

The actual final ratio should be validated against real Faed listing images.

---

## 8.2 Card information hierarchy

Prioritize:

1. Product image
2. Title
3. Current price / reference discount price where supported
4. Condition
5. Merchant/trust context
6. Availability/channel only when useful

Reduce or remove from card surface when redundant:

- Repeated explanatory metadata
- Large text blocks
- Separate `View` footer when the card already has a clear large click target
- Excessive badges
- Empty fixed-height metadata rows

Do not remove information if it is important for buyer trust; instead make it compact.

---

## 8.3 Card interaction

- Make the primary destination clear.
- Large click target where safe.
- Maintain keyboard focus.
- Use subtle lift/image zoom only.
- Avoid dramatic animation.
- Keep equal visual rhythm without forcing huge empty card bodies.

---

# 9. CATEGORY CARDS

Category cards should support discovery without becoming oversized hero-like panels.

For current V1 categories:

- Clothing
- Shoes
- Bags & Accessories

Requirements:

- Entire card clickable.
- Strong photography.
- Text overlay remains readable.
- Keep current counts only if already available.
- Use compact card height that allows the full category row to be understood quickly.
- Preserve responsive cropping.
- Component naming/copy must remain ready for future non-fashion categories.

Preferred desktop visual height is roughly in the `180–260px` range depending grid width, not a screen-sized tile.

---

# 10. HOME PAGE FLOW

Final hierarchy should feel fast and intentional:

```text
Sticky navbar
↓
Compact cinematic hero
↓
Categories / discovery bridge
↓
Featured/current listings
↓
Trust / how Faed works
↓
Transparency / condition explanation if still useful
↓
Merchant CTA
↓
Footer
```

Improve:

- Section spacing
- Section header hierarchy
- CTA priority
- Repetition
- Visual rhythm
- Card density

Avoid making every section vertically huge.

A premium marketplace does not require excessive whitespace; it requires **controlled whitespace**.

---

# 11. AUTHENTICATION UI — FINAL POLISH

Identity pages must look like part of Faed, not default ASP.NET Identity.

Review all reachable pages:

- Login
- Register
- Confirm Email
- Forgot Password
- Reset Password
- Access Denied
- Account settings/manage pages

## 11.1 Register form

The current production registration fields are expected to include:

- First Name
- Last Name
- Phone Number
- Email
- Password
- Confirm Password

Use the actual latest source as truth.

### Required UX improvements

- Remove floating-label duplication/confusion.
- Prefer conventional `label` above input where it improves clarity.
- Do not repeat label text as a distracting placeholder.
- First Name + Last Name may share a row on desktop.
- All fields become single-column on mobile.
- Phone number should have clear formatting/help if already supported.
- Password requirements should be readable but concise.
- Validation should appear directly beside/below the relevant field.
- Validation summary should not visually dominate unless necessary.
- Submit button hierarchy must be obvious.

## 11.2 Auth layout proportions

The decorative/context panel must not compete with the form.

Preferred direction:

- Form/content: ~58–65%
- Context/visual panel: ~35–42%
- Form max width: roughly `520–600px` for Register
- Login form can be narrower
- Hide or significantly simplify the context panel below laptop/tablet breakpoints if needed
- Mobile should be clean single-column

Do not let auth pages become unnecessarily tall because of the side panel.

---

# 12. SHOP / SEARCH / FILTERS

Preserve the approved interaction:

> **Quick Filters + More Filters Drawer**

Do not return to a large permanent filter sidebar as the primary experience.

## 12.1 Quick controls

Prioritize:

- Search
- Category
- Condition
- Price
- More Filters
- Sort as a separate clear control

## 12.2 More Filters drawer

Expose all existing filters, including where currently supported:

- Discount reason
- Brand
- Size
- Colour
- Availability/channel
- Min/max price
- Other existing filter fields

Requirements:

- Current selections visible
- Clear all
- Apply/show-results action
- Accessible close action
- Proper focus behavior
- Mobile full-width/offcanvas treatment where appropriate

## 12.3 Active filter chips

- Show current active filters compactly.
- Removing one must preserve all others.
- Preserve existing query semantics.
- Do not generate duplicate query parameters accidentally.

## 12.4 Result context

Keep visible:

- Result count
- Current search context
- Sort
- Active filters

Differentiate:

- No inventory exists
- No results match current filters

Filtered zero-results state must provide an obvious reset path.

---

# 13. PAGINATION — REQUIRED EVERYWHERE IT ALREADY EXISTS

Pagination remains mandatory and must not be replaced by infinite scroll.

Desktop target direction:

```text
Showing 21–40 of 186 results        ‹ Previous   1  2  3  4  5   Next ›
```

Mobile target:

```text
‹ Previous        Page 2 of 10        Next ›
```

Preserve:

- Search query
- Filters
- Sort
- Route values
- Correct current page
- Correct total count

Audit all existing paginated pages, including Public, Buyer, Merchant, and Admin lists.

Prefer a shared pagination partial/style.

---

# 14. LISTING DETAILS

Make the product decision flow immediately understandable.

Desktop priority:

```text
Gallery                    Decision column
                           Title
                           Merchant/trust
                           Price
                           Condition
                           Discount reason
                           Variant/options
                           Quantity
                           Fulfillment
                           Primary action
```

Requirements:

- Gallery thumbnails clear and compact.
- Selected image state obvious.
- Defect photos remain trust-critical and visible.
- No oversized gallery that pushes all purchase information below the fold unnecessarily.
- Primary price/action should be visible without excessive scrolling on normal laptop screens.
- Preserve all variant/quantity/order logic.
- Do not hide condition or discount reason.

---

# 15. CART / CHECKOUT / BUYER EXPERIENCE

If a persistent Cart exists in the latest source, polish it consistently with the new navbar entry.

Cart requirements:

- Compact product rows/cards
- Clear quantity controls
- Clear remove action
- Price/total hierarchy
- Strong checkout CTA
- Useful empty-cart state
- Mobile-friendly layout

Checkout requirements:

- Clear sequential form rhythm
- Product/order summary visible
- Fulfillment information easy to understand
- Validation close to fields
- Total and primary action visually clear
- Preserve the existing checkout/order workflow exactly

Buyer Orders / Disputes:

- Strong status visibility
- Important information first
- Pagination consistent
- Mobile layout usable
- Details pages make current status and next action obvious

---

# 16. MERCHANT WORKSPACE

Keep the approved **Sidebar** architecture.

Do not return to long horizontal navigation.

## 16.1 Sidebar goals

- Compact
- Clear grouping
- Visible active state
- Icon usage consistent
- Labels concise
- Collapse/drawer on mobile/tablet
- No links the Merchant cannot actually use

Suggested grouping based on existing routes:

```text
Overview / Verification
Sell
  Listings
  Inventory
Commerce
  B2C Orders
  B2B Offers
  B2B Deals
Customers / Resolution
  Reviews
  Disputes
Insights
  Analytics
Settings
  Store Settings
```

Use actual current routes and labels as the source of truth.

## 16.2 Merchant pages

Polish:

- Verification
- Listings
- Create/Edit listing
- Inventory
- B2C Orders
- B2B Offers
- B2B Deals
- Disputes
- Reviews
- Analytics
- Store Settings

Focus on:

- Status-first scanning
- Clear primary actions
- Compact tables
- Grouped long forms
- Strong empty states
- Pagination
- Consistent status badges
- Responsive action placement

Do not decorate operational pages like marketing pages.

---

# 17. ADMIN WORKSPACE

Keep the approved **Sidebar** architecture and make it denser than public pages.

Suggested grouping:

```text
Overview
Marketplace / Moderation
  Merchant Verification
  Listing Moderation
  Catalog
Transactions / Operations
  Orders
  B2B Deals
  Disputes
Trust / Governance
  Reviews
  Audit Log
```

Use current routes/features only.

Polish:

- Dashboard
- Merchant verification
- Listing moderation
- Orders
- B2B deals
- Disputes
- Catalog
- Reviews
- Audit log

Prioritize:

- Queue scanning
- Status clarity
- Clear review actions
- Compact table density
- Safe destructive/decision controls
- Pagination
- Responsive overflow handling

Avoid decorative charts or fake KPIs.

---

# 18. TABLES AND LISTS

Desktop:

- Clear headers
- Consistent row height
- Numeric alignment
- Status badges
- Action grouping
- No unnecessary columns

Mobile/tablet:

Use the least disruptive approach:

- Controlled horizontal scroll, or
- Responsive stacked rows/cards when semantics remain clear

Do not hide critical data/actions simply to make a table fit.

---

# 19. FORMS

Create one consistent form language across Faed.

Standardize:

- Labels
- Required indicators
- Help text
- Input heights
- Selects
- Textareas
- File uploads
- Validation
- Error summaries
- Disabled states
- Submit/cancel actions
- Destructive actions

Long forms must be visually grouped, e.g.:

```text
Basic information
Pricing
Product details
Inventory
Media
Publication/status
```

Preserve:

- Input names
- Tag Helpers
- Validation
- Hidden fields
- Anti-forgery
- POST targets
- Business-meaningful order of fields

---

# 20. BUTTON HIERARCHY

Audit every action group.

Use consistent meanings for:

- Primary
- Secondary
- Ghost/link
- Destructive
- Icon-only
- Disabled

Rules:

- One visually dominant primary action per task group.
- Destructive actions must not look like normal primary actions.
- Do not show several same-weight buttons competing for attention.
- Icon-only buttons must have accessible names/tooltips where appropriate.

---

# 20A. ICONOGRAPHY & ACTION SEMANTICS — FINAL SYSTEM

Perform a source-backed icon audit across the entire application. Icons must improve recognition and scanning; they must not become decoration or visual noise.

## 20A.1 Library rule

- First identify the icon library already used by the latest Faed codebase.
- Reuse that existing library consistently.
- Do not introduce a second icon library merely to obtain one missing icon.
- Do not mix unrelated outline/solid icon families without a deliberate reason.
- Do not use emoji or arbitrary Unicode symbols as production UI icons when a proper existing icon is available.
- If the project has no suitable icon system at all, report the gap before adding a dependency. Prefer the smallest production-safe option that integrates with the current Bootstrap/Razor stack.

## 20A.2 Semantic icon mapping

Choose the closest semantically obvious icon already available in the existing library. Preferred concepts include:

```text
Cart / Basket      → shopping cart or shopping bag
Search             → magnifying glass
Account / Profile  → user/person
Orders             → receipt, box, or package
Wishlist           → heart (only if Wishlist exists)
Merchant Center    → storefront/shop
Admin Workspace    → shield, gauge, or admin/dashboard concept
Dashboard          → grid/gauge
Listings           → tag/card/listing concept
Inventory          → boxes/package/warehouse
B2B Offers/Deals   → handshake/arrows/document as supported
Verification       → badge/check/shield-check
Reviews            → star/comment
Disputes           → alert-circle/flag/message warning
Analytics          → chart
Settings           → gear
Filters            → sliders/funnel
Sort               → sort arrows
Add/Create         → plus
Edit               → pencil
Delete             → trash
View/Details       → eye or chevron only when context is obvious
Download           → download arrow
Upload             → upload arrow
Close              → x/close
Back               → arrow-left matching reading direction/context
External link      → external-link
Sign out           → sign-out/log-out
Success            → check-circle
Warning            → warning triangle/circle
Error              → x-circle/alert-circle
Information        → info-circle
```

Use actual current features only. Do not add controls merely because an icon exists.

## 20A.3 Navbar icon rules

The navbar must use icons strategically rather than iconizing every link.

Required direction:

- `Cart` gets a highly recognizable cart/bag icon and remains directly visible for buyer-capable users.
- If a real cart count already exists, attach a compact badge to the Cart control; never fake a count.
- Account uses a user/avatar treatment; initials are acceptable if already supported cleanly.
- Search uses a magnifying-glass icon in or beside the search action without reducing text-field clarity.
- Merchant Center may use a storefront icon.
- Admin Workspace may use an admin/shield/dashboard icon.
- Mobile menu uses a standard menu/hamburger icon with an accessible name.
- Do not hide important desktop actions behind ambiguous icon-only buttons simply to make the navbar smaller.

## 20A.4 Sidebar icon rules

Merchant/Admin sidebars should use one consistent icon per major destination where it improves scanning.

- Keep icon position and width aligned vertically.
- Active state must remain obvious from label/background/border treatment, not icon color alone.
- Icons must not replace labels for primary workspace navigation.
- Do not give every nested item an icon if this makes the sidebar noisy.
- Workspace switchers should be visually distinct from normal page destinations.

## 20A.5 Size, alignment, and visual weight

Use a small consistent size system based on the existing icon set, approximately:

```text
Inline/form icon:       ~16–18px
Navbar/sidebar icon:    ~18–20px
Primary action icon:    ~18–20px
Empty-state icon:       larger only when it genuinely aids recognition
```

Exact values should follow the current typography and icon library.

Requirements:

- Align icons optically with adjacent text.
- Keep consistent gap between icon and label.
- Avoid oversized icons inside compact controls.
- Avoid inconsistent stroke/filled weight on the same surface.
- Icons must not make buttons taller than the global control-height system without reason.

## 20A.6 Icon-only controls

Use icon-only controls only for universally recognizable, low-ambiguity actions and where space materially benefits, such as:

- Close
- Search submit in a clearly labelled search field
- Previous/next gallery controls
- Compact table actions where context is explicit

For icon-only controls:

- Provide an accessible name via visible context, `aria-label`, or equivalent native semantics.
- Add a tooltip/title only when it materially improves discoverability; do not use tooltips as a replacement for accessible names.
- Maintain at least ~44px touch target on touch-first/mobile surfaces when practical.
- Provide visible hover, focus-visible, active, and disabled states.

Important or destructive actions should retain text when ambiguity or risk exists.

## 20A.7 Status icons

Status icons may reinforce, but never replace, text/badge meaning.

Correct pattern:

```text
[✓] Approved
[!] Needs review
[×] Rejected
```

The textual status remains authoritative so color/icon interpretation is never required.

## 20A.8 Action hierarchy and icon placement

- Place icons before labels for most action buttons where it matches the interface reading flow.
- Keep chevrons/arrows at the trailing edge when they indicate navigation or expansion.
- Avoid combining a leading icon, trailing icon, badge, and long label in one compact control unless all are necessary.
- Destructive actions must remain clearly destructive through wording and button treatment, not a trash icon alone.

## 20A.9 Hover, focus, active, and motion behavior

Every interactive icon/button must have coherent states:

- Default
- Hover
- `:focus-visible`
- Active/pressed
- Disabled where applicable

Keep icon animation minimal. Acceptable examples:

- Small color/opacity transition
- Subtle translate/scale no larger than necessary
- Chevron rotation for an expanded disclosure

Do not use bouncing, spinning, pulsing, or decorative icon animation for normal navigation/actions. Respect `prefers-reduced-motion`.

## 20A.10 Iconography acceptance criteria

Iconography is complete only when:

- One coherent icon family is used throughout the touched UI.
- Cart has a clear direct navbar icon.
- Merchant/Admin workspace destinations scan quickly.
- No important action becomes ambiguous because its text was removed.
- Icon-only controls have accessible names.
- Hover/focus states are visible.
- No emoji/random glyphs remain where proper interface icons should be used.
- Status meaning never depends on an icon or color alone.
- Icon sizing/alignment is visually consistent across navbar, sidebars, buttons, forms, tables, and states.

---

# 21. FEEDBACK / EMPTY / ERROR STATES

Unify presentation for:

- Success
- Error
- Warning
- Information
- Empty state
- No search results
- Unauthorized
- Not found
- Validation
- Pending states

Do not invent loading states if the current architecture does not use them.

Messages should tell the user:

1. What happened.
2. What they can do next.

---

# 22. FOOTER

Keep the footer concise.

Improve:

- Marketplace positioning
- Information hierarchy
- Responsive columns
- Spacing
- Typography
- Useful links only

Remove permanent clothing-only branding.

Do not turn the footer into a large sitemap unless the current product actually needs it.

---

# 23. GLOBAL SPACING AND DENSITY PASS

This phase must explicitly search for oversized layout values.

Audit:

- `min-height`
- `height`
- `vh/svh/dvh`
- large `clamp()` values
- vertical padding
- section margins
- card media height
- giant empty states
- wide gaps in grids/forms

Specific goal:

> Faed should feel spacious, not stretched.

Reduce oversized spacing when it prevents users from seeing useful information within the first viewport.

---

# 24. DESIGN TOKENS / CSS CLEANLINESS

Do not add another random override layer to `faed.css`.

Prefer a coherent final hierarchy:

```text
1. Design tokens
2. Base/typography
3. Global shell
4. Navbar
5. Buttons
6. Forms
7. Badges/status
8. Alerts/states
9. Cards
10. Marketplace
11. Hero/home
12. Shop/filters
13. Product details
14. Buyer
15. Workspace shell
16. Merchant
17. Admin
18. Identity
19. Utilities
20. Responsive/accessibility
```

Rules:

- Consolidate duplicate selectors only when safe.
- Remove obsolete styles only after proving they are unused.
- Avoid `!important` unless genuinely required.
- Prefer tokens over repeated hard-coded colors/radii/spaces.
- Keep diffs explainable.

---

# 25. RESPONSIVE FINAL PASS

Test at minimum:

```text
320–375px    Small mobile
390–430px    Typical mobile
768px        Tablet
1024–1280px  Laptop/small desktop
1440px+      Wide desktop
```

Check every major surface for:

- No accidental horizontal overflow
- Navbar behavior
- Search width
- Hero height/crop
- Category card size
- Product grid density
- Product card height
- Filters/drawer
- Pagination
- Auth forms
- Tables
- Sidebars
- Modals/offcanvas
- Listing details
- Cart/checkout
- Footer

The interface must not simply scale everything down proportionally; prioritize and reflow content intentionally.

---

# 26. ACCESSIBILITY FINAL PASS

Minimum requirements:

- Skip link remains functional
- Semantic heading hierarchy
- Proper label/input associations
- Visible keyboard focus
- Accessible dropdown/offcanvas controls
- Current page state exposed accessibly
- Icon-only controls have names
- Color is not the only status signal
- Sufficient contrast
- Meaningful image alt text
- Decorative images have empty alt or CSS background treatment
- Touch targets are large enough
- Reduced-motion preference respected
- No hover-only critical information
- Form validation announced/readable

Use native HTML semantics before adding unnecessary ARIA.

---

# 27. IMAGE / MEDIA POLISH

Preserve the established premium photography direction.

Use WebP for photographic assets where practical.

For Hero/category images:

- No embedded text
- No logos/watermarks
- Responsive focal-point-safe cropping
- Avoid imagery that permanently defines Faed as fashion-only
- V1 fashion products are acceptable because they represent current inventory

Do not create fake product listings, merchants, reviews, or operational data for visual decoration.

---

# 28. MICROINTERACTIONS

Keep movement calm and subtle.

Allowed:

- ~2–4px card lift
- slight image zoom
- soft shadow/border transitions
- active nav indicator
- button press/focus states
- smooth Bootstrap drawer/offcanvas transitions

Avoid:

- Parallax
- Bouncing CTAs
- Large entrance animations
- Constant movement
- Heavy page transitions

Performance and clarity matter more than animation.

---

# 29. PERFORMANCE / VISUAL STABILITY

During UI polish verify:

- Images have stable dimensions/aspect ratios.
- Layout does not jump while images load.
- No unnecessary giant background assets.
- Existing WebP optimization remains intact.
- No new render-blocking libraries.
- CSS/JS remains reasonably scoped.
- Sticky navbar does not cause layout shift.

Do not reopen infrastructure optimization unless the UI changes introduce a regression.

---

# 30. ROLE-BY-ROLE FINAL SMOKE TEST

## Guest

Verify:

- Home
- Shop/search
- Categories
- Filters
- Listing details
- Store
- Login
- Register
- Forgot/reset flow presentation

## Buyer

Verify:

- Navbar Cart direct access
- Account dropdown
- Cart if supported
- Checkout
- Orders
- Disputes
- Account settings

## Merchant

Verify:

- Marketplace navigation as allowed
- Cart if Merchant can buy under current rules
- Merchant Center direct workspace entry
- Verification
- Listings
- Inventory
- Orders
- Offers
- Deals
- Reviews
- Disputes
- Analytics
- Store Settings

## Admin

Verify:

- Admin Workspace direct entry
- No inappropriate buyer actions
- Merchant Verification
- Listing Moderation
- Orders
- Deals
- Disputes
- Catalog
- Reviews
- Audit Log

---

# 31. Pagination Coverage

Audit every existing paginated page discovered in the latest source.

Known categories to verify include:

## Public
- Shop browse/results

## Buyer
- Orders
- Disputes

## Merchant
- Listings
- Inventory
- Orders
- Offers
- Deals
- Disputes
- Reviews

## Admin
- Merchant Verification
- Listing Moderation
- Orders
- Deals
- Disputes
- Reviews
- Audit Log

Also include any additional existing paginated page found during the source audit.

---

# 32. Implementation Order

Execute in this order unless a real code dependency requires otherwise:

```text
00  Source-backed final audit
01  Global design tokens / spacing normalization
02  Final Navbar + role-aware navigation + direct Cart
02A Iconography/action-semantic audit and consistency pass
03  Hero height/content-density correction
04  Product/category card size and grid-density correction
05  Authentication UI final polish
06  Home-page rhythm/section polish
07  Shop/search/filters/pagination polish
08  Listing details/store polish
09  Cart/Checkout/Buyer polish
10  Merchant workspace final polish
11  Admin workspace final polish
12  Forms/tables/states/footer consistency
13  Responsive pass
14  Accessibility pass
15  CSS cleanup only where proven safe
16  Regression/build/test
17  Production-ready completion report
```

Do not skip the initial audit.

---

# 33. Acceptance Criteria — Navbar

Navbar is complete only when:

- Sticky behavior works.
- Desktop height is compact.
- No overflow at laptop widths.
- Search remains easy to use.
- Cart is directly visible for buyer-capable users.
- Cart is not duplicated in the Account dropdown.
- Merchant Center is easy for Merchants to enter.
- Admin Workspace is easy for Admin to enter.
- Guest auth actions are clear.
- Account menu is clean and personal rather than a second navigation sitemap.
- Long user names do not break layout.
- Mobile has one-tap access to Cart/account/menu.
- Search is still practical on mobile.
- Keyboard/focus behavior works.
- No role sees links it cannot meaningfully use.

---

# 34. Acceptance Criteria — Hero

Hero is complete only when:

- It no longer feels like 1.5 screens tall.
- At normal laptop/desktop size, the beginning of the next discovery/category section is visible in or immediately below the first viewport.
- Title remains premium but not oversized.
- CTA hierarchy is clear.
- Trust content is compact.
- Image crop is meaningful.
- Mobile hero is content-driven rather than forced to full viewport height.
- No essential content is hidden behind the sticky navbar.

---

# 35. Acceptance Criteria — Cards

Product/category cards are complete only when:

- A single product card no longer feels close to viewport height.
- Multiple products can be compared comfortably on one screen.
- Product imagery remains premium.
- Metadata is compact and useful.
- Card heights do not contain unnecessary empty areas.
- Grid density is appropriate at each breakpoint.
- Mobile cards remain readable.
- Hover/focus behavior is subtle and accessible.

---

# 36. Build and Regression Validation

Before declaring this phase complete, run:

```bash
dotnet build Faed.slnx -c Release
```

Run the project locally and smoke-test touched flows.

Run existing automated tests if available and practical.

Because this is a UI/UX phase, there should normally be:

```text
No new migrations
No database schema change
No production environment-variable change
No R2 configuration change
No Brevo configuration change
```

If EF unexpectedly reports model changes, **stop and report instead of creating a migration**.

---

# 37. Production Publishing Rule After UI Changes

For UI-only changes, the normal deployment cycle is:

```text
Edit
→ Build
→ Local smoke test
→ Visual Studio Publish using the existing SmarterASP publish profile
→ Production smoke test
```

Do not recreate:

- Database
- Environment variables
- R2 credentials/configuration
- Brevo configuration
- Production Admin
- Publish profile

Do not rerun migrations unless this plan was intentionally violated by an approved schema change.

After publishing:

- Hard-refresh or use Incognito/private browsing.
- Verify CSS/JS versioned assets loaded.
- Test navbar and the highest-risk touched flows first.

---

# 38. Final Completion Report

At the end, provide a concise report containing:

1. Final status: `PASS`, `PASS WITH DOCUMENTED LIMITATIONS`, or `FAIL`
2. Files changed
3. Navbar improvements
4. Iconography/action-semantic improvements
5. Hero sizing changes
6. Product/category card sizing changes
7. Authentication improvements
8. Public marketplace improvements
9. Buyer improvements
10. Merchant improvements
11. Admin improvements
12. Responsive improvements
13. Accessibility improvements
14. CSS cleanup performed
15. Build result
16. Test/smoke-test result
17. Any intentionally deferred issue
18. Confirmation that no DB/business/auth/integration behavior was intentionally changed

Do not claim `PASS` unless the Release build succeeds.

---

# 39. Definition of Done

This final UI/UX phase is complete only when:

## Global
- Faed feels visually coherent and production-ready.
- The UI is category-neutral enough for future marketplace expansion.
- No existing feature is lost.

## Navbar
- Compact, sticky, role-aware, search-first.
- Direct Cart access for buyer-capable users.
- Account menu simplified.
- Merchant/Admin workspace access is clear.
- Mobile navigation is excellent, not merely acceptable.
- Iconography is coherent, semantic, accessible, and uses the existing icon family.
- Cart/search/account/workspace icons improve recognition without overcrowding the navbar.

## Home
- Hero is significantly better proportioned.
- Next content begins quickly.
- Category cards are compact and useful.
- Product cards no longer dominate the viewport.
- Section rhythm is controlled.

## Marketplace
- Search/filter/sort context is clear.
- Quick Filters + More Filters Drawer remains the standard.
- Pagination is consistent and preserves state.
- Listing details support fast purchase decisions.

## Identity
- Register/Login look native to Faed.
- No floating-label duplication.
- New profile fields are presented cleanly.

## Buyer
- Cart/checkout/orders/disputes are clear and responsive.

## Merchant
- Sidebar workspace is coherent and efficient.
- Long forms/tables/statuses are polished.

## Admin
- Operational pages are readable, dense where appropriate, and easy to scan.

## Technical
- Release build passes.
- No new migration.
- No production configuration regression.
- R2/Brevo/SQL/Identity behavior remains intact.
- Production publish can use the existing Visual Studio profile.

---

# 40. Agent Execution Prompt

Use this short prompt with the latest project and this file:

> Read `plan.md` completely, then inspect the latest Faed codebase before changing anything. Treat all previous phases as DONE and execute this as the final UI/UX production-polish phase. The latest code is the source of truth. Prioritize the final navbar/role-aware navigation and direct Cart access, perform a coherent iconography/action-semantic pass using the existing icon library, then correct the oversized hero and oversized product/category cards, followed by Identity, marketplace, Buyer, Merchant, Admin, responsive, accessibility, and CSS consistency. Preserve every working route, business rule, authorization rule, database behavior, pagination/filter/search semantic, R2 integration, Brevo integration, and production configuration. Do not create migrations. If a requested UI improvement requires a backend or schema change, stop and report it first. Finish with a Release build, smoke-test report, files changed, and final PASS/PASS WITH DOCUMENTED LIMITATIONS/FAIL status.

---

## Final Rule

> **Faed should feel premium because it is clear, proportioned, fast, and easy to use — not because every component is large.**
