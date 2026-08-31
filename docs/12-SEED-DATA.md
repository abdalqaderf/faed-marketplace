# 12 — Development / Demo Seed Data

Seed must be deterministic and safe for Development/Demo only.

Do not store a fixed production admin password.

## Reference data

### Category hierarchy
```text
Fashion Overstock
├── Clothing
│   ├── Tops
│   ├── Bottoms
│   ├── Dresses
│   └── Outerwear
├── Shoes
│   ├── Sneakers
│   ├── Casual Shoes
│   └── Sandals
└── Bags & Accessories
    ├── Bags
    ├── Belts
    └── Non-sensitive Accessories
```

> Reference seed (TASK-003) creates only `Fashion Overstock` and the three launch
> categories. The lower-level categories above are dev/demo data seeded in a later task
> (open question 4).

### Condition grades
- A — New / Complete
- B — New / Packaging Imperfection
- C — Opened or Returned / Unused
- D — Display / Cosmetic Imperfection

### Discount reasons
- Overstock
- Past Season
- Customer Return
- Display Item
- Packaging Damage
- Cosmetic Defect
- Missing Non-Essential Item
- Other Approved Reason

---

## Demo users

Use environment-provided passwords.

Suggested identities:
- Admin
- Approved Merchant A
- Approved Merchant B
- Pending Merchant
- Buyer A
- Buyer B

---

## Demo listings

### Listing 1 — Sneakers
- Condition: B
- Reasons: Past Season + Packaging Damage
- Options:
  - Size: 41, 42, 43
  - Color: Black
- B2C: enabled
- B2B: enabled
- MOQ: 10

### Listing 2 — T-Shirt
- Condition: A
- Reason: Overstock
- Options:
  - Size: M, L, XL
  - Color: Black, White
- B2C/B2B enabled

### Listing 3 — Handbag
- Condition: D
- Reason: Display Item
- Visible cosmetic defect photo
- B2C enabled
- B2B disabled

### Listing 4 — Sold-out listing
For public sold-out behavior testing.

---

## Seed scenarios

Create data that supports testing:
- one active B2C order;
- one completed B2C order;
- one open B2B negotiation;
- one counter-offer chain;
- one completed B2B deal;
- one dispute;
- one review.

Do not seed transactional scenarios until their implementation phase exists.
