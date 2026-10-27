# Sprint 3 — Phúc backend handoff

Scope: UC33 counter member registration, UC35 invoice search/history/receipt,
UC37 member report. Base BE: `e727064`; reviewed FE: `b078598` in
https://github.com/NakinoMinh/Sports-Center-Management-FE.

## UC33 — atomic counter registration

`POST /api/Member/counter-registration`

Roles: Receptionist and CenterManager, matching the existing policy. The server
also checks the persisted actor role, active/deleted state and lock state.

Request fields: `fullName`, `email`, `phone`, `dateOfBirth` (`yyyy-MM-dd`),
`packageId` (positive integer), `expectedPrice` (positive decimal),
`paymentMethod` (`CASH`, `BANK_TRANSFER`, `CARD`). Do not send a password.

The server reads the active package price. `expectedPrice` detects a stale quote;
it never sets the invoice amount. The account, member, pending subscription and
pending invoice commit in one transaction. Selecting a payment method does not
activate access or record collection. UC34 cash collection remains Huy's scope.

201 response: `{ member, receipt, emailDelivery }`, retaining the existing member
and receipt DTOs. `receipt.invoiceStatus` and `subscriptionStatus` are both
`PENDING_PAYMENT`; paid timestamps/collector are null.

**Credential contract change:** the response no longer contains
`initialPassword`. A generated password stays on the server and is used only
for hashing and welcome-email delivery after commit. `emailDelivery` is `SENT`,
`NOT_CONFIGURED`, or `FAILED`. A mail failure leaves the successfully created
pending order intact; do not repeat registration. Use the existing password
recovery flow if delivery is unavailable. Do not display/store generated
credentials in FE.

Errors use `{ success: false, error: { code, message, details }, traceId }`:

| HTTP | Codes |
| --- | --- |
| 400 | `VALIDATION_ERROR`, `INVALID_MEMBER_DATA`, `INVALID_PAYMENT_METHOD` |
| 401 | `UNAUTHORIZED` |
| 403 | `FORBIDDEN`, `STAFF_INACTIVE` |
| 404 | `STAFF_NOT_FOUND`, `PACKAGE_NOT_FOUND` |
| 409 | `DUPLICATE_EMAIL`, `DUPLICATE_PHONE`, `PACKAGE_INACTIVE`, `PRICE_CHANGED`, `PENDING_ORDER_EXISTS`, `CONCURRENCY_CONFLICT` |
| 423 | `STAFF_LOCKED` |
| 500 | `INTERNAL_ERROR` |

SQL Server email/phone unique violations from racing registrations map to the
same duplicate errors as preflight validation. The existing unique indexes and
transaction isolation are retained; no schema changes are required.

## UC35 — invoice history

The existing `GET /api/MembershipInvoice` keeps its array response for the
current FE `membershipApi.listInvoices`. The new paginated endpoint is:

`GET /api/MembershipInvoice/search?search=INV-&status=PAID&paymentMethod=CASH&page=1&pageSize=20`

Optional filters: `memberId`, `search`, `status`, `paymentMethod`. Search matches
invoice number, member name/code, email or phone. Status is `PENDING_PAYMENT`,
`PAID` or `CANCELED`; method is `CASH`, `CARD` or `BANK_TRANSFER`. Omit a filter
to include every value. Page starts at 1; pageSize is 1–100.

Response: `{ items: MembershipReceipt[], page, pageSize, totalItems, totalPages }`.
Sorting is `createdAt DESC, invoiceId DESC`, including ties. Count and paging
run in the database; the count respects the same ownership and filters.
Invalid filters/pagination return 400 `INVALID_INVOICE_FILTER` with the standard
error envelope.

Member requests are always restricted to their own account, including when
they supply another `memberId`. Receptionist/CenterManager can filter members.
Coach is denied. Deleted, inactive, locked or unsupported callers receive no
invoice data even when the service is invoked outside the controller.

`GET /api/MembershipInvoice/{invoiceId}/receipt` remains available only for PAID
invoices, with collector, paid time and subscription/package snapshots.
Pending/canceled invoices appear in history; they are not receipts for collected
money. A Member requesting another member's receipt receives 404
`INVOICE_NOT_FOUND`. Other receipt errors now have the standard envelope:
`RECEIPT_NOT_AVAILABLE`, `ACCOUNT_NOT_FOUND`, `FORBIDDEN`, `ACCOUNT_INACTIVE`,
`ACCOUNT_LOCKED`, `DATA_INTEGRITY_ERROR`, `INTERNAL_ERROR`.

