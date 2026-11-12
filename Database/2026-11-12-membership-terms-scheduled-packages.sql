SET NOCOUNT ON;
SET XACT_ABORT ON;

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

INSERT INTO dbo.MemberSubscriptionDiscipline (SubscriptionId, DisciplineId, DisciplineName)
SELECT subscription.Id, packageSport.DisciplineId, discipline.Name
FROM dbo.MemberSubscription AS subscription
INNER JOIN dbo.MembershipPackageSport AS packageSport
    ON packageSport.PackageId = subscription.PackageId
INNER JOIN dbo.Discipline AS discipline
    ON discipline.Id = packageSport.DisciplineId
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.MemberSubscriptionDiscipline AS snapshot
    WHERE snapshot.SubscriptionId = subscription.Id
      AND snapshot.DisciplineId = packageSport.DisciplineId
);

UPDATE subscription
SET HasDisciplineRestrictions = 1
FROM dbo.MemberSubscription AS subscription
WHERE EXISTS
(
    SELECT 1
    FROM dbo.MemberSubscriptionDiscipline AS snapshot
    WHERE snapshot.SubscriptionId = subscription.Id
);

COMMIT TRANSACTION;
