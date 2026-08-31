Read and follow `/AGENTS.md` before generating or modifying code.
Use `/docs` as the product/engineering specification and `/tasks` as the implementation queue.
Do not invent business rules that are listed as unresolved in `docs/13-OPEN-QUESTIONS.md`.

Faed has one production project: `src/Faed.Web`. Keep entities, EF Core, services,
controllers, Areas, and ViewModels organized inside that project. Do not create separate
Domain, Application, or Infrastructure projects, and do not introduce Repository Pattern,
UnitOfWork, CQRS, or MediatR unless an authoritative future requirement explicitly
justifies it. See `docs/adr/0006-SINGLE-PROJECT-MVC.md`.
