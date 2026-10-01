SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRANSACTION;

MERGE dbo.Role AS target
USING
(
    VALUES
        (1, N'CenterManager'),
        (2, N'Coach'),
        (3, N'Member'),
        (4, N'Receptionist')
) AS source (Id, Name)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET Name = source.Name
WHEN NOT MATCHED THEN
    INSERT (Id, Name) VALUES (source.Id, source.Name);

DECLARE @PasswordHash varchar(255) = '$(DemoPasswordHash)';
IF LEN(@PasswordHash) < 50
    THROW 51004, 'DemoPasswordHash SQLCMD variable is required.', 1;
DECLARE @ManagerId varchar(400) = '10000000-0000-0000-0000-000000000001';
DECLARE @ReceptionistId varchar(400) = '10000000-0000-0000-0000-000000000002';
DECLARE @ActiveMemberId varchar(400) = '10000000-0000-0000-0000-000000000003';
DECLARE @ExpiringMemberId varchar(400) = '10000000-0000-0000-0000-000000000004';
DECLARE @SuspendedMemberId varchar(400) = '10000000-0000-0000-0000-000000000005';
DECLARE @InactiveMemberId varchar(400) = '10000000-0000-0000-0000-000000000006';
DECLARE @CoachId varchar(400) = '10000000-0000-0000-0000-000000000007';

DECLARE @Accounts TABLE
(
    Id varchar(400) NOT NULL PRIMARY KEY,
    Email varchar(150) NOT NULL UNIQUE,
    Phone varchar(10) NULL,
    RoleId int NOT NULL,
    Status varchar(20) NOT NULL
);

INSERT INTO @Accounts (Id, Email, Phone, RoleId, Status)
VALUES
    (@ManagerId, 'manager.test@sportscenter.local', '0900000001', 1, 'Active'),
    (@ReceptionistId, 'receptionist.test@sportscenter.local', '0900000002', 4, 'Active'),
    (@ActiveMemberId, 'member.active@sportscenter.local', '0900000003', 3, 'Active'),
    (@ExpiringMemberId, 'member.expiring@sportscenter.local', '0900000004', 3, 'Active'),
    (@SuspendedMemberId, 'member.suspended@sportscenter.local', '0900000005', 3, 'Active'),
    (@InactiveMemberId, 'member.inactive@sportscenter.local', '0900000006', 3, 'Inactive'),
    (@CoachId, 'coach.test@sportscenter.local', '0900000007', 2, 'Active');

IF EXISTS
(
    SELECT 1
    FROM dbo.Account AS target
    INNER JOIN @Accounts AS source ON source.Email = target.Email
    WHERE target.Id <> source.Id
)
BEGIN
    THROW 51000, 'A demo email is already assigned to another account.', 1;
END;

UPDATE target
SET
    target.Email = source.Email,
    target.PasswordHash = @PasswordHash,
    target.Status = source.Status,
    target.FailedLoginCount = 0,
    target.IsLocked = 0,
    target.UpdatedAt = SYSUTCDATETIME(),
    target.Phone = source.Phone,
    target.RoleId = source.RoleId,
    target.DeletedAt = NULL
FROM dbo.Account AS target
INNER JOIN @Accounts AS source ON source.Id = target.Id;

INSERT INTO dbo.Account
(
    Id, Email, PasswordHash, Status, FailedLoginCount, IsLocked,
    CreatedAt, UpdatedAt, Phone, RoleId, DeletedAt
)
SELECT
    source.Id, source.Email, @PasswordHash, source.Status, 0, 0,
    SYSUTCDATETIME(), NULL, source.Phone, source.RoleId, NULL
FROM @Accounts AS source
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Account AS target
    WHERE target.Id = source.Id
);

IF EXISTS (SELECT 1 FROM dbo.CenterManager WHERE AccountId = @ManagerId)
    UPDATE dbo.CenterManager SET FullName = N'Long Demo Manager' WHERE AccountId = @ManagerId;
ELSE
    INSERT dbo.CenterManager (AccountId, FullName, CreatedAt)
    VALUES (@ManagerId, N'Long Demo Manager', SYSUTCDATETIME());

IF EXISTS (SELECT 1 FROM dbo.Receptionist WHERE AccountId = @ReceptionistId)
    UPDATE dbo.Receptionist SET FullName = N'Long Demo Receptionist', WorkShift = N'Ca hành chính' WHERE AccountId = @ReceptionistId;
