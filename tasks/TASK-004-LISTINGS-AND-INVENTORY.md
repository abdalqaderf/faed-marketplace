# TASK-004 — Listings, Variants, Inventory and Moderation

## Objective
Allow an approved merchant to create real Fashion Overstock listings with generic options/variants and safe inventory.

## Deliverables
- Listing aggregate
- Listing options/values
- `ListingVariant`
- media and defect media
- discount reasons
- reference-price evidence metadata
- B2C/B2B flags
- MOQ
- `RowVersion`
- inventory adjustment audit
- moderation workflow
- merchant listing management
- admin moderation

## Mandatory examples
The model must represent:
- T-shirt: Black/M, Black/L, White/M
- Shoes: sizes 41, 42, 43

without separate hard-coded clothing/shoe entity designs.

## Exit criteria
- [ ] Variant combination is unique.
- [ ] Stock is variant-level.
- [ ] Quantities cannot become negative.
- [ ] Live listing material edit requires moderation.
- [ ] Public cannot see non-Live data.
- [ ] Defect media is distinguishable.
- [ ] Migration includes RowVersion from first variant creation.
