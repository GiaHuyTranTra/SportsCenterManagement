# Homework Assignment History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separate homework from training plans/results and give coaches and members an auditable, read-only assignment history.

**Architecture:** Introduce a normalized `HomeworkAssignment` entity and focused service/controller rather than adding more behavior to the already-large training controller. Assigning to a class/session fans out immutable per-member rows and matching notifications in one transaction. Existing structured homework fields are backfilled once, then removed from new API contracts and UI while legacy columns remain temporarily for safe rollback.

**Tech Stack:** ASP.NET Core/.NET 10, EF Core 10, SQL Server/SQLite tests, xUnit, React 19, TypeScript 6, Vite 8, Vitest 5.

**Spec:** `docs/superpowers/specs/2026-11-12-membership-terms-scheduled-package-coach-homework-design.md`

## Global Constraints

- Homework is not part of `TrainingPlan` or `TrainingResult` in new API/UI behavior.
- Members can only read their own homework history; coaches can only assign/read assignments within their teaching relationships.
- Homework has no completion, submission, score or grading fields.
- Class assignment recipients are distinct members with at least one `BOOKED`, non-canceled session in that class whose date is today or later.
- Session assignment recipients are `BOOKED` members of that exact non-canceled session.
- Assignment rows and notifications commit atomically.
- Legacy `COACH_HOMEWORK_NOTICE` notifications are retained but never guessed into homework rows.
- Legacy DB columns `TrainingPlan.Homework` and `TrainingResult.HomeworkAssigned` remain temporarily but new code neither writes nor displays them.

## Review Focus

- One member registered in several future sessions must receive exactly one class homework row/notification (Task 2 test).
- A coach must not target another coach's class, session or unrelated member (Task 2 authorization tests).
- Empty/canceled target cohorts must write nothing and return stable business errors (Task 2 tests).
- Transaction failure after assignments but before notifications must roll back both (Task 2 rollback test).
- Backfill must be idempotent and must not duplicate plan/result homework when rerun (Task 1 migration smoke test).

---

### Task 1: Homework persistence and legacy backfill

**Files:**
- Modify: `DataAccess/Entities/TrainingOperationEntities.cs`
- Modify: `DataAccess/Entities/SportsCenterManagementContext.TrainingOperations.cs`
- Create: `Database/2026-11-12-homework-assignment-history.sql`
- Test: `SportsCenterManagement.Tests/HomeworkAssignmentPersistenceTests.cs`

**Interfaces:**
- Add `DbSet<HomeworkAssignment> HomeworkAssignments`.
- `HomeworkAssignment` fields: `Id`, `CoachId`, `MemberId`, nullable `ClassId`, nullable `SessionId`, `Title`, `Instructions`, `SourceType`, `AssignedAtUtc`, nullable `LegacySourceKey`.
- Exact `SourceType` values: `DIRECT`, `CLASS`, `SESSION`.
- Unique filtered index on non-null `LegacySourceKey` makes backfill idempotent.

- [ ] **Step 1: Write failing persistence tests**

