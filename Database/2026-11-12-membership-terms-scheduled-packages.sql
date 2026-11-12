SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SnapshotTableAlreadyExisted bit =
    CASE WHEN OBJECT_ID('dbo.MemberSubscriptionDiscipline', 'U') IS NULL THEN 0 ELSE 1 END;

DECLARE @DuplicatePendingMembers nvarchar(max);
SELECT @DuplicatePendingMembers = STRING_AGG(duplicates.MemberId, ', ')
FROM
(
    SELECT MemberId
    FROM dbo.MemberSubscription
    WHERE Status = 'PENDING_PAYMENT'
    GROUP BY MemberId
    HAVING COUNT(*) > 1
) AS duplicates;

IF @DuplicatePendingMembers IS NOT NULL
BEGIN
    DECLARE @PendingError nvarchar(2048) =
        N'Migration blocked: members with multiple pending subscriptions: ' + @DuplicatePendingMembers;
    THROW 51001, @PendingError, 1;
END;

DECLARE @BusinessToday date =
    CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time');
DECLARE @DuplicateFutureMembers nvarchar(max);
SELECT @DuplicateFutureMembers = STRING_AGG(duplicates.MemberId, ', ')
FROM
(
    SELECT MemberId
    FROM dbo.MemberSubscription
    WHERE Status = 'CONFIRMED' AND StartDate > @BusinessToday
    GROUP BY MemberId
    HAVING COUNT(*) > 1
) AS duplicates;

IF @DuplicateFutureMembers IS NOT NULL
BEGIN
    DECLARE @FutureError nvarchar(2048) =
        N'Migration blocked: members with multiple future subscriptions: ' + @DuplicateFutureMembers;
    THROW 51002, @FutureError, 1;
END;

BEGIN TRANSACTION;

IF OBJECT_ID('dbo.ScmsDataMigrationHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScmsDataMigrationHistory
    (
        MigrationKey varchar(150) NOT NULL CONSTRAINT PK_ScmsDataMigrationHistory PRIMARY KEY,
        AppliedAtUtc datetime2 NOT NULL CONSTRAINT DF_ScmsDataMigrationHistory_AppliedAtUtc DEFAULT (sysutcdatetime())
    );
END;

IF EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.MemberSubscription')
      AND name = 'CK_MemberSubscription_Kind'
)
BEGIN
    ALTER TABLE dbo.MemberSubscription DROP CONSTRAINT CK_MemberSubscription_Kind;
END;
ALTER TABLE dbo.MemberSubscription WITH CHECK
    ADD CONSTRAINT CK_MemberSubscription_Kind
    CHECK (Kind IN ('REGISTER', 'RENEW', 'UPGRADE', 'DOWNGRADE', 'CHANGE'));

IF COL_LENGTH('dbo.MemberSubscription', 'HasDisciplineRestrictions') IS NULL
BEGIN
    ALTER TABLE dbo.MemberSubscription
        ADD HasDisciplineRestrictions bit NOT NULL
            CONSTRAINT DF_MemberSubscription_HasDisciplineRestrictions DEFAULT (0);
END;

IF OBJECT_ID('dbo.MembershipTermAcceptance', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MembershipTermAcceptance
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_MembershipTermAcceptance PRIMARY KEY,
        MemberId varchar(400) NOT NULL,
        TermsVersion varchar(80) NOT NULL,
        ContentHash varchar(64) NOT NULL,
        AcceptanceChannel varchar(30) NOT NULL,
        AcceptedAtUtc datetime2 NOT NULL
            CONSTRAINT DF_MembershipTermAcceptance_AcceptedAtUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_MembershipTermAcceptance_Member
            FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId) ON DELETE CASCADE,
        CONSTRAINT UQ_MembershipTermAcceptance_Member_Version
            UNIQUE (MemberId, TermsVersion)
    );
END;

IF OBJECT_ID('dbo.MemberSubscriptionDiscipline', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MemberSubscriptionDiscipline
    (
        SubscriptionId int NOT NULL,
        DisciplineId int NOT NULL,
        DisciplineName nvarchar(100) NOT NULL,
        CONSTRAINT PK_MemberSubscriptionDiscipline
            PRIMARY KEY (SubscriptionId, DisciplineId),
        CONSTRAINT FK_MemberSubscriptionDiscipline_Subscription
            FOREIGN KEY (SubscriptionId) REFERENCES dbo.MemberSubscription(Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.ScmsDataMigrationHistory
    WHERE MigrationKey = '2026-11-12-membership-entitlement-snapshot-backfill-v1'
)
BEGIN
    -- If the snapshot table already existed when this script started, an older
    -- revision already performed the one-time import. Mark it complete without
    -- rereading the mutable package catalog.
    IF @SnapshotTableAlreadyExisted = 0
    BEGIN
        INSERT INTO dbo.MemberSubscriptionDiscipline (SubscriptionId, DisciplineId, DisciplineName)
        SELECT subscription.Id, packageSport.DisciplineId, discipline.Name
        FROM dbo.MemberSubscription AS subscription
        INNER JOIN dbo.MembershipPackageSport AS packageSport
            ON packageSport.PackageId = subscription.PackageId
        INNER JOIN dbo.Discipline AS discipline
            ON discipline.Id = packageSport.DisciplineId;

        UPDATE subscription
        SET HasDisciplineRestrictions = 1
        FROM dbo.MemberSubscription AS subscription
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.MemberSubscriptionDiscipline AS snapshot
            WHERE snapshot.SubscriptionId = subscription.Id
        );
    END;

    INSERT INTO dbo.ScmsDataMigrationHistory(MigrationKey)
    VALUES ('2026-11-12-membership-entitlement-snapshot-backfill-v1');
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.MemberSubscription')
      AND name = 'CK_MemberSubscription_Kind'
      AND definition LIKE '%CHANGE%'
)
    THROW 51003, 'MemberSubscription.Kind constraint does not allow CHANGE.', 1;

COMMIT TRANSACTION;
