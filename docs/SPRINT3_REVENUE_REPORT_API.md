# UC36 — Revenue report

`GET /api/reports/revenue?from=2026-10-01&to=2026-10-27&groupBy=DAY&paymentMethod=CASH`

CenterManager only. `from`/`to` are inclusive center-calendar dates; `to`
defaults to today in `BusinessSettings:TimeZoneId`, and `from` defaults to the
first day of the end month. Limit: 366 days. `groupBy` is DAY (default) or MONTH.
SQL Server converts each UTC `paidAt` with AT TIME ZONE before grouping.

Sprint 3 revenue decision: only CASH invoices in PAID state with a nonnull
`paidAt` count. CARD/BANK_TRANSFER have no reconciliation evidence and are
rejected as report filters until a separately reviewed reconciliation flow
exists. The amount is the invoice's collected `amount`, including upgrade
credit adjustments, never the current or original package list price.
Pending/canceled invoices and invoices without a paid timestamp do not count.
Invoice creation date and the current member account state do not determine
financial recognition. No invoice/subscription join can multiply the amount.

Response: `{ from, to, timeZoneId, groupBy, paymentMethod, currency: "VND",
totalRevenue, paidInvoiceCount, items: [{ period, paymentMethod, revenue,
invoiceCount }] }`. Daily period is yyyy-MM-dd; monthly period is yyyy-MM.
Missing periods have zero revenue/count. Partial-month buckets only count the
requested days. Totals are derived from the same grouped SQL result as buckets.
Only aggregate rows are materialized; no invoice identities or member PII.

400 errors use the standard `{ success, error: { code, message, details },
traceId }` envelope: INVALID_REPORT_DATE_RANGE, INVALID_REPORT_GROUPING,
UNSUPPORTED_REPORT_PAYMENT_METHOD, or VALIDATION_ERROR for binding failures.

Relational SQLite tests compare the result to an independent SUM query, and
cover register/renew/upgrade amounts, pending/canceled/unreconciled methods,
missing paidAt, center midnight, month/year boundaries, zero buckets and bad
filters. HTTP tests enforce Manager access. Production SQL translation tests
also compile the actual DAY/MONTH query and assert database aggregation and
AT TIME ZONE without opening a connection. SQL Server timezone execution still
requires a dedicated SQL Server test database; SQLite uses fixed-offset zones.

UC34 cash-confirmation code is unchanged and remains assigned to Huy. This
report does not assert that UC34 concurrency/collection acceptance is complete.
