# 18 — Requirements Traceability

This matrix prevents requirements from disappearing between documents and implementation.

| Requirement | Primary spec | Build phase | Critical verification |
|---|---|---|---|
| Verified merchants only sell | PRD §4 / Business Rules §1 | Phase 1 | authorization integration test |
| English MVP UI | Scope Decisions | all | QA string review |
| Fashion-only launch UI | PRD §5 | Phase 2-4 | seed/catalog + public browse test |
| Condition separate from discount reason | Business Rules §3 | Phase 2-3 | persistence/domain test |
| Variant-level inventory | Domain Model §4 | Phase 3 | model/constraint test |
| Listing moderation before public | Scope Decisions | Phase 3 | public visibility integration test |
| One merchant per B2C order | Business Rules §7 | Phase 5 | mixed-merchant rejection test |
| Server-side price calculation | Security §7 | Phase 5 | forged-price test |
| B2C stock concurrency | Business Rules §7 | Phase 5 | SQL Server simultaneous last-unit test |
| Immutable B2B revisions | Business Rules §9 | Phase 6 | counter-offer history test |
| Offer expiry separate from reservation expiry | Business Rules §9-10 | Phase 6-7 | expiry tests |
| Atomic multi-line B2B reservation | Business Rules §10 | Phase 7 | SQL Server transaction/concurrency test |
| Reviews after Completed only | Business Rules §13 | Phase 8 | service/integration test |
| Dispute participant only | Security/Business Rules | Phase 8 | IDOR/authorization test |
| Recovered-value analytics derived | PRD §14 | Phase 9 | reconciliation test |
| Verification files private | Security §3 | Phase 1 | unauthorized download test |
| Admin actions auditable | Domain Model §10 | Phase 1+ | audit assertion tests |
| Multi-sector-ready taxonomy | Future Expansion | Phase 2-3 | architecture/code review |
