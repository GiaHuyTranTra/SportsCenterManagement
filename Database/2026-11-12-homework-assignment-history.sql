SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @HomeworkTableAlreadyExisted bit =
    CASE WHEN OBJECT_ID(N'dbo.HomeworkAssignment', N'U') IS NULL THEN 0 ELSE 1 END;
DECLARE @BusinessToday date =
    CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time');

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ScmsDataMigrationHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ScmsDataMigrationHistory (
            MigrationKey VARCHAR(150) NOT NULL CONSTRAINT PK_ScmsDataMigrationHistory PRIMARY KEY,
            AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ScmsDataMigrationHistory_AppliedAtUtc DEFAULT (SYSUTCDATETIME())
        );
    END;

    IF OBJECT_ID(N'dbo.HomeworkAssignment', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.HomeworkAssignment (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HomeworkAssignment PRIMARY KEY,
            CoachId VARCHAR(400) NOT NULL,
            MemberId VARCHAR(400) NOT NULL,
            ClassId INT NULL,
            SessionId INT NULL,
            Title NVARCHAR(200) NOT NULL,
            Instructions NVARCHAR(2000) NOT NULL,
            SourceType VARCHAR(20) NOT NULL,
            AssignedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_HomeworkAssignment_AssignedAtUtc DEFAULT (SYSUTCDATETIME()),
            LegacySourceKey VARCHAR(100) NULL,
            CONSTRAINT CK_HomeworkAssignment_SourceType CHECK (SourceType IN ('DIRECT', 'CLASS', 'SESSION')),
            CONSTRAINT FK_HomeworkAssignment_Coach FOREIGN KEY (CoachId) REFERENCES dbo.Coach(AccountId),
            CONSTRAINT FK_HomeworkAssignment_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId),
            CONSTRAINT FK_HomeworkAssignment_Class FOREIGN KEY (ClassId) REFERENCES dbo.Class(Id),
            CONSTRAINT FK_HomeworkAssignment_Session FOREIGN KEY (SessionId) REFERENCES dbo.ClassSession(Id)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HomeworkAssignment') AND name = N'IX_HomeworkAssignment_Member_AssignedAt')
        CREATE INDEX IX_HomeworkAssignment_Member_AssignedAt ON dbo.HomeworkAssignment(MemberId, AssignedAtUtc DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HomeworkAssignment') AND name = N'IX_HomeworkAssignment_Coach_AssignedAt')
        CREATE INDEX IX_HomeworkAssignment_Coach_AssignedAt ON dbo.HomeworkAssignment(CoachId, AssignedAtUtc DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HomeworkAssignment') AND name = N'UQ_HomeworkAssignment_LegacySourceKey')
        CREATE UNIQUE INDEX UQ_HomeworkAssignment_LegacySourceKey
            ON dbo.HomeworkAssignment(LegacySourceKey)
            WHERE LegacySourceKey IS NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.ScmsDataMigrationHistory
        WHERE MigrationKey = '2026-11-12-homework-legacy-backfill-v1'
    )
    BEGIN
    -- An existing homework table means an earlier revision already ran the
    -- one-time import. Do not reread mutable class rosters on deployment retry.
    IF @HomeworkTableAlreadyExisted = 0
    BEGIN
    INSERT INTO dbo.HomeworkAssignment
        (CoachId, MemberId, ClassId, SessionId, Title, Instructions, SourceType, AssignedAtUtc, LegacySourceKey)
    SELECT r.CoachId, r.MemberId, s.ClassId, r.SessionId,
           N'Bài tập sau buổi tập', LEFT(r.HomeworkAssigned, 2000), 'DIRECT', r.CreatedAt,
           CONCAT('RESULT:', r.Id)
    FROM dbo.TrainingResult r
    LEFT JOIN dbo.ClassSession s ON s.Id = r.SessionId
    WHERE NULLIF(LTRIM(RTRIM(r.HomeworkAssigned)), N'') IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM dbo.HomeworkAssignment h
          WHERE h.LegacySourceKey = CONCAT('RESULT:', r.Id));

    INSERT INTO dbo.HomeworkAssignment
        (CoachId, MemberId, ClassId, SessionId, Title, Instructions, SourceType, AssignedAtUtc, LegacySourceKey)
    SELECT p.CoachId, p.MemberId, p.ClassId, NULL,
           LEFT(CONCAT(N'Bài tập: ', p.Title), 200), LEFT(p.Homework, 2000), 'DIRECT', p.CreatedAt,
           CONCAT('PLAN:', p.Id, ':MEMBER:', p.MemberId)
    FROM dbo.TrainingPlan p
    WHERE p.MemberId IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(p.Homework)), N'') IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM dbo.HomeworkAssignment h
          WHERE h.LegacySourceKey = CONCAT('PLAN:', p.Id, ':MEMBER:', p.MemberId));

    ;WITH ClassPlanRecipients AS (
        SELECT DISTINCT p.Id AS PlanId, p.CoachId, p.ClassId, p.Title, p.Homework,
               p.CreatedAt, registration.MemberId
        FROM dbo.TrainingPlan p
        INNER JOIN dbo.ClassSession session ON session.ClassId = p.ClassId
        INNER JOIN dbo.ClassRegistration registration ON registration.SessionId = session.Id
        WHERE p.MemberId IS NULL
          AND p.ClassId IS NOT NULL
          AND NULLIF(LTRIM(RTRIM(p.Homework)), N'') IS NOT NULL
          AND registration.Status = 'BOOKED'
          AND registration.CanceledAt IS NULL
          AND session.Status <> 'CANCELED'
          AND CONVERT(date, session.SessionDate) >= @BusinessToday
    )
    INSERT INTO dbo.HomeworkAssignment
        (CoachId, MemberId, ClassId, SessionId, Title, Instructions, SourceType, AssignedAtUtc, LegacySourceKey)
    SELECT recipient.CoachId, recipient.MemberId, recipient.ClassId, NULL,
           LEFT(CONCAT(N'Bài tập: ', recipient.Title), 200), LEFT(recipient.Homework, 2000),
           'CLASS', recipient.CreatedAt,
           CONCAT('PLAN:', recipient.PlanId, ':MEMBER:', recipient.MemberId)
    FROM ClassPlanRecipients recipient
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.HomeworkAssignment h
        WHERE h.LegacySourceKey = CONCAT('PLAN:', recipient.PlanId, ':MEMBER:', recipient.MemberId));
    END;

    INSERT INTO dbo.ScmsDataMigrationHistory(MigrationKey)
    VALUES ('2026-11-12-homework-legacy-backfill-v1');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT COUNT(*) AS HomeworkAssignmentCount FROM dbo.HomeworkAssignment;