Assert required/max-length mappings, nullable class/session context, and duplicate `LegacySourceKey` rejection.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~HomeworkAssignmentPersistenceTests`

Expected: FAIL because entity/DbSet do not exist.

- [ ] **Step 3: Implement entity/mapping and SQL migration**

Use limits: IDs `400` chars for account IDs; title `200`; instructions `2000`; source type `20`; legacy key `100`. Add indexes `(MemberId, AssignedAtUtc DESC)` and `(CoachId, AssignedAtUtc DESC)`.

Backfill rules:

- result homework: `LegacySourceKey='RESULT:' + Id`, direct to `TrainingResult.MemberId`, title `Bài tập sau buổi tập`, session/coach copied;
- individual-plan homework: `LegacySourceKey='PLAN:' + Id + ':MEMBER:' + MemberId`, direct to plan member, title derived from plan title;
- class-plan homework: one key per plan/member and recipients selected from valid future booked registrations;
- never inspect notification title/body.

- [ ] **Step 4: Run persistence tests and migration twice**

Expected: focused tests PASS; second SQL run inserts zero duplicate homework rows.

- [ ] **Step 5: Commit schema/backfill**

```powershell
git add DataAccess/Entities/TrainingOperationEntities.cs DataAccess/Entities/SportsCenterManagementContext.TrainingOperations.cs Database/2026-11-12-homework-assignment-history.sql SportsCenterManagement.Tests/HomeworkAssignmentPersistenceTests.cs
git commit -m "feat: persist homework assignment history"
```

### Task 2: Homework assignment domain service

**Files:**
- Create: `APIViewModel/Homework/CreateHomeworkAssignmentAPIViewModel.cs`
- Create: `APIViewModel/Homework/HomeworkAssignmentAPIViewModel.cs`
- Create: `Services/HomeworkAssignmentService/IHomeworkAssignmentService.cs`
- Create: `Services/HomeworkAssignmentService/HomeworkAssignmentService.cs`
- Modify: `SportsCenterManagement/Program.cs`
- Create: `SportsCenterManagement.Tests/HomeworkAssignmentServiceTests.cs`

**Interfaces:**
- Request fields: `TargetType`, nullable `MemberId`, nullable `ClassId`, nullable `SessionId`, `Title`, `Instructions`.
- `TargetType`: `MEMBER`, `CLASS`, `SESSION`; exactly the corresponding target ID is required.
- Response fields: `Id`, `CoachId`, `CoachName`, `MemberId`, `MemberName`, nullable `ClassId`, nullable `ClassName`, nullable `SessionId`, nullable `SessionLabel`, `Title`, `Instructions`, `SourceType`, `AssignedAtUtc`.
- `Task<(AssignHomeworkResult Result, IReadOnlyList<HomeworkAssignmentAPIViewModel> Items)> AssignAsync(string coachId, CreateHomeworkAssignmentAPIViewModel request)`.
- `Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> GetMemberHistoryAsync(string memberId)`.
- `Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> GetCoachHistoryAsync(string coachId)`.

- [ ] **Step 1: Write failing target/authorization tests**

Cover:

- direct member associated with coach creates one row;
- class fan-out deduplicates a member booked in multiple future sessions;
- session fan-out includes only exact `BOOKED` registrations;
- canceled/past-only registrations are excluded;
- unrelated coach receives `TargetNotFoundOrForbidden` with no existence leak;
- empty target returns `TargetHasNoMembers`;
- invalid target shape returns `InvalidRequest`.

- [ ] **Step 2: Run service tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~HomeworkAssignmentServiceTests`

Expected: FAIL because service is missing.

- [ ] **Step 3: Implement recipient resolution and stable ordering**

Direct-member relationship is valid when the coach has a plan/result for that member or teaches a class/session with that member booked. Class/session ownership requires matching `CoachId` on a non-canceled session. Return history newest first, tie-breaking by descending `Id`.

- [ ] **Step 4: Add notification and rollback tests**

Each assignment creates one `COACH_HOMEWORK_NOTICE` notification for its member. Inject a relational failure on notification insert and assert no assignment remains.

- [ ] **Step 5: Implement one transaction for assignments, notifications and audit**

Audit action `ASSIGN_HOMEWORK`, entity type `HomeworkAssignment`, and description contains target type/count but not full instructions.

- [ ] **Step 6: Run tests and commit service**

Run the focused command; expected PASS.

```powershell
git add APIViewModel/Homework Services/HomeworkAssignmentService SportsCenterManagement/Program.cs SportsCenterManagement.Tests/HomeworkAssignmentServiceTests.cs
git commit -m "feat: add homework assignment domain service"
```

### Task 3: Homework API and removal from training contracts

**Files:**
- Create: `SportsCenterManagement/Controllers/HomeworkController.cs`
- Modify: `SportsCenterManagement/Controllers/TrainingAndOperationsController.cs`
- Create: `SportsCenterManagement.Tests/HomeworkHttpTests.cs`
- Modify: `SportsCenterManagement.Tests/Sprint3LiveE2ETests.cs`

