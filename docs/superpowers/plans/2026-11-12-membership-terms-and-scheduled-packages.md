# Membership Terms and Scheduled Packages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require auditable member-owned terms acceptance and make renewals/upgrades/downgrades immutable, full-price subscriptions scheduled after the current paid period.

**Architecture:** Add versioned terms acceptance and per-subscription discipline snapshots to the database, expose a focused terms service/controller, and enforce acceptance plus scheduling in the existing subscription/payment services. Split counter account creation from package ordering so a receptionist can create an account but only that member can accept terms. FE reads the same terms API, gates ordering, and presents all future package changes without proration.

**Tech Stack:** ASP.NET Core/.NET 10, EF Core 10, SQL Server/SQLite tests, xUnit, React 19, TypeScript 6, Vite 8, Vitest 5.

**Spec:** `docs/superpowers/specs/2026-11-12-membership-terms-scheduled-package-coach-homework-design.md`

## Global Constraints

- Backend is the sole authority for order kind, dates, amount, acceptance and package eligibility.
- Only the authenticated `Member` account may accept its current terms; staff cannot accept on its behalf.
- Current terms use an immutable version plus SHA-256 content hash; changing content requires a new version.
- Paid/current subscriptions are never shortened, canceled, repriced or re-entitled.
- A pending order pays the captured package price/duration/discipline snapshot even if the catalog changes later; an inactive package may still block collection under the existing rule.
- At most one pending order and one paid future subscription may exist for a member.
- `RENEW`, `UPGRADE`, `DOWNGRADE`, and same-price `CHANGE` all start after the latest confirmed `EndDate` and charge the full snapshot price.
- Pending unpaid invoices may be canceled; `PAID`/`CONFIRMED` subscriptions may not.
- Existing user-created untracked FE files must remain untouched.

## Review Focus

- A stale terms dialog must fail with `TERMS_VERSION_MISMATCH`, never record acceptance for changed content (Task 2 tests).
- A package with no sport restrictions must remain unrestricted after snapshotting; an empty snapshot must not be confused with missing data (Tasks 1 and 4 tests).
- Two simultaneous order requests must not create two pending/future rows (Task 4 SQL/transaction tests).
- Same-price different-package selection must be `CHANGE`, not `RENEW`, and must start next period (Tasks 4 and 7 tests).
- Vietnamese business date boundaries must use `BusinessSettings:TimeZoneId`, including orders placed around UTC midnight (Tasks 4 and 5 tests).

---

### Task 1: Persistence for terms acceptance and package entitlement snapshots

**Files:**
- Create: `DataAccess/Entities/MembershipTermAcceptance.cs`
- Create: `DataAccess/Entities/MemberSubscriptionDiscipline.cs`
- Modify: `DataAccess/Entities/MemberSubscription.cs`
- Modify: `DataAccess/Entities/SportsCenterManagementContext.cs`
- Create: `Database/2026-11-12-membership-terms-scheduled-packages.sql`
- Test: `SportsCenterManagement.Tests/MembershipTermsPersistenceTests.cs`

**Interfaces:**
- Produces: `DbSet<MembershipTermAcceptance> MembershipTermAcceptances`.
- Produces: `DbSet<MemberSubscriptionDiscipline> MemberSubscriptionDisciplines`.
- Produces: `MemberSubscription.HasDisciplineRestrictions: bool` and `MemberSubscription.SubscriptionDisciplines`.
- `MembershipTermAcceptance` fields: `Id`, `MemberId`, `TermsVersion`, `ContentHash`, `AcceptanceChannel`, `AcceptedAtUtc`.
- `MemberSubscriptionDiscipline` composite key: `(SubscriptionId, DisciplineId)`; snapshot `DisciplineName` for audit/display.

- [ ] **Step 1: Write failing persistence tests**

Add tests named:

