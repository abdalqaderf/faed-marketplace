# TASK-003 — Catalog Foundations

## Objective
Create the DB-driven taxonomy and disclosure reference data required by Fashion Overstock.

## Deliverables
- hierarchical `Category`
- `ConditionGrade`
- `DiscountReason`
- optional `Brand`
- idempotent seed
- basic admin management where needed

## Required seed
- Fashion Overstock
  - Clothing
  - Shoes
  - Bags & Accessories
- Grades A-D
- approved discount reasons from the PRD

## Critical rule
Condition and discount reason remain separate.

## Exit criteria
- [ ] Seed runs repeatedly without duplication.
- [ ] No category/condition business values are hard-coded into public views.
- [ ] Grade E is absent from MVP.
- [ ] Catalog unit/integration tests pass.
