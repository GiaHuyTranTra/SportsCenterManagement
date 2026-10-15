# Flow 2 database validation

The upgrade has been prepared but has NOT been executed against the user's SQL Server.
Do not execute it against a shared or production database.

1. Confirm the server, database and development/test ownership.
2. Run `Database/2026-10-14-sprint2-flow2-preflight.sql` (read-only).
3. Review incompatible columns, missing Sprint 1 prerequisites, duplicate names/bookings and counter drift.
4. On a disposable test copy, execute `Database/2026-10-14-sprint2-flow2-schema.sql`.
5. Run `Database/2026-10-14-sprint2-flow2-verify.sql` and inspect the resulting schema.
6. Execute the upgrade again and repeat verification.
7. Repeat with a clean Sprint 1 database and a partial Flow 2 database. `legacy-v1-flow2-schema.sql` is the old fixture, not the repaired upgrade; it assumes its Room/Sprint 1 prerequisites already exist.

The upgrade uses one transaction and a shared application lock. Failed constraints or incompatible required column types roll back the upgrade; it does not silently delete or repair business records. Legacy registration subscription links and notification event links remain nullable because their historical provenance cannot be inferred safely. New commands populate them. The legacy IsRead/EventAt columns remain for compatibility; read state is represented by ReadAt and event identity by EventId.

Live Database First re-scaffolding is pending schema approval/application. The source EF model has been synchronized to the proposed DDL for review and build/tests; it is not evidence that a database was scaffolded. Preserve the partial mappings and existing navigation changes when re-scaffolding.

## SQL Server concurrency tests

Provision a separate database whose name begins with `SCMS_Flow2_Test_`, with the reviewed Sprint 1 and Flow 2 schemas and the four roles. Supply its connection through the local environment variable `SCMS_SQLSERVER_TEST_CONNECTION`. Do not save the value in source control or paste it in reports.

```text
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~Flow2SqlServerTests
```

These tests insert uniquely named data into that test database. They do not create/drop databases or delete history; discard/recreate the test database yourself when appropriate. They cover final capacity, duplicate requests, member overlap, scheduling overlap, booking versus cancellation, repeated cancellation and rollback after audit failure. They are explicitly skipped without the environment variable and must not be described as passing until run on SQL Server.

Application writes currently share one SQL Server transaction-owned lock for the class domain. This favors correctness over write throughput. Account and membership eligibility reads use RepeatableRead; application locks alone do not serialize unrelated writers or direct SQL. Every future class-writing path must use the same scope. Do not introduce direct writes outside that protocol.