- `Acceptance_UniquePerMemberAndVersion()` asserting a duplicate `(MemberId, TermsVersion)` fails.
- `SubscriptionDiscipline_DeletesWithSubscription()` asserting snapshot children cascade with a deleted unpaid subscription.
- `UnrestrictedSubscription_IsExplicitEvenWithNoDisciplineRows()` asserting `HasDisciplineRestrictions == false` is distinguishable from a restricted subscription.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~MembershipTermsPersistenceTests`

Expected: FAIL because the entities/DbSets do not exist.

- [ ] **Step 3: Implement entities and EF mappings**

Use exact SQL limits: member/version/hash/channel strings `400/80/64/30`; discipline snapshot name `100`; `AcceptedAtUtc` defaults to `sysutcdatetime()`; unique index `(MemberId, TermsVersion)`; FK acceptance to `Member`; FK discipline snapshot to `MemberSubscription` with cascade.

The SQL migration must:

1. preflight duplicate `PENDING_PAYMENT` rows and multiple future confirmed rows, throwing with identifying member IDs instead of deleting data;
2. create both tables and indexes idempotently by object name;
3. add `HasDisciplineRestrictions bit NOT NULL DEFAULT 0`;
4. backfill existing subscriptions from the current `MembershipPackageSport` mappings and set the flag when mappings exist.

- [ ] **Step 4: Run focused persistence tests**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~MembershipTermsPersistenceTests`

Expected: PASS.

- [ ] **Step 5: Smoke-test the migration against local SQL Server**

Run the script against the Docker `SportsCenterManagement` database with `sqlcmd`, then query `MembershipTermAcceptance`, `MemberSubscriptionDiscipline`, and `COL_LENGTH('MemberSubscription','HasDisciplineRestrictions')`.

Expected: both tables exist and the column returns non-null metadata; running the script a second time performs no duplicate DDL.

- [ ] **Step 6: Commit BE schema work**

```powershell
git add DataAccess/Entities/MembershipTermAcceptance.cs DataAccess/Entities/MemberSubscriptionDiscipline.cs DataAccess/Entities/MemberSubscription.cs DataAccess/Entities/SportsCenterManagementContext.cs Database/2026-11-12-membership-terms-scheduled-packages.sql SportsCenterManagement.Tests/MembershipTermsPersistenceTests.cs
git commit -m "feat: persist membership terms and entitlement snapshots"
```

### Task 2: Versioned membership terms API

**Files:**
- Create: `APIViewModel/MembershipTerms/CurrentMembershipTermsAPIViewModel.cs`
- Create: `APIViewModel/MembershipTerms/AcceptMembershipTermsAPIViewModel.cs`
- Create: `APIViewModel/MembershipTerms/MemberTermsStatusAPIViewModel.cs`
- Create: `Services/MembershipTermsService/IMembershipTermsService.cs`
- Create: `Services/MembershipTermsService/MembershipTermsCatalog.cs`
- Create: `Services/MembershipTermsService/MembershipTermsService.cs`
- Create: `SportsCenterManagement/Controllers/MembershipTermsController.cs`
- Modify: `SportsCenterManagement/Program.cs`
- Test: `SportsCenterManagement.Tests/MembershipTermsServiceTests.cs`
- Test: `SportsCenterManagement.Tests/MembershipTermsHttpTests.cs`

**Interfaces:**
- `MembershipTermsCatalog.CurrentVersion = "membership-terms-v1"`.
- `MembershipTermsCatalog.CurrentHash` is lowercase SHA-256 hex of the exact canonical UTF-8 terms content.
- The current DTO contains `sections` plus required acknowledgements with exact IDs `PACKAGE_NON_CANCELABLE`, `NO_MID_TERM_CHANGE`, `SCHEDULED_NEXT_PERIOD`, and `CENTER_RULES_AND_SAFETY`; FE renders the Vietnamese labels supplied by BE rather than duplicating them.
- `Task<CurrentMembershipTermsAPIViewModel> GetCurrentAsync(string memberId)`.
- `Task<AcceptMembershipTermsResult> AcceptCurrentAsync(string memberId, string version, string contentHash)`.
- `Task<MemberTermsStatusAPIViewModel> GetMemberStatusAsync(string memberId)` for staff status display only.
- `GET /api/MembershipTerms/current`, `POST /api/MembershipTerms/current/accept`, `GET /api/MembershipTerms/members/{memberId}/status`.

- [ ] **Step 1: Write failing service tests**

Cover exact assertions:

- current response returns version/hash/sections and `accepted=false` before a record exists;
- `AcceptCurrentAsync` uses the authenticated member ID, stores `MEMBER_WEB`, and is idempotent;
- wrong version or hash returns `VersionMismatch` and writes nothing;
- inactive/locked/deleted/non-member callers return `MemberNotEligible`;
- staff status returns only acceptance version/time, never terms acceptance mutation capability.

