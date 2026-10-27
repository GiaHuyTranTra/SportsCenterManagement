# UC38 — Class and registration report

`GET /api/reports/classes?from=2026-10-01&to=2026-10-27&registrationStatus=BOOKED&page=1&pageSize=20`

CenterManager only. Dates are inclusive center-calendar **session dates**, not
registration creation timestamps. `to` defaults to today's center date; `from`
defaults to the first day of the end month. Maximum range is 366 days. Optional
filters: positive `classId`, positive `sportId` (Discipline ID), and
`registrationStatus` BOOKED (default), CANCELED or ALL. Page starts at 1;
pageSize is 1–100. Invalid filters return INVALID_CLASS_REPORT_FILTER; invalid
date ranges return INVALID_REPORT_DATE_RANGE in the standard error envelope.

Response includes `{ from, to, timeZoneId, classId, sportId, registrationStatus,
totalClasses, totalSessions, canceledSessions, totalRegistrations, uniqueMembers,
page, pageSize, totalPages, items }`.

Each item contains `{ classId, className, sportId, sportName, sessionCount,
canceledSessionCount, registrationCount, uniqueMemberCount }`.
Rows are sorted by className then classId. Summary counts cover every filtered
class, not just the current page.

- Classes count once if they have a session in range, including archived classes
  and canceled sessions so history is retained. Classes without sessions in the
  selected interval are excluded.
- Sessions include SCHEDULED/COMPLETED/CANCELED. Canceled session counts are
  separately visible; their BOOKED rows never count as valid registrations.
- BOOKED counts registration rows on noncanceled sessions; CANCELED counts
  canceled rows, including canceled sessions. ALL combines these two sets.
- A member booking two sessions contributes two registrations and one distinct
  member within that class. The top-level uniqueMembers is distinct across all
  filtered classes and can be smaller than the sum of per-class member counts.
- Statuses are persisted current states, not a reconstruction of historic
  cancellations. Account deletion/inactivity does not erase registration
  history. No member names, emails, phones, passwords or attendance metrics are
  returned.

EF computes scalar/distinct counts in the database; only the requested page of
aggregate rows is materialized. Existing session/registration indexes are used;
there is no schema or package change. SQLite relational tests compare to an
independent SQL count and cover repeated bookings, distinct members, canceled
session guards, both interval endpoints, out-of-range sessions, archived
classes, empty results, filtering and paging. HTTP tests verify Manager-only
access. UC34 remains assigned to Huy and is untouched.
