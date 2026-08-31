# 13 — Open Questions

These are intentionally unresolved or configurable. The coding agent must not silently invent permanent rules.

## Before Phase 1 public deployment
1. Exact Jordanian business document types accepted for merchant verification.
2. Whether email confirmation is mandatory before merchant application.
3. Exact retention policy for rejected verification documents.

## Before Phase 3
4. Final lower-level fashion taxonomy.
5. Whether Brand is required or optional by category.
6. Whether a merchant can create its own brand name or only choose admin-controlled brands.
7. Exact reference-price evidence requirements.

## Before Phase 5
8. Default B2C reservation duration.
9. Merchant confirmation SLA.
10. Exact customer cancellation window.
11. Exact no-show rules.
12. Delivery-zone modeling detail for Amman.
13. Whether a buyer can add items from several listings of the same merchant into one order in the first public demo. Domain model supports it; UI can be simplified initially if needed.

## Before Phase 6/7
14. Default B2B offer validity.
15. Default accepted-deal reservation duration.
16. Whether counter-offer can modify quantity and price in one revision.
17. Maximum counter-offer rounds, if any.
18. Whether B2B reviews are one-way or both merchants may review each other.

## Legal/policy
19. Final return/exchange policy for size/change-of-mind.
20. Platform Terms of Use.
21. Privacy policy.
22. Merchant seller agreement.
23. Exact dispute resolution policy.
24. Tax/invoice responsibilities.
25. Handling of branded product authenticity claims.

## Infrastructure
26. Production hosting provider.
27. Object storage provider.
28. Email provider.
29. Production domain.
30. Deployment/CI environment.

## Important

Configurable durations should live in configuration, not hard-coded constants.

When a question becomes necessary for the active phase, ask the product owner only if the existing specification cannot support a safe reversible default.