- [ ] **Step 2: Run service tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~MembershipTermsServiceTests`

Expected: FAIL because the service/catalog are missing.

- [ ] **Step 3: Implement catalog and service**

Canonicalize the catalog once; never accept a hash supplied without matching both current version and current hash. Return enum values `Success`, `VersionMismatch`, `MemberNotEligible`, `MemberNotFound`.

- [ ] **Step 4: Run service tests and verify GREEN**

Run: same command; expected PASS.

- [ ] **Step 5: Write failing authorization/contract HTTP tests**

Assert:

- Member can GET and POST only for itself.
- Receptionist/Manager POST receives 403.
- Receptionist/Manager can GET `/members/{id}/status`.
- Member cannot GET another member's staff status route.
- version mismatch returns HTTP 409 code `TERMS_VERSION_MISMATCH`.

- [ ] **Step 6: Implement controller and Autofac registration**

Use the existing `ApiErrorResponseAPIViewModel` envelope and stable error codes. Add `MembershipTermsService` registration in `Program.cs`.

- [ ] **Step 7: Run HTTP tests and commit**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~MembershipTermsHttpTests`

Expected: PASS.

```powershell
git add APIViewModel/MembershipTerms Services/MembershipTermsService SportsCenterManagement/Controllers/MembershipTermsController.cs SportsCenterManagement/Program.cs SportsCenterManagement.Tests/MembershipTermsServiceTests.cs SportsCenterManagement.Tests/MembershipTermsHttpTests.cs
git commit -m "feat: add auditable membership terms acceptance"
```

### Task 3: Split counter account creation from package ordering

**Files:**
- Create: `APIViewModel/Member/CounterCreateMemberAPIViewModel.cs`
- Create: `APIViewModel/Member/CounterCreateMemberResponseAPIViewModel.cs`
- Delete: `APIViewModel/MemberSubscription/CounterRegisterMemberAPIViewModel.cs`
- Delete: `APIViewModel/MemberSubscription/CounterRegisterMemberResponseAPIViewModel.cs`
- Modify: `Services/MemberSubscriptionService/IMemberSubscriptionService.cs`
- Modify: `Services/MemberSubscriptionService/MemberSubscriptionService.cs`
- Modify: `Services/EmailService/IEmailService.cs`
- Modify: `Services/EmailService/EmailService.cs`
- Modify: `SportsCenterManagement/Controllers/MemberController.cs`
- Modify: `SportsCenterManagement.Tests/MemberSubscriptionServiceTests.cs`

**Interfaces:**
- Replace the atomic counter method with `Task<(CounterCreateMemberResult Result, CounterCreateMemberResponseAPIViewModel? Data)> CreateMemberAccountAtCounterAsync(string staffAccountId, CounterCreateMemberAPIViewModel request)`.
- Request fields: `FullName`, `Email`, `Phone`, `DateOfBirth`; no package, price or payment fields.
- Response fields: `Member`, `EmailDelivery`; no invoice or subscription.
- Add `IEmailService.SendMemberAccountWelcomeAsync(string recipientEmail, string fullName, string initialPassword)`.
- Keep `POST /api/Member/counter-registration` route for FE compatibility, but change its contract to account creation only.

- [ ] **Step 1: Rewrite existing counter tests to fail against the atomic implementation**

