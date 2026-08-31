# ADR 0002 — Inventory at Variant/SKU Level

## Status
Accepted.

## Decision
Authoritative inventory is stored on `ListingVariant`, not on `Listing`.

## Why
Fashion stock differs by size/color. A single listing quantity cannot represent real stock safely.

## Consequences
- Checkout and B2B reservations operate on variant lines.
- Listing totals are derived.
- Each quantity-bearing variant uses SQL Server `rowversion`.
