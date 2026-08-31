# 07 — UI / UX Specification

## 1. Product feel

Faed should feel like a **trusted modern commerce marketplace**, not a classifieds board.

Priorities:
1. clarity;
2. trust;
3. discount transparency;
4. mobile usability;
5. fast understanding of condition.

---

## 2. Language

MVP interface is English-only.

Use clear commerce wording:
- "Condition"
- "Why discounted"
- "Defect details"
- "Verified merchant"
- "Retail"
- "Wholesale"
- "Make an offer"
- "Pickup"
- "Merchant delivery"

Avoid internal engineering terms in customer UI.

---

## 3. Public navigation

Suggested:
- Home
- Shop
- Categories
- How It Works
- Sell as a Merchant
- Sign In

Authenticated menus adapt by role.

---

## 4. Public pages

### Home
Should communicate within one screen:
- what Faed sells;
- why products are discounted;
- that sellers are verified merchants;
- retail and wholesale availability.

### Shop
Filters:
- category;
- price;
- condition;
- discount reason;
- size;
- color;
- brand if present;
- B2C/B2B availability.

### Listing Detail
Above the fold on mobile:
- images;
- title;
- merchant + verified indicator;
- price;
- reference price/discount when valid;
- condition grade;
- "Why discounted";
- variant selector;
- quantity/availability;
- primary CTA.

Defect information must be prominent.

### Merchant Store
- business name;
- verified status;
- aggregate trust signals;
- active listings.

---

## 5. Buyer area

Suggested pages:
- Orders
- Order Details
- Profile
- Reviews

Do not overwhelm with features not in MVP.

---

## 6. Merchant area

Suggested navigation:
- Dashboard
- Listings
- Inventory
- B2C Orders
- B2B Offers
- B2B Deals
- Analytics
- Store Settings

Listing creation should be a guided form:
1. product;
2. category;
3. condition;
4. reason for discount;
5. variants;
6. stock;
7. pricing;
8. photos/defects;
9. fulfillment/policies;
10. review/submit.

---

## 7. Admin area

Suggested:
- Overview
- Merchant Verification
- Listing Moderation
- Orders
- B2B Deals
- Disputes
- Catalog
- Reviews
- Audit Log

Private verification documents must never be linked from public screens.

---

## 8. Condition presentation

Use a consistent visual component:

```text
Condition: Grade B — New, packaging imperfect
Why discounted: Past season + damaged shoe box
```

Do not rely on "Grade B" alone.

Users need the human-readable meaning.

---

## 9. Price presentation

When reference price is approved/usable:

```text
JOD 34.900
Reference: JOD 49.900
30% lower
```

Do not show a computed discount if reference price is missing or untrusted.

---

## 10. Mobile-first rules

- touch targets large enough;
- sticky primary CTA on product detail when useful;
- filters in mobile drawer;
- images optimized;
- tables in merchant/admin areas adapt to cards/scroll;
- no horizontal layout that assumes desktop.

---

## 11. Accessibility baseline

- semantic HTML;
- labels for inputs;
- keyboard-accessible actions;
- visible focus;
- meaningful image alt text;
- validation summary + field error;
- do not communicate state only through color.

---

## 12. Empty/error states

Design explicit states:
- no listings;
- no matching results;
- sold out;
- stock changed during checkout;
- offer expired;
- merchant pending approval;
- listing rejected with reason;
- no analytics data yet.

Do not show generic blank pages.

---

## 13. UI consistency

Use Bootstrap 5 primitives and a small project design-token layer.

Do not introduce a large JS UI framework.

Keep:
- button hierarchy;
- badges;
- cards;
- alerts;
- forms;
- spacing;
- typography

consistent across Buyer, Merchant, Admin.