**Interfaces:**
- `POST /api/Homework` for Coach.
- `GET /api/Homework/coach/me` for Coach.
- `GET /api/Homework/member/me` for Member.
- Stable errors: `HOMEWORK_TARGET_NOT_FOUND`, `HOMEWORK_TARGET_HAS_NO_MEMBERS`, `INVALID_HOMEWORK_TARGET`.
- `CreateTrainingPlanRequest` no longer contains `Homework`.
- `CreateTrainingResultRequest` no longer contains `HomeworkAssigned`.
- Training plan/result workspace payloads no longer serialize `homework`/`homeworkAssigned`.
- Existing `POST /api/TrainingAndOperations/coach/homework-notice` returns 410 or is removed after FE migration; do not keep an ambiguous write path.

- [ ] **Step 1: Write failing HTTP role/ownership tests**

Assert Coach POST/GET, Member self-history GET, Member POST 403, Coach member-history route 403, and one member can never query another member ID because no ID is accepted by the self route.

- [ ] **Step 2: Run HTTP tests and verify RED**

Run: `dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter FullyQualifiedName~HomeworkHttpTests`

Expected: FAIL because controller/routes are missing.

- [ ] **Step 3: Implement controller and stable error envelopes**

Extract caller ID from `ClaimTypes.NameIdentifier`; never accept coach/member actor IDs from body/query.

- [ ] **Step 4: Add contract tests proving homework fields are gone**

Serialize plan/result workspace responses and assert JSON lacks `"homework"` and `"homeworkAssigned"`. Model binding a payload containing those legacy fields must ignore them and never persist legacy columns.

- [ ] **Step 5: Remove legacy controller reads/writes/notification concatenation**

Keep entity columns for migration only. Delete field assignments, anonymous response properties, plan-notification homework concatenation, and result-notification homework concatenation.