Tests must assert account/member/audit are committed without any `MemberSubscription` or `MembershipInvoice`, and rollback leaves no account/member when account creation fails. Remove assertions that account creation generates a pending order.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~MemberSubscriptionServiceTests`

Expected: failures on old invoice/subscription behavior.

- [ ] **Step 3: Extract account-only transaction and welcome email**

Preserve duplicate email/phone, age, role, staff status/lock, transaction rollback, audit and delivery-status behavior. Do not reuse placeholder package values in the welcome email.

- [ ] **Step 4: Update controller contract and run focused tests**

Run the same test command; expected PASS.

- [ ] **Step 5: Commit the BE counter split**

```powershell
git add APIViewModel/Member Services/MemberSubscriptionService Services/EmailService SportsCenterManagement/Controllers/MemberController.cs SportsCenterManagement.Tests/MemberSubscriptionServiceTests.cs
git commit -m "refactor: separate member creation from package ordering"
```

### Task 4: Enforce terms and schedule every package change after the current period

**Files:**
- Modify: `Services/MemberSubscriptionService/IMemberSubscriptionService.cs`
- Modify: `Services/MemberSubscriptionService/MemberSubscriptionService.cs`
- Modify: `SportsCenterManagement/Controllers/MemberSubscriptionController.cs`
- Modify: `SportsCenterManagement.Tests/MemberSubscriptionServiceTests.cs`
- Create: `SportsCenterManagement.Tests/ScheduledSubscriptionConcurrencyTests.cs`

**Interfaces:**
- Inject `IMembershipTermsService` into `MemberSubscriptionService`.
- Add `TermsAcceptanceRequired` to `RegisterSubscriptionResult` and `CounterRegisterResult`.
- Replace private quote result with `SubscriptionOrderQuote(string Kind, DateOnly StartDate, decimal Amount)`.
- Exact kinds: `REGISTER`, `RENEW`, `UPGRADE`, `DOWNGRADE`, `CHANGE`.
- Copy package disciplines into `MemberSubscription.SubscriptionDisciplines` at order creation and set `HasDisciplineRestrictions`.

- [ ] **Step 1: Add failing quote/enforcement tests**

Test names and assertions:

- `OrderWithoutCurrentTerms_IsRejectedWithoutWrites()` for self and counter flows.
- `SamePackage_RenewsAfterLatestEndAtFullPrice()`.
- `HigherPrice_UpgradesAfterLatestEndAtFullPrice()`.
- `LowerPrice_DowngradesAfterLatestEndAtFullPrice()`.
- `DifferentPackageSamePrice_IsChangeAfterLatestEnd()`.
- `ExpiredSamePackage_IsRenewToday_ExpiredDifferentPackageIsRegisterToday()`.
- `OrderSnapshotsRestrictedDisciplines_AndMarksUnrestrictedPackage()`.
- `BusinessDateUsesConfiguredTimeZone()` with a clock seam or injectable business-date provider.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~MemberSubscriptionServiceTests`

Expected: the immediate-upgrade/proration assertions fail and acceptance/snapshot APIs are absent.

- [ ] **Step 3: Implement a single quote function used by self and counter paths**

Use package identity, not equal price, to identify renewal. `latestConfirmed.EndDate >= today` means every different package is scheduled at `latestEnd + 1`; no credit calculation exists. Acquire/update-lock the member row before pending/future checks so concurrent requests serialize.

- [ ] **Step 4: Enforce current acceptance and snapshot disciplines inside the same transaction**

Terms status, pending/future checks, subscription creation, invoice creation, discipline snapshot and audit must succeed or roll back together. Controller maps missing acceptance to HTTP 409 `TERMS_ACCEPTANCE_REQUIRED`.

- [ ] **Step 5: Run quote tests and add concurrency tests**

The concurrency test starts two order attempts for one member against SQL Server or a relational fixture that supports locking and asserts only one pending/future order is committed; the loser returns `PendingOrderExists` or `UpcomingSubscriptionExists`.

Run:

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~MemberSubscriptionServiceTests|FullyQualifiedName~ScheduledSubscriptionConcurrencyTests"
```

Expected: PASS.

- [ ] **Step 6: Commit scheduling/enforcement**

```powershell
git add Services/MemberSubscriptionService SportsCenterManagement/Controllers/MemberSubscriptionController.cs SportsCenterManagement.Tests/MemberSubscriptionServiceTests.cs SportsCenterManagement.Tests/ScheduledSubscriptionConcurrencyTests.cs
git commit -m "feat: schedule package changes after paid periods"
```

### Task 5: Confirm scheduled changes, preserve entitlements, and block paid cancellation

**Files:**
- Modify: `Services/MembershipInvoiceService/MembershipInvoiceService.cs`
- Modify: `Services/MembershipInvoiceService/IMembershipInvoiceService.cs`
- Modify: `Services/ClassScheduling/ClassEligibility.cs`
- Modify: `SportsCenterManagement/Controllers/MembershipInvoiceController.cs`
- Modify: `SportsCenterManagement.Tests/CashPaymentServiceTests.cs`
- Modify: `SportsCenterManagement.Tests/BankTransferTests.cs`
- Modify: `SportsCenterManagement.Tests/DemoPaymentTests.cs`
- Modify: `SportsCenterManagement.Tests/VnpaySandboxTests.cs`
- Modify: `SportsCenterManagement.Tests/ClassRegistrationServiceTests.cs`

**Interfaces:**
- Payment accepts `REGISTER`, `RENEW`, `UPGRADE`, `DOWNGRADE`, `CHANGE` when snapshot price/duration and scheduled dates are valid.
- Payment preserves every earlier subscription date and discipline snapshot.
- `ClassEligibility.ValidateAsync` reads `MemberSubscriptionDiscipline` when `HasDisciplineRestrictions=true`; false means all disciplines.
- Cancel endpoint returns `ConfirmedSubscriptionCannotBeCanceled`/HTTP 409 `CONFIRMED_SUBSCRIPTION_CANNOT_BE_CANCELED` for paid/confirmed pairs.

- [ ] **Step 1: Replace unsupported-order tests with failing scheduled-payment tests**

For cash, bank transfer, demo and VNPAY sandbox, assert an `UPGRADE`/`DOWNGRADE` pending invoice becomes `PAID`/`CONFIRMED`, keeps its future `StartDate`, charges full `PackagePrice`, and leaves the previous subscription unchanged.

Also assert catalog edits after order creation do not rewrite the captured price, duration or discipline snapshot; package deactivation still returns `PackageInactive` without mutation.

- [ ] **Step 2: Run payment tests and verify RED**

Run:

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~CashPaymentServiceTests|FullyQualifiedName~BankTransferTests|FullyQualifiedName~DemoPaymentTests|FullyQualifiedName~VnpaySandboxTests"
```