ELSE
    INSERT dbo.Receptionist (AccountId, FullName, WorkShift, CreatedAt)
    VALUES (@ReceptionistId, N'Long Demo Receptionist', N'Ca hành chính', SYSUTCDATETIME());

IF EXISTS (SELECT 1 FROM dbo.Coach WHERE AccountId = @CoachId)
    UPDATE dbo.Coach SET FullName = N'Long Demo Coach', Specialization = N'Fitness', WorkSchedule = N'Thứ Hai - Thứ Sáu' WHERE AccountId = @CoachId;
ELSE
    INSERT dbo.Coach (AccountId, FullName, Specialization, WorkSchedule, CreatedAt)
    VALUES (@CoachId, N'Long Demo Coach', N'Fitness', N'Thứ Hai - Thứ Sáu', SYSUTCDATETIME());

DECLARE @Members TABLE
(
    AccountId varchar(400) NOT NULL PRIMARY KEY,
    MemberCode varchar(30) NOT NULL UNIQUE,
    FullName nvarchar(100) NOT NULL,
    DateOfBirth date NOT NULL
);

INSERT INTO @Members (AccountId, MemberCode, FullName, DateOfBirth)
VALUES
    (@ActiveMemberId, 'LONG-ACTIVE', N'Thành viên đang hoạt động', '2000-01-03'),
    (@ExpiringMemberId, 'LONG-EXPIRING', N'Thành viên sắp hết hạn', '2000-01-04'),
    (@SuspendedMemberId, 'LONG-SUSPENDED', N'Thành viên đang tạm ngưng', '2000-01-05'),
    (@InactiveMemberId, 'LONG-INACTIVE', N'Thành viên ngừng hoạt động', '2000-01-06');

UPDATE target
SET
    target.MemberCode = source.MemberCode,
    target.FullName = source.FullName,
    target.DateOfBirth = source.DateOfBirth
FROM dbo.Member AS target
INNER JOIN @Members AS source ON source.AccountId = target.AccountId;

INSERT dbo.Member (AccountId, MemberCode, FullName, DateOfBirth, AvatarUrl, CreatedAt)
SELECT source.AccountId, source.MemberCode, source.FullName, source.DateOfBirth, NULL, SYSUTCDATETIME()
FROM @Members AS source
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Member AS target WHERE target.AccountId = source.AccountId
);

DECLARE @Today date = CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time');
DECLARE @ReceptionistEmail varchar(150) = 'receptionist.test@sportscenter.local';

DECLARE @ActivePackageId int = (SELECT Id FROM dbo.MembershipPackage WHERE Name = N'Gói Tiêu chuẩn 1 tháng');
DECLARE @ExpiringPackageId int = (SELECT Id FROM dbo.MembershipPackage WHERE Name = N'Gói Cơ bản 1 tháng');
DECLARE @SuspendedPackageId int = (SELECT Id FROM dbo.MembershipPackage WHERE Name = N'Gói Premium 1 tháng');

IF @ActivePackageId IS NULL OR @ExpiringPackageId IS NULL OR @SuspendedPackageId IS NULL
BEGIN
    THROW 51001, 'Run the demo membership package seed before the account seed.', 1;
END;

DECLARE @ActiveSubscriptionId int =
(
    SELECT SubscriptionId FROM dbo.MembershipInvoice WHERE InvoiceNumber = 'LONG-DEMO-ACTIVE-001'
);
IF @ActiveSubscriptionId IS NULL
BEGIN
    INSERT dbo.MemberSubscription
    (
        MemberId, PackageId, PackageName, PackagePrice, DurationMonths,
        Benefits, StartDate, EndDate, Kind, Status, IsSuspended,
        SuspensionReason, CreatedAt
    )
    SELECT
        @ActiveMemberId, Id, Name, Price, DurationMonths, Benefits,
        DATEADD(day, -5, @Today), DATEADD(day, 24, @Today),
        'REGISTER', 'CONFIRMED', 0, NULL, SYSUTCDATETIME()
    FROM dbo.MembershipPackage WHERE Id = @ActivePackageId;
    SET @ActiveSubscriptionId = CONVERT(int, SCOPE_IDENTITY());
    INSERT dbo.MembershipInvoice
    (
        InvoiceNumber, SubscriptionId, MemberId, Amount, Status,
        PaymentMethod, CreatedAt, CreatedBy, PaidAt, PaidBy
    )
    SELECT
        'LONG-DEMO-ACTIVE-001', @ActiveSubscriptionId, @ActiveMemberId,
        Price, 'PAID', 'CASH', SYSUTCDATETIME(), @ReceptionistId,
        SYSUTCDATETIME(), @ReceptionistId
    FROM dbo.MembershipPackage WHERE Id = @ActivePackageId;
