# Coach Public Profile in Schedule Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let authenticated members open a privacy-safe coach profile from every coach name shown in their class catalog and personal schedule.

**Architecture:** Keep the existing manager-only `CoachController` unchanged and add a separate authenticated public-profile controller so method-level authorization cannot accidentally weaken admin routes. Reuse `CoachService` query composition with a dedicated DTO that omits contact and account-management fields. FE adds one cached profile client and reusable dialog/trigger component integrated into `MemberClassesPage`.

**Tech Stack:** ASP.NET Core/.NET 10, EF Core 10, xUnit, React 19, TypeScript 6, Vite 8, Vitest 5.

**Spec:** `docs/superpowers/specs/2026-11-12-membership-terms-scheduled-package-coach-homework-design.md`

## Global Constraints

- Public profile access requires authentication and allows roles `Member,Coach,Receptionist,CenterManager`.
- Never serialize email, phone, `IsLocked`, audit timestamps, deleted/inactive discipline assignments, or deleted coaches.
- Do not change the authorization boundary of manager CRUD routes in `CoachController`.
- A session without a coach ID shows plain fallback text and never issues a profile request.
- Existing user-created untracked FE files remain untouched.

## Review Focus

- A crafted response must not leak contact/account fields even if the entity contains them (Task 1 serialization test).
- Deleted or inactive coach accounts must return 404, not a partially populated profile (Task 1 tests).
- Inactive disciplines must be filtered while active disciplines remain stably ordered (Task 1 tests).
- Repeated coach names across a weekly schedule should result in one request per coach per page lifetime (Task 2 cache test).
- Failed profile loading must leave the schedule usable and allow retry (Task 3 build/manual verification).

---

### Task 1: Privacy-safe coach public-profile endpoint

**Files:**
- Create: `APIViewModel/Coach/CoachPublicProfileAPIViewModel.cs`
- Modify: `Services/CoachService/ICoachService.cs`
- Modify: `Services/CoachService/CoachService.cs`
- Create: `SportsCenterManagement/Controllers/CoachPublicProfileController.cs`
- Modify: `SportsCenterManagement.Tests/CoachServiceTests.cs`
- Create: `SportsCenterManagement.Tests/CoachPublicProfileHttpTests.cs`

**Interfaces:**
- `Task<CoachPublicProfileAPIViewModel?> GetPublicProfileAsync(string accountId)`.
- DTO fields only: `AccountId`, `FullName`, `WorkSchedule`, `Disciplines`.
- `GET /api/coaches/{accountId}/public-profile`.

- [ ] **Step 1: Write failing service tests**

Add tests:

- `GetPublicProfile_ReturnsOnlyActiveDisciplinesInStableOrder()`.
- `GetPublicProfile_ReturnsNullForDeletedOrInactiveCoach()`.
- `PublicProfileSerialization_DoesNotContainContactOrAccountManagementFields()` checking serialized JSON lacks `email`, `phone`, `isLocked`, `createdAt`, `updatedAt`.

- [ ] **Step 2: Run service tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~CoachServiceTests`

Expected: FAIL because DTO/method are absent.

- [ ] **Step 3: Implement the focused query and mapper**

Use `BuildCoachQuery(tracking:false)`, require account role `Coach`, status `Active`, `DeletedAt == null`, and filter disciplines where `Discipline.IsActive`. Do not reuse `CoachDetailAPIViewModel`.

- [ ] **Step 4: Run service tests and verify GREEN**

Run the same command; expected PASS.

- [ ] **Step 5: Write failing HTTP authorization tests**

Assert all four authenticated roles receive 200 for an active coach, unauthenticated requests receive 401, a missing/deleted coach receives 404 code `COACH_PUBLIC_PROFILE_NOT_FOUND`, and manager CRUD routes remain forbidden to Member.

- [ ] **Step 6: Implement the separate controller**

Apply `[Authorize(Roles = "Member,Coach,Receptionist,CenterManager")]` to the new controller. Do not edit the class-level manager authorization on `CoachController`.

- [ ] **Step 7: Run HTTP tests and commit BE work**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~CoachServiceTests|FullyQualifiedName~CoachPublicProfileHttpTests"`

Expected: PASS.

```powershell
git add APIViewModel/Coach/CoachPublicProfileAPIViewModel.cs Services/CoachService SportsCenterManagement/Controllers/CoachPublicProfileController.cs SportsCenterManagement.Tests/CoachServiceTests.cs SportsCenterManagement.Tests/CoachPublicProfileHttpTests.cs
git commit -m "feat: expose privacy-safe coach profiles"
```

