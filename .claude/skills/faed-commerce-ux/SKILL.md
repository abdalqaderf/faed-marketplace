---
name: faed-commerce-ux
description: >
  Faed-specific commerce UX rules for product cards, listing details, pricing,
  conditions, defects, variants, B2C, B2B, trust blocks, and commerce CTAs.
  Use on any product-facing or transaction-facing page.
---

# Faed Commerce UX

## Mission

Implement Faed's marketplace UX in a way that makes discounted inventory feel:
- understandable;
- honest;
- trustworthy;
- easy to evaluate.

This skill is **Faed-specific** and must override generic marketplace assumptions when necessary.

## Non-negotiable product communication

Every relevant commerce surface must clearly communicate:
1. what the product is;
2. who is selling it;
3. current price;
4. whether the merchant is verified;
5. physical condition;
6. why it is discounted;
7. availability;
8. fulfillment options;
9. whether B2C and/or B2B are available.

## Condition vs discount reason

Never merge these into one vague label.

Bad:
- `Grade B`
- `Discounted inventory`

Good:
- `Condition: Grade B — New, packaging imperfect`
- `Why discounted: Past season + packaging damage`

These must appear as separate UI concepts and separate data concepts.

## Product card anatomy

A product card should quickly show:
1. image;
2. title;
3. price;
4. reference price/discount only if valid;
5. condition/reason signal;
6. verified merchant or merchant name;
7. optional wholesale indicator.

Do not overload product cards with every listing field.

## Listing detail order

Above the fold:
1. image gallery;
2. title;
3. verified merchant;
4. current price;
5. reference price/discount if valid;
6. condition;
7. why discounted;
8. variant selectors;
9. stock/availability;
10. primary CTA;
11. fulfillment summary.

Immediately below:
- defect evidence;
- detailed description;
- included/missing items;
- return policy;
- warranty if any;
- merchant trust details;
- B2B purchase block.

## Pricing rules

Current price is visually dominant.

Reference price:
- secondary;
- only shown if supported/approved.

Discount %:
- tertiary;
- only shown when reference price is trusted.

Do not use aggressive sales language or fake urgency.

## Defect evidence

If a listing has a defect, cosmetic issue, damaged packaging, or display wear:
- it must be discoverable quickly;
- it must be visually labeled;
- it must not be buried inside a general image gallery without context.

Use explicit labels such as:
- `Defect photo`
- `Packaging issue`
- `Cosmetic mark`

## Variant UX

Variant selection must be:
- SKU-aware;
- accessible;
- obvious;
- honest.

Rules:
- disable unavailable combinations;
- show clear active state;
- support size and color cleanly;
- do not imply stock exists at listing level when the selected SKU is unavailable.

Prefer chips/buttons for small sets and selects only when option count becomes large.

## Availability states

Use understandable inventory states:
- `In stock`
- `Low stock`
- `Sold out`
- `Limited variant availability`

Avoid exact unit-count obsession in every context unless useful.

## CTA rules

Primary CTA examples:
- `Add to Order`
- `Make an Offer`
- `Sign in to Order`
- `Sold Out`

Disabled CTAs must explain why.

## B2B block

When B2B is enabled, the page should clearly show:
- minimum order quantity;
- whether mixed variants are allowed;
- indicative wholesale pricing when available;
- wholesale CTA separate from retail purchase flow.

Do not mix B2C and B2B into one ambiguous purchase form.

## Trust block

Show calm and credible signals:
- verified merchant;
- basic location/service context if appropriate;
- rating only when meaningful;
- completed transactions or reliability only when real data exists.

Do not fabricate social proof.
