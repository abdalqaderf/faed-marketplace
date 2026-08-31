---
name: faed-ui-direction
description: >
  Faed's visual direction and design system guidance. Use whenever creating or
  modifying layouts, Razor views, CSS, design tokens, navigation, product cards,
  forms, or any public/merchant/admin UI. Prevent generic AI-looking or default Bootstrap UI.
---

# Faed UI Direction

## Mission

Create a distinctive, modern, trustworthy marketplace interface for **Faed**.

The UI must **not** look like:
- default Bootstrap;
- an AI-generated template;
- a generic marketplace clone;
- a noisy discount website;
- a fashion-luxury editorial site.

Faed should feel:
- modern;
- credible;
- sharp;
- commerce-first;
- transparent;
- premium enough to inspire trust;
- restrained rather than flashy.

## Core principles

1. **Function before decoration**  
   Visual choices should improve understanding, not simply add style.

2. **Trust before hype**  
   Faed sells discounted inventory. The UI must reassure users instead of feeling manipulative.

3. **Clarity before density**  
   Show the most decision-relevant information first.

4. **Consistency before creativity**  
   Components should feel like a system.

5. **Distinctive without gimmicks**  
   The interface should look custom, but not weird.

## Visual direction

### Tone
Use a visual tone that sits between:
- a clean premium SaaS product;
- a serious modern commerce marketplace.

Avoid both extremes:
- sterile enterprise grayness;
- trendy dribbble-like ornamentation.

### Color system
Use a neutral-first palette with one restrained brand accent and purposeful semantic colors.

Design intent:
- strong text contrast;
- light, calm surfaces;
- subtle emphasis rather than over-coloring;
- condition/warning/error states must be legible.

Do not hardcode arbitrary page-level colors.

Create a small token system using CSS variables, for example:
- background
- surface
- muted surface
- text
- muted text
- border
- brand
- brand-strong
- success
- warning
- danger
- info

### Typography
Use a modern sans-serif appropriate for commerce and dashboards.

Rules:
- avoid over-sized hero text that pushes useful information down;
- use a clear type scale;
- keep body text highly readable;
- ensure labels and metadata remain legible on mobile.

### Spacing
Use a consistent spacing scale.
Prefer rhythm and breathing room over crowding.

### Radius & shadows
Use restrained radii and subtle shadows.
Avoid exaggerated rounded-corner “AI SaaS” aesthetics.

### Containers
Use consistent max-width containers and sectional spacing.
Do not randomly switch between wide and narrow layouts without reason.

## Bootstrap rule

Bootstrap 5 is a foundation, not the visual identity.

Required behavior:
- customize variables/tokens;
- create project-level component classes;
- avoid shipping default Bootstrap cards/buttons/forms as the final result.

## Components and systemization

Build reusable:
- buttons
- badges
- inputs
- product cards
- section headers
- stat cards
- empty states
- tables
- form help/error styles
- merchant/admin side navigation if needed

Use partials/View Components for repeated patterns.

## What makes UI look AI-generated, and how to avoid it

Avoid these common AI-generated UI signs:
- every element trapped in a card;
- all sections having the same visual weight;
- random gradients and glow effects;
- giant rounded corners everywhere;
- heavy drop shadows on everything;
- unclear primary CTA;
- inconsistent spacing;
- decorative icons without meaning;
- long hero sections with vague text;
- too many similarly styled chips/badges;
- no real distinction between public, merchant, and admin interfaces.

Countermeasures:
- strong hierarchy;
- different component density by context;
- data-first layout decisions;
- restrained motion;
- meaningful whitespace;
- visible trust blocks;
- fewer but stronger components.

## Before implementing a page

Write down:
1. primary user goal;
2. primary CTA;
3. most important trust question;
4. required above-the-fold information;
5. mobile structure;
6. empty/error/loading/sold-out states.

Then build.
