---
name: faed-ui-quality-gate
description: >
  Mandatory final quality gate for Faed UI work. Use after building any page,
  component, or flow to detect generic AI-looking output, weak hierarchy, poor trust,
  or missing states.
---

# Faed UI Quality Gate

## Mission

This is the final UI review gate.

Use it after implementation and before calling a UI task complete.

## Reject if the page looks like:

- default Bootstrap;
- a generic AI-generated dashboard/marketplace;
- a random template with Faed text pasted into it;
- a card-on-card-on-card layout without hierarchy;
- a pretty page that hides crucial commerce information.

## Review categories

### 1. Visual distinctiveness
- Does this look custom?
- Is it clearly more refined than default Bootstrap?
- Is it free of obvious AI-template symptoms?

### 2. Hierarchy
- Is the primary CTA obvious?
- Is the most important information visually first?
- Are sections clearly differentiated?

### 3. Commerce clarity
- Can the user quickly understand product, price, condition, and discount reason?
- Is B2C/B2B clear?
- Is availability honest?

### 4. Trust
- Is merchant verification clear?
- Are defects visible where needed?
- Do discount signals feel transparent, not manipulative?

### 5. Component consistency
- Are cards, badges, buttons, and forms stylistically consistent?
- Are spacing and type scales consistent?

### 6. Context appropriateness
- Does a public page feel different from a merchant/admin screen?
- Is the density right for the context?

### 7. State coverage
Check for:
- empty state;
- no results;
- sold out;
- validation errors;
- loading/processing where needed;
- rejected/pending merchant/listing states where applicable.

### 8. Responsive & accessibility readiness
This gate does not replace `faed-responsive-accessibility`.
It verifies that responsiveness/accessibility review actually happened.

## Mandatory completion note

For every UI task, produce a short review summary:
- what was improved to avoid a generic AI look;
- how trust and clarity were strengthened;
- what components were standardized;
- what responsive/accessibility issues were fixed.
