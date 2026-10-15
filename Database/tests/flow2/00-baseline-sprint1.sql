-- INFERRED Sprint 1 baseline (no Flow 2). Derived from DataAccess entities/DbContext mapping,
-- minus Flow 2 objects. NOT verified against the real development database.
-- Inferred: constraint/index names for Sprint 1 tables are taken from the mapping; column
-- collations, FK delete actions of Sprint 1 FKs and unmapped CHECK constraints are unknown.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE dbo.[Role] ([Id] int NOT NULL, [Name] nvarchar(50) NOT NULL, CONSTRAINT PK_Role PRIMARY KEY ([Id]));
CREATE UNIQUE INDEX [UQ__Role__737584F616AD49EB] ON dbo.[Role]([Name]);
CREATE TABLE dbo.[Account] (
    [Id] varchar(400) NOT NULL, [Email] varchar(150) NOT NULL, [PasswordHash] varchar(255) NOT NULL,
    [Status] varchar(20) NOT NULL DEFAULT 'Active', [FailedLoginCount] int NOT NULL DEFAULT 0, [IsLocked] bit NOT NULL DEFAULT 0,
    [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()), [UpdatedAt] datetime2 NULL, [DeletedAt] datetime2 NULL,
    [Phone] varchar(10) NULL, [RoleId] int NOT NULL,
    CONSTRAINT [PK__Account__3214EC07E4264B6E] PRIMARY KEY ([Id]),
    CONSTRAINT FK_Account_Role FOREIGN KEY ([RoleId]) REFERENCES dbo.[Role]([Id]));
CREATE UNIQUE INDEX [UQ__Account__A9D105343F8562DD] ON dbo.[Account]([Email]);
CREATE UNIQUE INDEX [UQ_Account_Phone] ON dbo.[Account]([Phone]) WHERE ([Phone] IS NOT NULL);
CREATE TABLE dbo.[CenterManager] ([AccountId] varchar(400) NOT NULL, [FullName] nvarchar(100) NOT NULL, [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()),
    CONSTRAINT [PK__CenterMa__349DA5A64807BEB6] PRIMARY KEY ([AccountId]), CONSTRAINT FK_CenterManager_Account FOREIGN KEY ([AccountId]) REFERENCES dbo.[Account]([Id]));
CREATE TABLE dbo.[Coach] ([AccountId] varchar(400) NOT NULL, [FullName] nvarchar(100) NOT NULL, [Specialization] nvarchar(255) NULL, [WorkSchedule] nvarchar(500) NULL, [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()),
    CONSTRAINT [PK__Coach__349DA5A65C9861FD] PRIMARY KEY ([AccountId]), CONSTRAINT FK_Coach_Account FOREIGN KEY ([AccountId]) REFERENCES dbo.[Account]([Id]));
CREATE INDEX [IX_Coach_Pagination] ON dbo.[Coach]([CreatedAt] DESC, [AccountId] DESC);
CREATE TABLE dbo.[Member] ([AccountId] varchar(400) NOT NULL, [MemberCode] varchar(30) NOT NULL, [FullName] nvarchar(100) NULL, [DateOfBirth] date NULL, [AvatarUrl] varchar(500) NULL, [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()),
    CONSTRAINT [PK__Member__349DA5A67C08C6CC] PRIMARY KEY ([AccountId]), CONSTRAINT FK_Member_Account FOREIGN KEY ([AccountId]) REFERENCES dbo.[Account]([Id]));
CREATE UNIQUE INDEX [UQ__Member__84CA6377C2C6C2FB] ON dbo.[Member]([MemberCode]);
CREATE TABLE dbo.[Receptionist] ([AccountId] varchar(400) NOT NULL, [FullName] nvarchar(100) NOT NULL, [WorkShift] nvarchar(100) NULL, [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()),
    CONSTRAINT [PK__Receptio__349DA5A6971DB159] PRIMARY KEY ([AccountId]), CONSTRAINT FK_Receptionist_Account FOREIGN KEY ([AccountId]) REFERENCES dbo.[Account]([Id]));
CREATE TABLE dbo.[PasswordChangeOtp] ([Id] int NOT NULL IDENTITY, [AccountId] varchar(400) NOT NULL, [OtpHash] varchar(255) NOT NULL, [FailedAttempts] int NOT NULL DEFAULT 0, [MaxAttempts] int NOT NULL DEFAULT 3,
    [ExpiresAt] datetime2 NOT NULL, [IsUsed] bit NOT NULL DEFAULT 0, [CreatedAt] datetime2 NOT NULL DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__Password__3214EC07225EE6F9] PRIMARY KEY ([Id]), CONSTRAINT FK_PasswordChangeOtp_Account FOREIGN KEY ([AccountId]) REFERENCES dbo.[Account]([Id]));