Expected: FAIL because current guards reject upgrade/downgrade.

- [ ] **Step 3: Implement one shared scheduled-kind validation path**

Remove quote-expiry/proration logic and unreachable date-truncation code. Recompute only defensive invariants: amount equals snapshot full price, start date follows latest prior confirmed end when prior exists, and no second future confirmed row is introduced.

- [ ] **Step 4: Add failing entitlement snapshot tests and implement eligibility**

Tests pin both cases:

- later catalog edits do not remove a bought discipline;
- `HasDisciplineRestrictions=false` allows any active class discipline even with zero snapshot rows.

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~ClassRegistrationServiceTests`

Expected after implementation: PASS.

- [ ] **Step 5: Add paid-cancel test and stable API error**

Assert pending unpaid cancellation still succeeds and writes audit, while paid/confirmed cancellation returns 409 and changes nothing.

- [ ] **Step 6: Run all affected BE tests and commit**

Run:

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~CashPaymentServiceTests|FullyQualifiedName~BankTransferTests|FullyQualifiedName~DemoPaymentTests|FullyQualifiedName~VnpaySandboxTests|FullyQualifiedName~ClassRegistrationServiceTests"
```

Expected: PASS.

```powershell
git add Services/MembershipInvoiceService Services/ClassScheduling/ClassEligibility.cs SportsCenterManagement/Controllers/MembershipInvoiceController.cs SportsCenterManagement.Tests
git commit -m "feat: activate scheduled package changes safely"
```

### Task 6: FE terms client and deterministic order quote helpers

**Files (FE repo):**
- Create: `src/services/membershipTermsApi.ts`
- Create: `src/services/membershipTermsApi.test.ts`
- Create: `src/utils/membershipTerms.ts`
- Create: `src/utils/membershipTerms.test.ts`
- Modify: `src/services/membershipService.ts`
- Create: `src/services/membershipService.test.ts`
- Modify: `src/types/membership.ts`
- Modify: `src/services/membershipApi.ts`
- Modify: `src/services/apiErrors.ts`

**Interfaces:**
- `membershipTermsApi.getCurrent(): Promise<CurrentMembershipTerms>`.
- `membershipTermsApi.accept(version: string, contentHash: string): Promise<MemberTermsStatus>`.
- `membershipTermsApi.getMemberStatus(memberId: string): Promise<MemberTermsStatus>`.
- `canAcceptTerms({hasScrolledToEnd, requiredChecks}): boolean`.
- `resolveOrderKind` includes `CHANGE`; `buildMembershipQuote` always schedules after latest confirmed end and never returns credit fields.

- [ ] **Step 1: Write failing API/terms helper tests**

Assert exact routes, request body, DTO mapping, stale-version error propagation, and that all required checks plus scroll completion are required.

- [ ] **Step 2: Write failing order-kind/quote tests**

Cover same package, higher, lower, same-price different package, expired same/different package, Vietnamese calendar dates, and full price/no credit.

- [ ] **Step 3: Run Vitest and verify RED**

Run: `npm test -- src/services/membershipTermsApi.test.ts src/utils/membershipTerms.test.ts src/services/membershipService.test.ts`

