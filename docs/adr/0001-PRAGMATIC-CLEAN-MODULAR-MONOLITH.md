# ADR 0001 — Pragmatic Clean Modular Monolith

## Status
Superseded in part by `docs/adr/0006-SINGLE-PROJECT-MVC.md`.

The "one deployable ASP.NET Core application, one database, no distributed
transaction/event infrastructure" decision still holds. The specific split into separate
`Faed.Domain` / `Faed.Application` / `Faed.Infrastructure` / `Faed.Web` projects has been
replaced by a single organized `Faed.Web` project.

## Decision
Use one deployable ASP.NET Core application.

Originally this application was split into Domain, Application, Infrastructure, and Web
projects. As of ADR 0006 it is a single ASP.NET Core MVC project organized by folder
(`Models`, `Data`, `Services`, `Areas`, `ViewModels`).

## Why
- MVP does not justify microservices.
- Keeps architecture understandable and portfolio-quality.

## Consequences
- One database.
- One application deployment.
- No distributed transaction/event infrastructure.
