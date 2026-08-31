# 21 — Claude Skills Usage

## Purpose

This file explains how Faed uses:
- repository-specific skills under `.claude/skills/`;
- general Claude skills available in the account/workspace.

## Built-in / workspace skills expected when available

Relevant general skills include:
- `/modern-web-guidance`
- `/design-system`
- `/design-critique`
- `/accessibility-review`
- `/ux-copy`

These are **general-purpose** skills.
They improve modern web execution, design quality, accessibility review, and UX writing.

## Why Faed still needs project skills

General skills do not understand Faed-specific marketplace rules such as:
- `ConditionGrade` vs `DiscountReason`;
- defect-evidence prominence;
- verified merchant trust blocks;
- dual B2C/B2B flows;
- wholesale MOQ;
- distinction between public vs merchant/admin UI contexts.

Therefore the repository defines project skills that specialize the design behavior for Faed.

## Project skills

### `faed-ui-direction`
Visual identity and design-system behavior.

### `faed-commerce-ux`
Faed-specific commerce UI rules.

### `faed-marketplace-pages`
Page blueprints for public marketplace pages.

### `faed-dashboard-ux`
Merchant/Admin operational interface rules.

### `faed-responsive-accessibility`
Responsive and accessibility review.

### `faed-ui-quality-gate`
Final quality review to reject generic or AI-looking UI.

## Recommended combinations

### Public marketplace pages
Use:
- `faed-ui-direction`
- `faed-commerce-ux`
- `faed-marketplace-pages`

Also use when available:
- `/modern-web-guidance`
- `/design-system`

Before completion:
- `faed-responsive-accessibility`
- `faed-ui-quality-gate`
- `/design-critique`
- `/accessibility-review`
- `/ux-copy`

### Merchant/Admin pages
Use:
- `faed-ui-direction`
- `faed-dashboard-ux`

Also use when available:
- `/modern-web-guidance`
- `/design-system`

Before completion:
- `faed-responsive-accessibility`
- `faed-ui-quality-gate`
- `/design-critique`
- `/accessibility-review`
- `/ux-copy`

## Important rule

Repository-specific skills never override the authoritative requirements in:
- `AGENTS.md`
- `/docs`
- `/tasks`

They operationalize them.