- [ ] **Step 6: Run HTTP/live tests and commit**

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~HomeworkHttpTests|FullyQualifiedName~Sprint3LiveE2ETests"
```

Expected: PASS.

```powershell
git add SportsCenterManagement/Controllers/HomeworkController.cs SportsCenterManagement/Controllers/TrainingAndOperationsController.cs SportsCenterManagement.Tests/HomeworkHttpTests.cs SportsCenterManagement.Tests/Sprint3LiveE2ETests.cs
git commit -m "feat: expose separate homework history APIs"
```

### Task 4: FE homework client, types, and history grouping

**Files (FE repo):**
- Create: `src/services/homeworkApi.ts`
- Create: `src/services/homeworkApi.test.ts`
- Create: `src/utils/homeworkHistory.ts`
- Create: `src/utils/homeworkHistory.test.ts`
- Modify: `src/services/trainingOperationsApi.ts`

**Interfaces:**
- `HomeworkTargetType = "MEMBER" | "CLASS" | "SESSION"`.
- `HomeworkAssignmentDto` mirrors the public API without completion/submission fields.
- `homeworkApi.assign(input)`, `homeworkApi.getCoachHistory()`, `homeworkApi.getMemberHistory()`.
- `groupHomeworkByAssignedDate(items)` returns newest date groups and stable newest-first items.
- Remove `TrainingPlanDto.homework`, `TrainingResultDto.homeworkAssigned`, and corresponding create-input fields.

- [ ] **Step 1: Write failing client tests**

Assert exact routes/roles are implicit, request target shapes, DTO mapping, and no completion/submission fields are invented.

- [ ] **Step 2: Write failing history grouping tests**

Cover equal timestamps/IDs, empty history and Vietnamese calendar date preservation.

- [ ] **Step 3: Run focused Vitest and verify RED**

Run: `npm test -- src/services/homeworkApi.test.ts src/utils/homeworkHistory.test.ts`

Expected: FAIL because files/types are absent.

- [ ] **Step 4: Implement clients/helpers and remove legacy fields**

Map API errors through existing `describeError`; keep title/instructions verbatim and render safely as text.

- [ ] **Step 5: Run tests and commit FE service work**

Run the same command; expected PASS.

```powershell
git add src/services/homeworkApi.ts src/services/homeworkApi.test.ts src/utils/homeworkHistory.ts src/utils/homeworkHistory.test.ts src/services/trainingOperationsApi.ts
git commit -m "feat: add homework assignment history client"
```

### Task 5: Coach homework assignment and history UI

**Files (FE repo):**
- Modify: `src/pages/coach/CoachTrainingPage.tsx`
- Modify: `src/styles/workspace.css`

**Interfaces:**
- Consumes `homeworkApi.assign/getCoachHistory` from Task 4.
- Existing homework modal becomes an assignment form with target type/member/class/session, title and instructions.

- [ ] **Step 1: Remove homework from training plan form/cards**

Delete `planForm.homework`, “Bài tập về nhà & Lời dặn của HLV”, request field, and plan card homework box.

- [ ] **Step 2: Remove homework from training result form/cards**

Delete `resultForm.homeworkAssigned`, plan-to-result auto-copy, request field, and result homework box. Coach feedback remains part of the result.

- [ ] **Step 3: Convert the ambiguous notification modal to structured homework assignment**

Use explicit target selector; class/session options must come from the coach workspace. On success, prepend returned assignments to history and show exact recipient count.

- [ ] **Step 4: Add “Lịch sử bài tập đã giao”**

Show assigned time, title, instructions, recipient/member, and class/session context. Do not add completion controls.

- [ ] **Step 5: Run FE verification and commit**

```powershell
npm test -- src/services/homeworkApi.test.ts src/utils/homeworkHistory.test.ts
npm run build
```

Expected: tests PASS; build exits 0.

```powershell
git add src/pages/coach/CoachTrainingPage.tsx src/styles/workspace.css
git commit -m "feat: separate coach homework from training plans"
```

### Task 6: Member read-only homework history UI

**Files (FE repo):**
- Modify: `src/pages/member/MemberTrainingPage.tsx`
- Modify: `src/styles/workspace.css`

**Interfaces:**
- Consumes `homeworkApi.getMemberHistory` and `groupHomeworkByAssignedDate`.
- Produces a standalone “Lịch sử bài tập về nhà” section with no mutation controls.

- [ ] **Step 1: Remove homework from plan/result sections**

Delete plan `homework` and result `homeworkAssigned` rendering and change headings/copy so plans, results and homework are three distinct concepts.

- [ ] **Step 2: Load homework alongside the member workspace**

Keep independent loading/error state so a homework API failure does not hide plans, results or attendance.

- [ ] **Step 3: Render the read-only history**

For each item display date/time, title, instructions, coach name, and optional class/session context. Empty state says no homework has been assigned. No checkbox, upload, status, score or submit button.

- [ ] **Step 4: Run FE tests/build and commit**

```powershell
npm test -- src/services/homeworkApi.test.ts src/utils/homeworkHistory.test.ts
npm run build
```

Expected: tests PASS; build exits 0.

```powershell
git add src/pages/member/MemberTrainingPage.tsx src/styles/workspace.css
git commit -m "feat: show member homework assignment history"
```

### Task 7: End-to-end homework verification

**Files:** existing tests only.

**Interfaces:** consumes Tasks 1–6.

- [ ] **Step 1: Apply migration and seed representative targets**

Use one coach, one direct member, one class with a member booked in multiple future sessions, and canceled/past registrations.

- [ ] **Step 2: Verify direct/class/session assignment**

Assert per-member deduplication, matching notifications and newest-first histories for coach/member accounts.

- [ ] **Step 3: Verify separation**

Create a new training plan and result; inspect database/API/UI to confirm legacy homework columns remain null and homework only appears in the dedicated history.

- [ ] **Step 4: Run full relevant verification**

```powershell
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --filter "FullyQualifiedName~Homework|FullyQualifiedName~Sprint3LiveE2ETests"
npm --prefix ..\Sports-Center-Management-FE test -- src/services/homeworkApi.test.ts src/utils/homeworkHistory.test.ts
npm --prefix ..\Sports-Center-Management-FE run build
```

Expected: all commands exit 0.

