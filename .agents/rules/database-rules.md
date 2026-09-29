---
trigger: model_decision
description: "Apply for SQL Server schema work, EF Core entities, DbContext mappings, queries, transactions, and persistence behavior."
---

# Database and EF Core Rules

## Database First

- This project uses SQL Server and EF Core Database First.
- Treat the database schema as the source of truth for generated entities and
  `SportsCenterManagementContext` mappings.
- For a schema change, update or provide the SQL schema change first, then
  re-scaffold the affected EF Core model. Do not invent Code First migrations
  unless the user explicitly changes the project strategy.
- Avoid placing custom business logic in scaffolded entity or DbContext files.
  Use partial classes or services when custom behavior is needed.
- Before re-scaffolding, identify generated files that may be overwritten and
  preserve intentional user changes.

## Queries and Integrity

- Use EF Core LINQ for normal queries. Do not add a DAO or repository layer to
  wrap `SportsCenterManagementContext` without an explicit requirement.
- EF Core parameterizes LINQ queries. If raw SQL is genuinely required, use EF
  Core parameter APIs and never concatenate user input into SQL.
- Use `AnyAsync` for existence checks and `AsNoTracking` for read-only queries
  when entity tracking is unnecessary.
- Avoid N+1 queries and loading entire tables when a filtered or projected query
  is sufficient.
- Enforce important uniqueness and referential integrity in SQL Server as well
  as in application validation. Current unique account data includes email, and
  member code is unique for members. Check the live schema before relying on
  additional constraints such as phone uniqueness.
- A single `SaveChangesAsync` is already transactional. Use an explicit
  transaction only when multiple saves or coordinated persistence operations
  must succeed or fail together.
- Never delete or overwrite financial, audit, booking, or historical records
  without first checking the domain retention rule and foreign-key impact.
