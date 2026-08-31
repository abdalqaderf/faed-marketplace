# 10 — Implementation Plan

Build in order. Each phase has an exit gate.

---

## Phase 0 — Foundation

Create:
- solution/projects;
- project references;
- configuration;
- MVC web app;
- Bootstrap base layout;
- EF Core SQL Server;
- Identity;
- logging/error baseline;
- unit/integration test projects.

Do not create marketplace entities yet except identity foundation genuinely required.

### Exit gate
- solution builds;
- app runs;
- DB migration applies;
- registration/login works;
- tests run;
- no secrets committed.

---

## Phase 1 — Roles and Merchant Verification

Implement:
- Buyer/Merchant/Admin roles;
- merchant application/profile;
- private verification document upload;
- verification states;
- admin review;
- audit log foundation;
- authorization policy `ApprovedMerchant`.

### Exit gate
- pending merchant cannot access listing submission;
- admin can approve/reject;
- unauthorized user cannot retrieve verification file;
- audit entry exists.

---

## Phase 2 — Catalog Foundations

Implement:
- hierarchical Category;
- ConditionGrade;
- DiscountReason;
- optional Brand;
- seed Fashion Overstock categories;
- seed A-D grades;
- seed discount reasons;
- admin read/manage where appropriate.

### Exit gate
- catalog comes from DB, not hard-coded UI strings;
- condition and discount reason are separate;
- seed is idempotent.

---

## Phase 3 — Listings, Options, Variants and Moderation

Implement:
- Listing;
- ListingOption;
- ListingOptionValue;
- ListingVariant;
- photos/defect photos;
- discount reasons;
- reference price evidence;
- stock;
- B2C/B2B enablement;
- MOQ;
- moderation workflow;
- merchant listing management;
- admin moderation.

### Exit gate
- merchant can model real size/color combinations;
- stock is variant-level;
- duplicate variant combination prevented;
- public sees Live only;
- material edit returns to review;
- RowVersion present from first variant migration.

---

## Phase 4 — Public Marketplace

Implement:
- Home;
- Shop;
- browse/paging;
- category/condition/reason/price filters;
- listing detail;
- merchant public page;
- English UI;
- responsive behavior.

### Exit gate
- anonymous buyer can discover and understand a listing;
- defect disclosure is prominent;
- no private/non-live content leaks.

---

## Phase 5 — B2C Orders

Implement:
- same-merchant cart/order builder;
- Order + OrderItems;
- server-side price calculation;
- stock reservation transaction;
- Pickup/MerchantDelivery;
- order history;
- merchant order management;
- cancellation/completion;
- reservation expiry policy.

### Mandatory tests
- concurrent last unit;
- forged price;
- multi-merchant order rejection;
- cancellation release;
- completion stock conversion.

### Exit gate
End-to-end B2C flow completes safely.

---

## Phase 6 — B2B Negotiation

Implement:
- create negotiation;
- quantity selection by variant;
- MOQ;
- immutable offer revisions;
- counter-offer;
- accept/reject;
- offer expiry.

### Exit gate
- revision history is preserved;
- expired offer cannot be accepted;
- permissions enforced.

---

## Phase 7 — B2B Deal and Fulfillment

Implement:
- atomic reservation on accepted revision;
- B2BDeal;
- B2BDealLines;
- separate reservation expiry;
- pickup/seller-arranged shipping;
- shipment reference;
- delivered/completed/cancelled;
- expiry release.

### Mandatory tests
- multi-line atomic reservation;
- B2C vs B2B concurrency;
- idempotent expiry release.

### Exit gate
Complete merchant-to-merchant deal works safely.

---

## Phase 8 — Disputes and Reviews

Implement:
- dispute creation/evidence;
- admin review;
- review gating;
- duplicate-review protection.

### Exit gate
- non-completed review rejected server-side;
- dispute resolution audited.

---

## Phase 9 — Analytics

Implement:
- recovered B2C value;
- recovered B2B value;
- sell-through;
- average time to sale;
- cancellations;
- active negotiations;
- stale listings.

### Exit gate
Values reconcile with seeded transaction data.

---

## Phase 10 — Admin Completeness

Implement/refine:
- merchant queue;
- listing moderation queue;
- catalog management;
- transaction monitoring;
- disputes;
- reviews;
- audit log.

---

## Phase 11 — Hardening and Delivery

- responsive QA;
- accessibility pass;
- security review;
- authorization audit;
- validation review;
- upload review;
- concurrency regression;
- production configuration;
- demo seed strategy;
- README run/deploy instructions.

### Exit gate
MVP can be demonstrated and field-tested with real merchants/inventory.

---

## Explicitly deferred

Do not build:
- Arabic UI;
- real online payments;
- escrow;
- platform shipping;
- shipping integrations;
- warehouses;
- auctions;
- native mobile app;
- merchant POS/ERP sync;
- subscriptions/commission billing;
- electronics-specific inspection.