Expected: FAIL because APIs/helpers and `CHANGE` are absent.

- [ ] **Step 4: Implement minimal clients/helpers/types**

Do not duplicate canonical terms text in FE; render sections returned by BE. Map `TERMS_ACCEPTANCE_REQUIRED` and `TERMS_VERSION_MISMATCH` to Vietnamese in `apiErrors.ts`.

- [ ] **Step 5: Run focused FE tests and commit in FE repo**

Run the same Vitest command; expected PASS.

```powershell
git add src/services/membershipTermsApi.ts src/services/membershipTermsApi.test.ts src/utils/membershipTerms.ts src/utils/membershipTerms.test.ts src/services/membershipService.ts src/services/membershipService.test.ts src/types/membership.ts src/services/membershipApi.ts src/services/apiErrors.ts
git commit -m "feat: add membership terms and scheduled quote clients"
```

### Task 7: FE terms dialog, account-only counter flow, and scheduled package UI

**Files (FE repo):**
- Create: `src/components/membership/MembershipTermsDialog.tsx`
- Create: `src/components/membership/MemberTermsStatus.tsx`
- Modify: `src/components/membership/CounterRegistrationForm.tsx`
- Modify: `src/pages/membership/MembershipPage.tsx`
- Modify: `src/styles/workspace.css`
- Modify: `src/services/membershipApi.ts`
- Modify: `src/types/membership.ts`

**Interfaces:**
- `MembershipTermsDialog` receives `terms`, `busy`, `error`, `onAccept(version, hash)`, `onClose`.
- `CounterRegistrationForm.onCreated(member, emailDelivery)` no longer returns an order.
- Selected member counter UI loads `MemberTermsStatus` and disables order creation until accepted.

- [ ] **Step 1: Change counter form to account creation only**

Remove package/payment fields and success invoice actions. Success copy instructs the member to log in and accept terms before staff can create an order.

- [ ] **Step 2: Gate self-service ordering with the terms dialog**

When terms are not current, selecting a package stores the intended package, opens the dialog, requires scrolling and all checkboxes, calls `accept`, reloads status, then resumes quote creation. Closing the dialog must not create an order.

- [ ] **Step 3: Gate counter ordering by member-owned status**

Show current version/accepted time and a “Kiểm tra lại” action. Staff cannot see or invoke an accept mutation. Backend 409 remains the final authority if state changes after loading.

- [ ] **Step 4: Replace immediate-upgrade/proration UI**

Remove credit/remaining-day fields and copy. Display every future action with exact start date, full amount and “Gói hiện tại không thay đổi”. Add `CHANGE` label “Đổi gói theo lịch”.

- [ ] **Step 5: Run FE test/build verification**

Run:

```powershell
npm test -- src/services/membershipTermsApi.test.ts src/utils/membershipTerms.test.ts src/services/membershipService.test.ts
npm run build
```

Expected: tests PASS; TypeScript/Vite build exits 0.

- [ ] **Step 6: Commit FE UI**

```powershell
git add src/components/membership src/pages/membership/MembershipPage.tsx src/styles/workspace.css src/services/membershipApi.ts src/types/membership.ts
git commit -m "feat: require terms before scheduling package orders"
```

### Task 8: End-to-end membership verification and documentation

**Files:**
- Modify: `README.md` (BE local setup/business flow)
- Modify: `../Sports-Center-Management-FE/README.md`
- Test: existing BE/FE suites

**Interfaces:**
- Consumes all earlier tasks; produces documented runnable flow.

- [ ] **Step 1: Apply migration to the local SQL container and restart BE/FE**

Verify Swagger exposes terms routes and account-only counter contract.

- [ ] **Step 2: Execute the accepted business path**

Use one member account to accept terms, create a current paid package, order a higher/lower package, pay it, and query history. Assert current dates stay unchanged and new dates start next day.

- [ ] **Step 3: Execute rejection paths**

Verify missing acceptance, stale terms hash, second future order, staff acceptance attempt, and paid cancellation return the documented stable codes.

- [ ] **Step 4: Run full verification**

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj
npm --prefix ..\Sports-Center-Management-FE test
npm --prefix ..\Sports-Center-Management-FE run build
```

Expected: all tests pass and both builds exit 0.

- [ ] **Step 5: Commit documentation**

```powershell
git add README.md
git commit -m "docs: explain terms and scheduled package workflow"
```