## UC37 — member report

`GET /api/reports/members?from=2026-10-01&to=2026-10-27`

CenterManager only. `from`/`to` are optional `yyyy-MM-dd` calendar dates, not
timestamps. `to` defaults to today's date in `BusinessSettings:TimeZoneId`.
Omitted `from` means all creation history through `to`. A reversed range or a
`to` with no representable following day returns 400 `INVALID_REPORT_DATE_RANGE`.

The center-day interval is `[from 00:00, day-after-to 00:00)` converted to UTC.
Response includes `from`, `to`, `asOfDate`, `timeZoneId` and these aggregate counts:

| Field | Definition |
| --- | --- |
| `totalMembers` | Member profiles linked to Member-role, nondeleted accounts, created before the end of `to`. |
| `newMembers` | Those profiles created within the filtered center-day interval. |
| `activeMembers` | Active, unlocked accounts with a current CONFIRMED, nonsuspended membership on `asOfDate`. |
| `expiredMembers` | No current or future CONFIRMED membership, but a CONFIRMED membership ended before `asOfDate`. |
| `suspendedMembers` | The current CONFIRMED membership is suspended. |
| `upcomingMembers` | No current membership, but a future CONFIRMED membership exists. |
| `pendingPaymentMembers` | No current, future or expired CONFIRMED history, but a PENDING_PAYMENT order exists. |
| `membersWithoutSubscription` | No qualifying CONFIRMED or PENDING_PAYMENT subscription; canceled/replaced history alone does not qualify. |
| `inactiveAccounts` | Member accounts that are inactive or locked. This overlaps membership-state metrics. |

Current membership selection follows the existing member-status API:
`startDate DESC, id DESC` among CONFIRMED rows covering the day. Member counts
are distinct profiles, never subscription counts. Subscriptions created after
the report's end are excluded. `from` filters new-member creation, not all
membership states. Metric totals are not additive (`inactiveAccounts` overlaps;
inactive/locked accounts cannot contribute to `activeMembers`).

This report uses persisted current statuses and membership validity dates; it
is not a reconstruction of past status changes from audit events. It exposes
no names, emails, phones or credentials. Aggregation runs in EF/SQL rather than
loading member lists into FE. There is no revenue or attendance metric here.

## FE integration observations

The reviewed remote FE currently calls `memberApi.createMember`, then
`membershipApi.counterRegisterOrRenew`, and generates/displays a password in
`CounterRegistrationForm`. Long must replace these two writes with the single
UC33 endpoint and consume `emailDelivery` without credential UI. Keep amounts
and statuses from the returned backend receipt. The old two-request workflow
cannot guarantee atomic registration.

Long can migrate invoice listing from its current array endpoint to `/search`
and consume the paging metadata. Tam can call `/api/reports/members` for UC37;
no report endpoint was found in the reviewed FE. Translate the error codes
through the existing FE error map. This change set edits BE only; it does not
claim FE/UI acceptance or remove FE demo adapters.

## Validation

- Build: `dotnet build SportsCenterManagement.slnx --no-restore`.
- Regression: `dotnet test SportsCenterManagement.slnx --no-build --no-restore`.
- SQLite integration coverage: injected invoice-insert failure rolls back every
  counter-registration row; SMTP failure preserves a committed order; response
  contains no credential; list filters, page ties, ownership and paid receipt.
- Report tests use SQLite relational queries, overlapping/history memberships,
  pending/canceled/suspended/inactive states, inclusive Vietnam-midnight
  boundaries, empty results and reversed dates.
- HTTP tests cover authentication, role restrictions, DTO binding and error
  envelopes using the application's existing test host.
- The 8 existing Flow2 SQL Server tests require
  `SCMS_SQLSERVER_TEST_CONNECTION` pointing to an explicitly provisioned test
  database. They remain skipped when it is absent. No production database was
  modified and SQL Server concurrency was not exercised by SQLite tests.
- Existing warning: CS8620 in `Services/ClassRegistrationService/ClassRegistrationService.cs:389`.
- No new production dependency or schema change.