CREATE UNIQUE INDEX [UQ_PasswordChangeOtp_ActiveAccount] ON dbo.[PasswordChangeOtp]([AccountId]) WHERE ([IsUsed]=(0));
CREATE TABLE dbo.[AuditLog] ([Id] varchar(400) NOT NULL, [AccountId] varchar(400) NULL, [Action] varchar(100) NOT NULL, [EntityType] varchar(100) NULL, [EntityId] varchar(400) NULL,
    [Description] nvarchar(500) NULL, [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()),
    CONSTRAINT [PK__AuditLog__3214EC07CCB0C4E6] PRIMARY KEY ([Id]), CONSTRAINT FK_AuditLog_Account FOREIGN KEY ([AccountId]) REFERENCES dbo.[Account]([Id]));
CREATE TABLE dbo.[Discipline] ([Id] int NOT NULL IDENTITY, [Name] nvarchar(80) NOT NULL, [Description] nvarchar(500) NULL,
    [IsActive] bit NOT NULL CONSTRAINT DF_Discipline_IsActive DEFAULT 1, [CreatedAt] datetime2 NOT NULL CONSTRAINT DF_Discipline_CreatedAt DEFAULT (sysutcdatetime()), [UpdatedAt] datetime2 NULL,
    CONSTRAINT PK_Discipline PRIMARY KEY ([Id]));
CREATE UNIQUE INDEX [UQ_Discipline_Name] ON dbo.[Discipline]([Name]);
CREATE TABLE dbo.[CoachDiscipline] ([CoachAccountId] varchar(400) NOT NULL, [DisciplineId] int NOT NULL, [CreatedAt] datetime2 NOT NULL CONSTRAINT DF_CoachDiscipline_CreatedAt DEFAULT (sysutcdatetime()),
    CONSTRAINT PK_CoachDiscipline PRIMARY KEY ([CoachAccountId],[DisciplineId]),
    CONSTRAINT FK_CoachDiscipline_Coach FOREIGN KEY ([CoachAccountId]) REFERENCES dbo.[Coach]([AccountId]),
    CONSTRAINT FK_CoachDiscipline_Discipline FOREIGN KEY ([DisciplineId]) REFERENCES dbo.[Discipline]([Id]));
CREATE INDEX [IX_CoachDiscipline_DisciplineId] ON dbo.[CoachDiscipline]([DisciplineId]);
CREATE TABLE dbo.[MembershipPackage] ([Id] int NOT NULL IDENTITY, [Name] nvarchar(80) NOT NULL, [Price] decimal(18,0) NOT NULL, [DurationMonths] int NOT NULL, [Benefits] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL CONSTRAINT DF_MembershipPackage_IsActive DEFAULT 1, [CreatedAt] datetime2 NOT NULL CONSTRAINT DF_MembershipPackage_CreatedAt DEFAULT (sysutcdatetime()), [UpdatedAt] datetime2 NULL,
    CONSTRAINT PK_MembershipPackage PRIMARY KEY ([Id]));
CREATE UNIQUE INDEX [UQ_MembershipPackage_Name] ON dbo.[MembershipPackage]([Name]);
CREATE TABLE dbo.[MemberSubscription] ([Id] int NOT NULL IDENTITY, [MemberId] varchar(400) NOT NULL, [PackageId] int NOT NULL, [PackageName] nvarchar(80) NOT NULL, [PackagePrice] decimal(18,0) NOT NULL,
    [DurationMonths] int NOT NULL, [Benefits] nvarchar(max) NOT NULL, [StartDate] date NOT NULL, [EndDate] date NOT NULL, [Kind] varchar(20) NOT NULL,
    [Status] varchar(20) NOT NULL CONSTRAINT DF_MemberSubscription_Status DEFAULT 'PENDING_PAYMENT', [IsSuspended] bit NOT NULL CONSTRAINT DF_MemberSubscription_IsSuspended DEFAULT 0,
    [SuspensionReason] nvarchar(500) NULL, [CreatedAt] datetime2 NOT NULL CONSTRAINT DF_MemberSubscription_CreatedAt DEFAULT (sysutcdatetime()),
    CONSTRAINT PK_MemberSubscription PRIMARY KEY ([Id]),
    CONSTRAINT FK_MemberSubscription_Member FOREIGN KEY ([MemberId]) REFERENCES dbo.[Member]([AccountId]),
    CONSTRAINT FK_MemberSubscription_MembershipPackage FOREIGN KEY ([PackageId]) REFERENCES dbo.[MembershipPackage]([Id]));
