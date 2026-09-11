# Faed (فائض)

A marketplace where verified, subscribed merchants in Amman sell **open-box and ex-display**
appliances and power tools. Every listing discloses its condition and the reason for the
discount. Buyers reserve online and pay cash at pickup — the platform never handles money.
Revenue is a monthly merchant subscription, not commission.

This is a university graduation project. There is no live deployment, no payment integration,
and no legal review — see `docs/BUSINESS-MODEL.md` §5 for what that means for money flow, and
`docs/DESIGN-BRIEF.md` §1 for what it means for design priorities.

## Documentation

| File | What it answers |
|---|---|
| `CLAUDE.md` | Repository rules — invariants, layout, what not to add |
| `docs/CORE.md` | Scope — roles, journeys, entities, what's in and out of Phase 1 |
| `docs/BUSINESS-MODEL.md` | Reasoning — money flow, subscriptions, why each rule exists |
| `docs/PHASE-PLAN.md` | Execution — Phases 0–14, back-filled from what the code does |
| `docs/04-DOMAIN-MODEL.md` | Entity reference — every entity, its fields, FKs, indexes and enums |
| `docs/DESIGN-BRIEF.md` | Visual system — locked colour, type and screen designs |
| `docs/DEPLOYMENT.md` | Production configuration, the R2 and Brevo infrastructure, release checklist |
| `docs/DEMO-SCRIPT.md` | The screen-by-screen walkthrough for the defence |
| `docs/REHEARSAL-CHECKLIST.md` | The same walkthrough, timed, one row per click, for the defence machine |
| `docs/CREDITS.md` | Photo sources and CC-BY / CC-BY-SA attribution for the demo images |
| `docs/COMMITS-AND-COMMENTS.md` | How commits are written and what a comment is for |
| `docs/B2B-DESIGN.md` | The removed merchant-to-merchant module, archived for a later phase |
| `docs/AUDIT-PHASE-14.md` | The Phase 14 final audit and its outcome |

Start with `docs/CORE.md` §1 for the one-sentence acceptance test every feature is measured
against.

## Stack

ASP.NET Core MVC on .NET 10, EF Core, SQL Server, ASP.NET Core Identity, Razor views,
Bootstrap 5, vanilla JavaScript. No frontend framework, no build step.

## Branch

All Phase 1 work happens on `faed-core`. `main` holds an earlier two-sided version and is not
touched.

## Commands

```bash
dotnet build Faed.slnx
dotnet test                                        # tests/Faed.Web.Tests
dotnet run --project src/Faed.Web
dotnet ef migrations add <Name> --project src/Faed.Web
dotnet ef database update --project src/Faed.Web
dotnet ef database drop -f --project src/Faed.Web
```

The app does not migrate on startup — it seeds roles and reference data idempotently on run.

## Status

**Phase 14 (final audit and defence readiness) — in progress.** Phases 0–13 are complete:
B2B and disputes removed, one clean migration, subscriptions and the three-check publish gate,
the one-page listing form, the Reserve flow, the five public screens rebuilt to the locked
visual system, and a demo seed with real product photography. Phase 14 removes what is dead,
regenerates the docs from the code, and rehearses the walkthrough; its findings and outcome
are in `docs/AUDIT-PHASE-14.md`.

## Running it locally

```bash
dotnet ef database update --project src/Faed.Web    # create the LocalDB database
dotnet run --project src/Faed.Web                    # seeds roles + reference data on start
```

Then browse `https://localhost:5001`. To load the demo scenario, set these before `dotnet run`
(user secrets or environment variables):

```
Faed__DemoSeed__Enabled  = true
Faed__DemoSeed__Password = Demo!Pass123
```

## Demo accounts

`DemoDataSeeder` builds the whole scenario — one order in every lifecycle state, a review, a
sold-out listing, a listing still in moderation — by calling the same application services a
real user would, so nothing bypasses moderation, authorization, quota or concurrency. It runs
in **Development only**. Product and defect photography comes from `tools/demo-images/`
(licences in `docs/CREDITS.md`).

**Every account below is Development-only seed data. The shared password is `Demo!Pass123`.**

| Role | Email | State |
|---|---|---|
| Admin | `demo-admin@faed.local` | Reviews verification, moderation, subscriptions |
| Merchant — Amman Kitchen Co. | `merchant-a@faed.local` | Verified, Standard plan, full order history |
| Merchant — Petra Power Tools | `merchant-b@faed.local` | Verified, Basic plan, one listing still in moderation |
| Merchant — Sahara Appliance Outlet | `unsubscribed-merchant@faed.local` | Verified but no subscription — sees the plan chooser |
| Merchant — Rainbow Home Essentials | `pending-merchant@faed.local` | Documents submitted, awaiting admin approval |
| Buyer — Layla Haddad | `buyer-a@faed.local` | Orders in Pending, Ready, Cancelled, Completed |
| Buyer — Omar Nasser | `buyer-b@faed.local` | Orders in Confirmed, No-show, plus a 5★ review |

`docs/DEMO-SCRIPT.md` is the screen-by-screen walkthrough; `docs/REHEARSAL-CHECKLIST.md` is
the timed three-role run to do by hand on the defence machine.
