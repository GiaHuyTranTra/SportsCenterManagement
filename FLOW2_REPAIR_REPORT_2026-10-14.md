# Flow 2 repair report — 14/10/2026

Branch: `codex/integrate-minhbe-master`. Existing staged integration and Antigravity changes are preserved. No commit/push/reset, dependency installation, live database update or live re-scaffolding was performed.

## Completed in source

- Replaced the incomplete Flow 2 DDL with a transactional upgrade that creates missing Room/Flow 2 tables, independently adds missing columns, validates supported column shapes, rebuilds named constraints WITH CHECK and adds indexes. Incompatible schema/data aborts and rolls back; business records are not deleted or silently repaired.
- Added read-only preflight and verification scripts. Added persisted normalized-name keys and rowversion, a booking subscription link, operation/event identifiers, notification ReadAt/EventId and audit class/session/operation links. Legacy provenance links remain nullable; new commands populate them.
- Membership eligibility uses Sprint 1 `CONFIRMED`, account role/status/deletion/lock, suspension, session date and package sport permissions. Booking and rescheduling share the validator. Class/sport changes with unchanged times revalidate booked members.
- All Flow 2 commands acquire the same SQL Server transaction-owned application lock BEFORE validation, using RepeatableRead. Includes scheduling, coach assignment, registration/cancellation, class/room capacity/status, disciplines and entitlement updates. This intentionally serializes writes; it favors correctness over throughput.
- Scheduling/assignment share a conflict validator; intervals are half-open. Registration updates counter conditionally, checks bookable status, stores the subscription ID and reloads SQL-updated records before returning counters. Cancellation transitions registration first and decrements once.
- Notification generation stages events/recipients into the caller's transaction. Unique identity is operation + session + event type, then event + recipient. Room-only changes and coach-only changes notify members/coaches; real repeated changes at the same session time create distinct events. Exact update retries are no-ops. Read state uses ReadAt while retaining legacy IsRead compatibility.
- Legacy ScheduleQuery now delegates to canonical query services. Available sessions require authentication and OPEN/future/SCHEDULED filtering. Registration `/me` is paged, accepts date filters, roster accepts search, Session DTO includes cancellation/update fields, and actor display fields use profile names.
- Audit supports class/session/entity/actor/search and new date aliases while keeping old parameters; conflicting aliases are rejected. `/Sport` records Unified action names; legacy `/Discipline` retains its existing audit labels.
- Room deactivation keeps existing schedules. Capacity reduction still validates future schedules. Date/time parsing is strict and SQL concurrency exceptions return a controlled conflict response.

## Validation

Build command:

```text
dotnet build SportsCenterManagement.slnx --no-restore --verbosity quiet -m:1 -p:UseSharedCompilation=false
```

Test command:

```text
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --no-build --no-restore --verbosity quiet
```

Verified handoff result: build clean; 158 passed, 0 failed, 7 explicitly skipped SQL Server tests, 165 total. The original 142-test suite passed after repairs; new regression/HTTP cases cover confirmed membership, invalid statuses, booking history pagination, room-only notifications, coach changes at the same time, entitlement changes without time changes and protected legacy routes.

HTTP integration exercises create Sport → Room → Class DRAFT → Session → Class OPEN → Member booking → Member cancellation/retry. It uses WebApplicationFactory + InMemory, not a live SQL Server or external SMTP/Postman server.

Seven opt-in SQL Server tests cover final capacity, duplicate booking, member overlap, schedule conflicts, booking versus cancellation, repeated cancellation and audit-failure rollback. They require an explicitly provisioned database named `SCMS_Flow2_Test_*` supplied through a local environment variable. They were NOT run against SQL Server. No claim of database/concurrency acceptance is made from InMemory results.

## Still required before acceptance

1. Confirm the development SQL Server/instance and database. Do not apply the upgrade to an unconfirmed/shared database.
2. Run preflight, review data/schema conflicts, test the upgrade on a disposable copy, run verification, then repeat the upgrade to check re-runnability.
3. Test a clean Sprint 1 baseline and a partial Flow 2 baseline. The old legacy SQL fixture is retained; it is not the repaired upgrade.
4. Apply the approved schema and re-scaffold affected model files carefully. Source mappings were synchronized to the proposed DDL, not generated from a live updated database.
5. Run the seven SQL Server tests and verify locking, FK/check constraints, raw SQL and rollback behavior on the target engine.
6. Reconnect FE to Unified routes/direct paged payloads and execute SQL-backed HTTP/Postman/FE tests. Verify OTP/SMTP separately.

Do not deploy this source against the old database schema: new mapped columns/tables require the reviewed upgrade first. Package entitlement changes apply to subsequent booking; rescheduling rechecks current eligibility. Direct SQL and future writers must not bypass the class-domain locking protocol. Client stale-edit version preconditions are not added to the Unified request contract in this repair; rowversion protects tracked database updates, not arbitrary stale UI payloads.

See `Database/tests/flow2/README.md` for provisioning/validation instructions. Sprint 2 is repaired in source but remains pending SQL Server and FE acceptance.
