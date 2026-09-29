# SportsCenterManagement Agent Instructions

## Project Context

- This repository is an ASP.NET Core Web API solution targeting .NET 10.
- The persistence stack is Entity Framework Core Database First with SQL Server.
- Dependency injection uses Autofac. API documentation uses NSwag.
- Authentication uses JWT Bearer tokens, BCrypt password hashing, and an
  `IMemoryCache` token blacklist for logout.
- The current account roles are exactly `CenterManager`, `Coach`, `Member`, and
  `Receptionist`.
- The current solution flow is Controller -> Service interface -> Service ->
  `SportsCenterManagementContext` -> SQL Server.
- Do not introduce a DAO, repository, mediator, or generic result abstraction
  unless the user explicitly requests it or the existing codebase adopts it.

## Working Rules

1. Inspect the relevant controller, service interface, service implementation,
   API view model, entity, DbContext mapping, and tests before editing behavior.
2. Keep changes narrowly scoped to the requested use case. Do not refactor
   unrelated code.
3. Follow the existing project boundaries:
   - `APIViewModel`: request and response contracts.
   - `DataAccess`: scaffolded EF Core entities and DbContext.
   - `Services`: business logic and reusable application services.
   - `SportsCenterManagement`: controllers, filters, and application startup.
   - `SportsCenterManagement.Tests`: unit and integration tests.
4. Never use `var` in C# source code or tests. Every local variable, including
   `using`, `await using`, arrays, query results, and `out` variables, must use an
   explicit type. This applies to new code, modified code, restored code, and
   code copied from existing methods. Existing `var` declarations are not a
   precedent to follow.
5. Prefer existing packages and services. Ask before adding a production
   dependency.
6. Do not write comments that merely restate code. Add a short comment only for
   a non-obvious invariant, security decision, or business rule.
7. Never place secrets, connection strings, passwords, or signing keys in source
   code, tests, logs, or documentation.
8. Run the relevant build and tests after changes. Report commands that could
   not be run and the reason.
9. Do not commit, push, reset, or discard changes unless the user explicitly
   asks for that Git operation.

## Model Guidance

Antigravity workspace rules provide context but cannot switch the active IDE
reasoning model. Do not claim that a model was switched automatically. For a
substantial task, classify it using this order and recommend a different model
only when the current selection is unsuitable:

1. Database schema, destructive migration, security architecture, or major
   cross-project design: Claude Opus 4.6 (Thinking).
2. Business logic, authentication, API implementation, tests, and focused bug
   fixes: Claude Sonnet 4.6 (Thinking).
3. Repository-wide audit, dependency analysis, or deep data-flow investigation:
   Gemini 3.1 Pro High.
4. DTOs, repetitive boilerplate, formatting, and documentation: Gemini 3.8
   Flash Medium.

When Antigravity CLI orchestration is used, pin the selected model explicitly
with `agy --model <model-slug>`. In the IDE, the user selects the model from the
model selector.
