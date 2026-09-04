# 00 — Specification Map

This repository intentionally separates product decisions from implementation guidance.

## Read map

| File | Purpose |
|---|---|
| `AGENTS.md` | Highest-priority engineering contract |
| `01-PRD.md` | What the product is and must do |
| `02-SCOPE-AND-DECISIONS.md` | Locked MVP decisions |
| `03-BUSINESS-RULES.md` | Rules the application must enforce |
| `04-DOMAIN-MODEL.md` | Conceptual entities/relationships |
| `05-USER-FLOWS-AND-STATE-MACHINES.md` | Workflow behavior |
| `06-ARCHITECTURE.md` | Solution/project design |
| `07-UI-UX-SPEC.md` | English web experience |
| `08-SECURITY-AND-PRIVACY.md` | Security/privacy requirements |
| `09-TEST-STRATEGY.md` | Required testing approach |
| `10-IMPLEMENTATION-PLAN.md` | Build sequence and phase gates |
| `11-ACCEPTANCE-CRITERIA.md` | MVP completion checklist |
| `12-SEED-DATA.md` | Development/demo data |
| `13-OPEN-QUESTIONS.md` | Decisions agents must not invent |
| `14-FUTURE-EXPANSION.md` | Multi-sector growth boundaries |
| `15-GLOSSARY.md` | Canonical domain language |
| `16-PERMISSIONS-MATRIX.md` | Role/action authorization matrix |
| `17-DATA-INVARIANTS.md` | Database/domain invariants |
| `18-TRACEABILITY.md` | Requirement → phase → test mapping |
| `19-CODING-CONVENTIONS.md` | Concrete .NET implementation conventions |
| `20-DEVELOPMENT-WORKFLOW.md` | How agents execute and close tasks |

## Rule

Do not duplicate business rules into code comments as a second source of truth.
Reference the relevant specification/ADR when a non-obvious rule needs explanation.


## Task queue

The implementation queue is already prepared:

1. `TASK-001-FOUNDATION.md`
2. `TASK-002-MERCHANT-VERIFICATION.md`
3. `TASK-003-CATALOG.md`
4. `TASK-004-LISTINGS-AND-INVENTORY.md`
5. `TASK-005-PUBLIC-MARKETPLACE.md`
6. `TASK-006-B2C-ORDERS.md`
7. `TASK-007-B2B-NEGOTIATION.md`
8. `TASK-008-B2B-DEALS.md`
9. `TASK-009-TRUST.md`
10. `TASK-010-ANALYTICS-AND-ADMIN.md`
11. `TASK-011-HARDENING-AND-DEMO.md`

Execute tasks in order unless the product owner explicitly changes the plan.
