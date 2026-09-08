# Commits and Comments

How commits are written on `faed-core`, and what a comment in this codebase is for.

---

# Part 1 — Commits

## Format

```
Phase <n>: <imperative summary, lowercase, no period>

<why this change exists — 2 to 4 lines, wrapped at 80>
<what a reader six months from now would otherwise have to guess>

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

Rules:

- **One commit per phase.** Eleven commits, eleven rollback points. Do not commit mid-phase
  unless the build is green.
- **The subject says what. The body says why.** `git log --oneline` should read as the plan.
- **Never write "fix", "update", "changes", "wip".** If the subject could describe any commit
  in any project, it says nothing.
- **No emoji, no ticket numbers.** There is no tracker; `docs/PHASE-PLAN.md` is the backlog.
- The build and tests are green before every commit. A red commit is not a rollback point.

## Ready messages

### Phase 0

```
Phase 0: add faed-core scope docs and repository rules

Reduce the project to a single-sided, subscription-funded marketplace for
open-box and ex-display appliances. CLAUDE.md carries the invariants that must
survive the reduction; docs/ carries the scope, the business reasoning and the
eleven-phase plan. B2B-DESIGN.md preserves the module's design before deletion.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 1

```
Phase 1: add smoke tests before the deletion phases

Four service-level tests covering listing status transitions, submission
blockers, role authorization and reservation expiry. The next two phases delete
about 7,200 lines across 27 files; without a safety net a regression would only
surface by hand.

InMemory has no concurrency tokens, so the oversell test asserts that
OrderService handles DbUpdateConcurrencyException rather than provoking a real
rowversion conflict.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 2

```
Phase 2: delete the B2B module

Merchant-to-merchant trading was roughly 40% of the codebase's complexity with
no public surface and no validated demand. Removing it also removes the
sales-channel concept from Listing, so a retail price is now always required
instead of being conditional on AllowB2C.

Reviews lose their B2B branch and are now always tied to an order. The design is
preserved in docs/B2B-DESIGN.md.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 3

```
Phase 3: remove disputes and secondary entities

Buyers inspect the item before paying cash, which removes most of what a dispute
system existed to handle. Audit logging, inventory adjustments, delivery zones
and brands were built ahead of any need for them.

Reviews stay: merchant reputation is the platform's enforcement mechanism.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 4

```
Phase 4: simplify verification document validation

The validator was 2,705 lines — larger than the ordering and trust services
combined — because it decompressed PDF and PNG streams looking for embedded
content. Magic-byte, extension and size checks give the protection this MVP
actually needs at a fiftieth of the size.

The public API is unchanged, so no caller moves.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 5

```
Phase 5: repivot from fashion overstock to open-box appliances

The seeded reference data was always appliance-shaped: seven of the eight
discount reasons and all four condition grades describe boxes, returns and
display units, none of which mean anything for clothing.

Warranty becomes a structured field because it decides a 120 JOD purchase, and
order references get a six-character code from an alphabet with no confusable
characters, since it is read aloud on the phone.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 6

```
Phase 6: add merchant subscriptions

Revenue comes from a monthly plan, not commission: the platform holds neither
the goods nor the money, so a per-transaction cut would be unenforceable.

Publishing now needs three independent checks — approved verification, an active
subscription, and room in the plan quota — each with its own message. Verification
is approved before payment is requested, so we never take money from a merchant
we might reject.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 7

```
Phase 7: replace eleven migrations with a single InitialCreate

Production data is not preserved, so the migration history describes a schema
that no longer exists. One migration generated from the settled model is
truthful and makes a fresh database a single command.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 8

```
Phase 8: reduce the listing form to one page and eight fields

Publishing an item took two pages, five sections and about twenty decisions,
because the merchant was asked for a condition grade and a discount reason
separately — two overlapping taxonomies for the same fact.

One condition question now derives both. Options, variants and SKUs are created
automatically and never named in the UI, and submission blockers became inline
validation instead of a list of errors after the fact.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 9

