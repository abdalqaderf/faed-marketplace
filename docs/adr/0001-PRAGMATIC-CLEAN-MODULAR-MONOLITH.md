# ADR 0001 — Pragmatic Clean Modular Monolith

## Status
Accepted.

## Decision
Use one deployable ASP.NET Core application split into Domain, Application, Infrastructure, and Web projects.

## Why
- MVP does not justify microservices.
- Strong project boundaries improve maintainability/testing.
- Keeps architecture understandable and portfolio-quality.

## Consequences
- One database.
- One application deployment.
- No distributed transaction/event infrastructure.
