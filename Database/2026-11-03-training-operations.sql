-- Sprint 3 — Training & Operations schema (attendance, center visits, support
-- requests, training profiles/plans/results, role permission config).
--
-- Column types mirror the EF model in
-- DataAccess/Entities/SportsCenterManagementContext.TrainingOperations.cs.
-- Account identifiers are VARCHAR(400) because dbo.Account.Id, dbo.Member.AccountId
-- and dbo.Coach.AccountId are VARCHAR(400); declaring them NVARCHAR makes every
-- foreign key fail with "is not the same data type as referencing column".
--
-- Schema only. Demo/seed rows live in Database/2026-11-05-center-seed.sql.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ClassSessionAttendance', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClassSessionAttendance (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassSessionAttendance PRIMARY KEY,
        RegistrationId INT NOT NULL,
        SessionId INT NOT NULL,
        MemberId VARCHAR(400) NOT NULL,
        Status VARCHAR(20) NOT NULL CONSTRAINT DF_ClassSessionAttendance_Status DEFAULT ('PRESENT'),
        Note NVARCHAR(300) NULL,
        RecordedBy VARCHAR(400) NOT NULL,
        RecordedAt DATETIME2 NOT NULL CONSTRAINT DF_ClassSessionAttendance_RecordedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_ClassSessionAttendance_Registration UNIQUE (RegistrationId),
        CONSTRAINT FK_ClassSessionAttendance_Registration FOREIGN KEY (RegistrationId) REFERENCES dbo.ClassRegistration(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ClassSessionAttendance_Session FOREIGN KEY (SessionId) REFERENCES dbo.ClassSession(Id),
        CONSTRAINT FK_ClassSessionAttendance_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.CenterVisit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CenterVisit (
        Id VARCHAR(100) NOT NULL CONSTRAINT PK_CenterVisit PRIMARY KEY,
        MemberId VARCHAR(400) NOT NULL,
        VisitDate DATE NOT NULL,
        CheckedInAt DATETIME2 NOT NULL,
        CheckedOutAt DATETIME2 NULL,
        CheckedInBy VARCHAR(400) NOT NULL,
        CheckedOutBy VARCHAR(400) NULL,
        Note NVARCHAR(300) NULL,
        CONSTRAINT FK_CenterVisit_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SupportRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportRequest (
        Id VARCHAR(100) NOT NULL CONSTRAINT PK_SupportRequest PRIMARY KEY,
        MemberId VARCHAR(400) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        Note NVARCHAR(1000) NOT NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_SupportRequest_Status DEFAULT ('OPEN'),
        ResolutionNote NVARCHAR(1000) NULL,
        CreatedBy VARCHAR(400) NOT NULL,
        UpdatedBy VARCHAR(400) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportRequest_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportRequest_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SupportRequest_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.MemberTrainingProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MemberTrainingProfile (
        MemberId VARCHAR(400) NOT NULL CONSTRAINT PK_MemberTrainingProfile PRIMARY KEY,
        FitnessGoal NVARCHAR(300) NOT NULL,
        FitnessLevel VARCHAR(30) NOT NULL CONSTRAINT DF_MemberTrainingProfile_Level DEFAULT ('BEGINNER'),
        CurrentWeightKg DECIMAL(5,1) NULL,
        TargetWeightKg DECIMAL(5,1) NULL,
        Notes NVARCHAR(500) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MemberTrainingProfile_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_MemberTrainingProfile_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId) ON DELETE CASCADE
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.TrainingPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrainingPlan (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingPlan PRIMARY KEY,
        CoachId VARCHAR(400) NOT NULL,
        PlanType VARCHAR(20) NOT NULL CONSTRAINT DF_TrainingPlan_Type DEFAULT ('INDIVIDUAL'),
        MemberId VARCHAR(400) NULL,
        ClassId INT NULL,
        Title NVARCHAR(200) NOT NULL,
        Goal NVARCHAR(300) NOT NULL,
        ExercisesJson NVARCHAR(MAX) NOT NULL,
        Homework NVARCHAR(500) NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TrainingPlan_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TrainingPlan_Coach FOREIGN KEY (CoachId) REFERENCES dbo.Coach(AccountId),
        CONSTRAINT FK_TrainingPlan_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId),
        CONSTRAINT FK_TrainingPlan_Class FOREIGN KEY (ClassId) REFERENCES dbo.Class(Id)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.TrainingResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrainingResult (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingResult PRIMARY KEY,
        CoachId VARCHAR(400) NOT NULL,
        MemberId VARCHAR(400) NOT NULL,
        SessionId INT NULL,
        PlanId INT NULL,
        RecordedDate DATE NOT NULL,
        CompletedExercises NVARCHAR(500) NOT NULL,
        CompletionRate INT NOT NULL CONSTRAINT DF_TrainingResult_Rate DEFAULT (100),
        ProgressRating VARCHAR(30) NOT NULL CONSTRAINT DF_TrainingResult_Rating DEFAULT ('ON_TRACK'),
        ResultSummary NVARCHAR(500) NOT NULL,
        CoachFeedback NVARCHAR(1000) NOT NULL,
        HomeworkAssigned NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TrainingResult_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TrainingResult_Coach FOREIGN KEY (CoachId) REFERENCES dbo.Coach(AccountId),
        CONSTRAINT FK_TrainingResult_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId),
        CONSTRAINT FK_TrainingResult_Session FOREIGN KEY (SessionId) REFERENCES dbo.ClassSession(Id),
        CONSTRAINT FK_TrainingResult_Plan FOREIGN KEY (PlanId) REFERENCES dbo.TrainingPlan(Id)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.RolePermissionConfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissionConfig (
        FeatureKey VARCHAR(50) NOT NULL CONSTRAINT PK_RolePermissionConfig PRIMARY KEY,
        AllowedRolesCsv VARCHAR(200) NOT NULL,
        UpdatedBy VARCHAR(400) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_RolePermissionConfig_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO
SET QUOTED_IDENTIFIER ON;

PRINT 'Training & Operations schema is up to date.';