```
Phase 9: reserve instead of checkout, and close every open-ended wait

"Checkout" promised a card payment that does not exist, so buyers hesitated at
the last step. Reserving describes what actually happens.

Nothing now waits on a person indefinitely: unconfirmed reservations expire in
12 hours, uncollected orders in 48, stale pickups in 72. Contact details are
revealed only after confirmation, a WhatsApp deep link replaces in-app chat, and
a published response rate gives ignoring an order a visible cost.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

### Phase 10

```
Phase 10: rebuild demo data for the reduced marketplace

The seeder exercises every role, every condition card and every order state
through the real application services, so nothing bypasses moderation,
authorization, quota enforcement or stock concurrency. A demo that cheats is a
demo that hides bugs.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

## When a phase needs more than one commit

Split by concern, never by file count, and keep every commit green:

```
Phase 2: delete B2B services, entities and views
Phase 2: remove sales-channel fields from Listing
```

---

# Part 2 — Comments

## The rule

> **Comment the reason, never the mechanism.** If a comment restates what the line
> already says, delete it. If a line would make a competent reader ask "why is it done this
> way?", the answer belongs above it.

The code says what happens. Git says who and when. `docs/` says what we are building. A
comment exists for the one thing none of them capture: **why this and not the obvious
alternative.**

## Always comment these

**1. Invariants that look removable.** Anything a future developer could delete as
redundant needs its reason on the line.

```csharp
// Concurrency token. Most listings are a single physical unit, so two buyers
// reserving at the same second is the normal case, not the edge case.
[Timestamp]
public byte[] RowVersion { get; private set; } = [];
```

**2. Snapshot fields.** They look like duplicated data. Say why they are not.

```csharp
// Captured at order time and never recomputed — editing the listing must not
// change what the buyer agreed to.
public decimal UnitPriceSnapshot { get; private set; }
```

**3. Deliberate absences.** When something looks missing, point at the decision so nobody
"fixes" it.

```csharp
// No payment step: the buyer pays cash at pickup. See docs/BUSINESS-MODEL.md §5.
```

**4. Non-obvious constants.** A number with a reason.

```csharp
// 12 hours, not 6: a reservation placed at 11pm must not expire at 5am with the
// shop closed.
public TimeSpan ConfirmationWindow { get; init; } = TimeSpan.FromHours(12);
```

**5. Derived UI mappings.** Where one input fills two fields.

```csharp
// The merchant answers one question; the grade and the discount reason are both
// derived from it. See docs/CORE.md §3.3.
```

## Never write these

| Don't | Why |
|---|---|
| `// increment the counter` | The line says that |
| `// constructor` · `// properties` · `// region` | Structure is visible |
| `// TODO: handle this later` | `docs/PHASE-PLAN.md` is the backlog. A TODO in code is a decision nobody made |
| Commented-out code | Delete it. Git has it, and dead code confuses grep during a deletion project |
| `// Author: …` · `// Modified: …` | Git owns this |
| A comment repeating a doc paragraph | Link the section instead: `See docs/CORE.md §4.2` |

## XML documentation

`<summary>` on:

- public methods on services and interfaces
- behaviour methods on entities (`SubmitForReview`, `Approve`, `DescribeSubmissionBlockers`)
- anything whose name cannot carry the whole contract

Not on: properties, DTOs, view models, private members, or anything where the summary would
just re-read the name.

```csharp
/// <summary>
/// Moves a Draft or Rejected listing into review. Throws if any submission
/// blocker remains — call <see cref="DescribeSubmissionBlockers"/> first to show
/// them to the merchant.
/// </summary>
```

## Language

Comments are **English**, like the UI. Full sentences. Explain the decision, not the
frustration — a comment is read by someone who was not in the conversation.

## During deletion phases

- **Delete, do not comment out.** A commented-out B2B block still matches
  `grep -ri "b2b" src/` and will fail the phase's acceptance criterion.
- **Delete stale comments with their code.** A comment describing a removed rule is worse
  than no comment: the next reader trusts it.
- **When a phase removes a rule, say so where the rule used to be** — one line, if a reader
  would otherwise wonder where it went.
