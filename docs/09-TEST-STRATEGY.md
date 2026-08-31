# 09 — Test Strategy

## 1. Test layers

### Unit tests
Use for:
- pure business rules;
- state transitions;
- calculations;
- eligibility rules;
- expiry decisions.

### Integration tests
Use for:
- EF Core mappings;
- Identity authorization integration;
- SQL Server transactions;
- `rowversion` concurrency;
- unique/check constraints;
- inventory reservation.

### MVC/application tests
Use where valuable for:
- authorization;
- important POST endpoints;
- validation and redirects.

---

## 2. Critical rule

**SQL Server concurrency must be tested against SQL Server.**

Do not claim concurrency is verified using:
- EF Core InMemory;
- SQLite.

A SQL Server test database, LocalDB, CI SQL Server service, or Testcontainers-style SQL Server environment is acceptable.

---

## 3. Must-have tests by domain

### Merchant verification
- unapproved merchant cannot submit listing;
- admin can approve;
- non-admin cannot approve;
- private document inaccessible to public user.

### Listing
- non-live listing not public;
- material edit requires moderation;
- condition and discount reason persist separately;
- duplicate variant combination rejected.

### Inventory
- cannot go negative;
- manual adjustment audited;
- concurrent last-unit purchase: only one succeeds;
- B2C and B2B competing for same stock: only valid reservation succeeds.

### B2C
- one merchant per order;
- totals recomputed server-side;
- reservation created atomically;
- cancel releases stock;
- complete converts reserved to sold;
- invalid status transition rejected.

### B2B negotiation
- counter-offer preserves old revisions;
- expired revision cannot be accepted;
- seller cannot accept negotiation it does not own;
- buyer cannot buy from itself.

### B2B deal
- all lines reserve atomically;
- reservation expiry releases all remaining stock exactly once;
- completion moves reserved to sold;
- repeated expiry job is idempotent.

### Review
- non-completed transaction rejected;
- unrelated user rejected;
- duplicate review rejected.

### Dispute
- must reference exactly one transaction type;
- participant required;
- admin resolution logged.

---

## 4. Acceptance test data

Use deterministic seeded/demo fixtures for:
- one admin;
- two approved merchants;
- one pending merchant;
- one buyer;
- listings with variants;
- one sold-out listing;
- one Grade B listing;
- one B2B-enabled listing.

See `12-SEED-DATA.md`.

---

## 5. Test naming

Prefer behavior names:

`PlaceOrder_WhenTwoBuyersCompeteForLastUnit_OnlyOneSucceeds`

rather than:

`TestOrder1`

---

## 6. CI baseline

When CI is introduced, it should run:
1. restore;
2. build;
3. unit tests;
4. integration tests;
5. migration validation where practical.

No deployment if critical tests fail.
