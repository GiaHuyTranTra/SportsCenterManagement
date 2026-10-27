# UC39 — CSV report exports (P1)

Manager-only download endpoints:

- `/api/reports/revenue/export`: same from/to/groupBy/paymentMethod as UC36.
- `/api/reports/members/export`: same from/to as UC37.
- `/api/reports/classes/export`: same from/to/classId/sportId/registrationStatus
  as UC38. Exports every matching class, irrespective of screen pagination.

Each endpoint calls the existing report service with the same validation and
business filters as JSON; authorization remains CenterManager only. Bad
filters return the existing JSON error envelope, never a misleading CSV file.
Response is `text/csv; charset=utf-8` with attachment filename, UTF-8 BOM and
CRLF record endings. Dates use yyyy-MM-dd (monthly revenue uses yyyy-MM), VND
is an integer without separators, and numeric formatting is culture invariant.

Revenue has PERIOD rows plus a final TOTAL row. Class export has CLASS rows
plus a final TOTAL row containing global distinct member count. Do not add the
TOTAL row to detail rows when calculating a sum. Member export contains one
aggregate row, no member identities. Per-class unique counts need not sum to
the global unique count because one member can book multiple classes.

The CSV writer quotes every field, doubles embedded quotes and preserves
Vietnamese/embedded commas and line breaks. Text cells beginning with a formula
marker (=, +, -, @), including leading whitespace, or a tab/CR/LF receive an
apostrophe prefix to prevent formula evaluation in spreadsheet applications.
Only aggregate fields already visible in the report are exported; no invoice
IDs, member names, emails, phones or credentials are added.

Revenue remains bounded by the 366-day filter. Class export materializes at
most 10000 aggregate rows; larger matches return 413 REPORT_EXPORT_TOO_LARGE
and instruct the caller to narrow filters. A second bounded fetch guard handles
growth between count and row retrieval. No unbounded raw invoice/registration
data is read into application memory.

Tests cover formula markers/control characters/leading spaces, CSV escaping,
Vietnamese/BOM, invariant money, totals, full class export across screen pages,
all role combinations, anonymous denial, attachment headers and JSON errors.
An oversized relational fixture verifies the 10000-class guard returns 413.

P1 implementation was explicitly requested while UC34 stays assigned to Huy.
This does not mark cash collection or the entire Sprint 3 Definition of Done
as accepted. Frontend export buttons and live SQL Server acceptance remain
separate integration work.

Validation commands: `dotnet build SportsCenterManagement.slnx --no-restore`
and `dotnet test SportsCenterManagement.slnx --no-restore`. Final regression:
239 passed, 8 existing Flow2 SQL Server tests skipped because
`SCMS_SQLSERVER_TEST_CONNECTION` was not provisioned. Production SQL query
translation is tested without a connection; no claim is made of live SQL
Server execution or FE acceptance. Existing CS8620 in ClassRegistrationService
is outside this change set. No production dependency or database schema change.