END;

DECLARE @ExpiringSubscriptionId int =
(
    SELECT SubscriptionId FROM dbo.MembershipInvoice WHERE InvoiceNumber = 'LONG-DEMO-EXPIRING-001'
);
IF @ExpiringSubscriptionId IS NULL
BEGIN
    INSERT dbo.MemberSubscription
    (
        MemberId, PackageId, PackageName, PackagePrice, DurationMonths,
        Benefits, StartDate, EndDate, Kind, Status, IsSuspended,
        SuspensionReason, CreatedAt
    )
    SELECT
        @ExpiringMemberId, Id, Name, Price, DurationMonths, Benefits,
        DATEADD(day, -25, @Today), DATEADD(day, 4, @Today),
        'REGISTER', 'CONFIRMED', 0, NULL, SYSUTCDATETIME()
    FROM dbo.MembershipPackage WHERE Id = @ExpiringPackageId;
    SET @ExpiringSubscriptionId = CONVERT(int, SCOPE_IDENTITY());
    INSERT dbo.MembershipInvoice
    (
        InvoiceNumber, SubscriptionId, MemberId, Amount, Status,
        PaymentMethod, CreatedAt, CreatedBy, PaidAt, PaidBy
    )
    SELECT
        'LONG-DEMO-EXPIRING-001', @ExpiringSubscriptionId, @ExpiringMemberId,
        Price, 'PAID', 'CASH', SYSUTCDATETIME(), @ReceptionistId,
        SYSUTCDATETIME(), @ReceptionistId
    FROM dbo.MembershipPackage WHERE Id = @ExpiringPackageId;
END;

DECLARE @SuspendedSubscriptionId int =
(
    SELECT SubscriptionId FROM dbo.MembershipInvoice WHERE InvoiceNumber = 'LONG-DEMO-SUSPENDED-001'
);
IF @SuspendedSubscriptionId IS NULL
BEGIN
    INSERT dbo.MemberSubscription
    (
        MemberId, PackageId, PackageName, PackagePrice, DurationMonths,
        Benefits, StartDate, EndDate, Kind, Status, IsSuspended,
        SuspensionReason, CreatedAt
    )
    SELECT
        @SuspendedMemberId, Id, Name, Price, DurationMonths, Benefits,
        DATEADD(day, -3, @Today), DATEADD(day, 26, @Today),
        'REGISTER', 'CONFIRMED', 1, N'Tạm ngưng theo yêu cầu thành viên', SYSUTCDATETIME()
    FROM dbo.MembershipPackage WHERE Id = @SuspendedPackageId;
    SET @SuspendedSubscriptionId = CONVERT(int, SCOPE_IDENTITY());
    INSERT dbo.MembershipInvoice
    (
        InvoiceNumber, SubscriptionId, MemberId, Amount, Status,
        PaymentMethod, CreatedAt, CreatedBy, PaidAt, PaidBy
    )
    SELECT
        'LONG-DEMO-SUSPENDED-001', @SuspendedSubscriptionId, @SuspendedMemberId,
        Price, 'PAID', 'CASH', SYSUTCDATETIME(), @ReceptionistId,
        SYSUTCDATETIME(), @ReceptionistId
    FROM dbo.MembershipPackage WHERE Id = @SuspendedPackageId;
END;

IF (SELECT COUNT(*) FROM dbo.Account WHERE Id IN (SELECT Id FROM @Accounts)) <> 7
    THROW 51002, 'Demo account seed verification failed.', 1;

IF (SELECT COUNT(*) FROM dbo.Member WHERE AccountId IN (SELECT AccountId FROM @Members)) <> 4
    THROW 51003, 'Demo member seed verification failed.', 1;

COMMIT TRANSACTION;

SELECT account.Email, role.Name AS Role, account.Status
FROM dbo.Account AS account
INNER JOIN dbo.Role AS role ON role.Id = account.RoleId
WHERE account.Id IN (SELECT Id FROM @Accounts)
ORDER BY role.Id, account.Email;
