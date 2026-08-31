# ADR 0003 — Separate Condition from Discount Reason

## Status
Accepted.

## Decision
Physical `ConditionGrade` and commercial `DiscountReason` are separate concepts.

## Why
"Past Season" or "Overstock" does not imply physical imperfection.

## Consequences
A Grade A product may still be discounted because it is past-season/overstock.