CREATE INDEX [IX_MemberSubscription_MemberId_Status] ON dbo.[MemberSubscription]([MemberId],[Status]);
CREATE UNIQUE INDEX [UQ_MemberSubscription_Pending_Member] ON dbo.[MemberSubscription]([MemberId]) WHERE ([Status]='PENDING_PAYMENT');
CREATE TABLE dbo.[MembershipInvoice] ([Id] int NOT NULL IDENTITY, [InvoiceNumber] varchar(50) NOT NULL, [SubscriptionId] int NOT NULL, [MemberId] varchar(400) NOT NULL, [Amount] decimal(18,0) NOT NULL,
    [Status] varchar(20) NOT NULL CONSTRAINT DF_MembershipInvoice_Status DEFAULT 'PENDING_PAYMENT', [PaymentMethod] varchar(30) NOT NULL,
    [CreatedAt] datetime2 NOT NULL CONSTRAINT DF_MembershipInvoice_CreatedAt DEFAULT (sysutcdatetime()), [CreatedBy] varchar(400) NOT NULL, [PaidAt] datetime2 NULL, [PaidBy] varchar(400) NULL,
    CONSTRAINT PK_MembershipInvoice PRIMARY KEY ([Id]),
    CONSTRAINT FK_MembershipInvoice_CreatedBy FOREIGN KEY ([CreatedBy]) REFERENCES dbo.[Account]([Id]),
    CONSTRAINT FK_MembershipInvoice_Member FOREIGN KEY ([MemberId]) REFERENCES dbo.[Member]([AccountId]),
    CONSTRAINT FK_MembershipInvoice_PaidBy FOREIGN KEY ([PaidBy]) REFERENCES dbo.[Account]([Id]),
    CONSTRAINT FK_MembershipInvoice_Subscription FOREIGN KEY ([SubscriptionId]) REFERENCES dbo.[MemberSubscription]([Id]));
CREATE UNIQUE INDEX [UQ_MembershipInvoice_InvoiceNumber] ON dbo.[MembershipInvoice]([InvoiceNumber]);
CREATE UNIQUE INDEX [UQ_MembershipInvoice_SubscriptionId] ON dbo.[MembershipInvoice]([SubscriptionId]);
GO
-- Seed: ids/emails are test-only.
INSERT dbo.[Role]([Id],[Name]) VALUES (1,N'CenterManager'),(2,N'Coach'),(3,N'Member'),(4,N'Receptionist');
INSERT dbo.[Account]([Id],[Email],[PasswordHash],[RoleId],[Phone]) VALUES
 ('acc-mgr','mgr@test.local','x',1,'0900000001'),('acc-coach','coach@test.local','x',2,'0900000002'),
 ('acc-mem','mem@test.local','x',3,'0900000003'),('acc-rec','rec@test.local','x',4,'0900000004');
INSERT dbo.[CenterManager]([AccountId],[FullName]) VALUES ('acc-mgr',N'Manager');
INSERT dbo.[Coach]([AccountId],[FullName],[Specialization]) VALUES ('acc-coach',N'Coach One',N'Yoga');
INSERT dbo.[Member]([AccountId],[MemberCode],[FullName]) VALUES ('acc-mem','M001',N'Member One');
INSERT dbo.[Receptionist]([AccountId],[FullName]) VALUES ('acc-rec',N'Reception');
INSERT dbo.[Discipline]([Name]) VALUES (N'Yoga'),(N'Boxing'),(N'B' + NCHAR(417) + N'i l' + NCHAR(7897) + N'i');
INSERT dbo.[CoachDiscipline] VALUES ('acc-coach',1,DEFAULT);
INSERT dbo.[MembershipPackage]([Name],[Price],[DurationMonths],[Benefits]) VALUES (N'Gold',1000,3,N'all');
INSERT dbo.[MemberSubscription]([MemberId],[PackageId],[PackageName],[PackagePrice],[DurationMonths],[Benefits],[StartDate],[EndDate],[Kind],[Status])
 VALUES ('acc-mem',1,N'Gold',1000,3,N'all','2026-10-01','2027-01-01','NEW','CONFIRMED');
GO