### Task 2: FE coach profile client and cache

**Files (FE repo):**
- Create: `src/services/coachPublicProfileApi.ts`
- Create: `src/services/coachPublicProfileApi.test.ts`
- Modify: `src/types/classes.ts`

**Interfaces:**
- `CoachPublicProfile` fields: `accountId`, `fullName`, `workSchedule`, `disciplines: {disciplineId, disciplineName}[]`.
- `coachPublicProfileApi.get(accountId: string): Promise<CoachPublicProfile>`.
- `createCoachProfileCache()` returns `{ get(accountId): Promise<CoachPublicProfile>, clear(): void }`, deduplicating both resolved and in-flight requests.

- [ ] **Step 1: Write failing mapping/cache tests**

Assert route encoding, fallback display values, active discipline mapping, two simultaneous `get("coach-1")` calls sharing one fetch, and a rejected request being evicted so retry triggers a new fetch.

- [ ] **Step 2: Run focused Vitest and verify RED**

Run: `npm test -- src/services/coachPublicProfileApi.test.ts`

Expected: FAIL because the client/cache do not exist.

- [ ] **Step 3: Implement client and cache**

Use `apiRequest` and do not accept/contact fields in the TypeScript DTO. Keep the cache module-local to the page lifecycle by exposing a factory rather than a permanent global cache.

- [ ] **Step 4: Run tests and commit FE client**

Run the same command; expected PASS.

```powershell
git add src/services/coachPublicProfileApi.ts src/services/coachPublicProfileApi.test.ts src/types/classes.ts
git commit -m "feat: add coach public profile client"
```

### Task 3: Reusable coach profile dialog in member schedules

**Files (FE repo):**
- Create: `src/components/classes/CoachProfileDialog.tsx`
- Create: `src/components/classes/CoachProfileTrigger.tsx`
- Modify: `src/pages/classes/MemberClassesPage.tsx`
- Modify: `src/styles/workspace.css`

**Interfaces:**
- `CoachProfileTrigger` props: `coachId`, `coachName`, `loadProfile(accountId)`, optional `className`.
- Dialog states: loading, loaded profile, error with retry; closing never mutates schedule state.

- [ ] **Step 1: Add the reusable trigger/dialog**

Use a semantic button styled as inline coach text. Show full name, active specialties and work schedule; show “Chưa cập nhật lịch làm việc” when absent. Never render contact placeholders.

- [ ] **Step 2: Integrate every member-visible coach occurrence**

Replace coach text in:

- class catalog session cards;
- weekly personal schedule;
- list personal schedule.

Keep plain text when `coachId` is empty.

- [ ] **Step 3: Verify keyboard/error behavior**

Tab to the trigger, open with Enter/Space, close dialog with existing dialog behavior, force a 404/network failure, retry, and confirm the schedule remains interactive.

- [ ] **Step 4: Run FE verification**

```powershell
npm test -- src/services/coachPublicProfileApi.test.ts src/services/classApi.test.ts
npm run build
```

Expected: tests PASS; build exits 0.

- [ ] **Step 5: Commit FE UI**

```powershell
git add src/components/classes/CoachProfileDialog.tsx src/components/classes/CoachProfileTrigger.tsx src/pages/classes/MemberClassesPage.tsx src/styles/workspace.css
git commit -m "feat: show coach details from member schedules"
```

### Task 4: End-to-end coach-profile verification

**Files:** existing tests only.

**Interfaces:** consumes Tasks 1–3.

- [ ] **Step 1: Start BE/FE and verify a Member token**

Open the member class catalog and personal schedule, open the same coach from multiple entries, and confirm one cached request per coach.

- [ ] **Step 2: Verify privacy at the HTTP boundary**

Inspect the JSON response and confirm only `accountId`, `fullName`, `workSchedule`, and active `disciplines` are present.

- [ ] **Step 3: Run full relevant suites**

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~CoachServiceTests|FullyQualifiedName~CoachPublicProfileHttpTests"
npm --prefix ..\Sports-Center-Management-FE test -- src/services/coachPublicProfileApi.test.ts src/services/classApi.test.ts
npm --prefix ..\Sports-Center-Management-FE run build
```

Expected: all commands exit 0.

