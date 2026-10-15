-- ============================================================================
-- MIGRATION SCRIPT: SPRINT 2 FLOW 2 - CLASS BOOKING & SCHEDULE MANAGEMENT
-- Target Database: SportsCenterManagement (SQL Server)
-- Note: Idempotent script with explicit constraint and index names.
--       Follows Database First conventions and NO CASCADE DELETE.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Cập nhật bảng Room (Thêm RoomType)
IF COL_LENGTH('dbo.Room', 'RoomType') IS NULL
BEGIN
    ALTER TABLE [dbo].[Room]
        ADD [RoomType] NVARCHAR(20) NULL;
END;
GO

-- 1.1 Check constraint cho RoomType nếu có
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_Room_RoomType' AND parent_object_id = OBJECT_ID(N'[dbo].[Room]')
)
BEGIN
    ALTER TABLE [dbo].[Room] WITH CHECK ADD
    CONSTRAINT [CK_Room_RoomType] CHECK (
        [RoomType] IS NULL OR [RoomType] IN (N'STUDIO', N'GYM', N'POOL', N'COURT', N'OTHER')
    );
END;
GO

-- 1.2 Check constraint cho Room.Capacity > 0
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_Room_Capacity' AND parent_object_id = OBJECT_ID(N'[dbo].[Room]')
)
BEGIN
    ALTER TABLE [dbo].[Room] WITH CHECK ADD
    CONSTRAINT [CK_Room_Capacity] CHECK ([Capacity] > 0);
END;
GO

-- 1.3 Unique index cho Room.Name
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_Room_Name' AND object_id = OBJECT_ID(N'[dbo].[Room]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Room_Name]
    ON [dbo].[Room] ([Name] ASC);
END;
GO

-- 2. Bảng MembershipPackageSport (Quyền bộ môn của gói)
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'MembershipPackageSport' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE [dbo].[MembershipPackageSport] (
        [PackageId] INT NOT NULL,
        [DisciplineId] INT NOT NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_MembershipPackageSport_CreatedAt] DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT [PK_MembershipPackageSport] PRIMARY KEY CLUSTERED ([PackageId] ASC, [DisciplineId] ASC),
        CONSTRAINT [FK_MembershipPackageSport_Package] FOREIGN KEY ([PackageId])
            REFERENCES [dbo].[MembershipPackage] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_MembershipPackageSport_Discipline] FOREIGN KEY ([DisciplineId])
            REFERENCES [dbo].[Discipline] ([Id])
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_MembershipPackageSport_DisciplineId]
    ON [dbo].[MembershipPackageSport] ([DisciplineId] ASC)
    INCLUDE ([PackageId]);
END;
GO

-- 3. Bảng Class (Lớp học)
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Class' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE [dbo].[Class] (
        [Id] INT IDENTITY(1, 1) NOT NULL,
        [DisciplineId] INT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [Capacity] INT NOT NULL,
        [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_Class_Status] DEFAULT ('DRAFT'),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Class_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] VARCHAR(400) NULL,
        [UpdatedAt] DATETIME2(7) NULL,

        CONSTRAINT [PK_Class] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Class_Discipline] FOREIGN KEY ([DisciplineId])
            REFERENCES [dbo].[Discipline] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_Class_CreatedBy] FOREIGN KEY ([CreatedBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [CK_Class_Capacity] CHECK ([Capacity] > 0),
        CONSTRAINT [CK_Class_Status] CHECK ([Status] IN ('DRAFT', 'OPEN', 'CLOSED', 'ARCHIVED'))
    );

    -- Unique Name cho các lớp chưa ARCHIVED
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Class_Name_Active]
    ON [dbo].[Class] ([Name] ASC)
    WHERE [Status] <> 'ARCHIVED';

    CREATE NONCLUSTERED INDEX [IX_Class_DisciplineId]
    ON [dbo].[Class] ([DisciplineId] ASC)
    INCLUDE ([Name], [Status], [Capacity]);
END;
GO

-- 4. Bảng ClassSession
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'ClassSession' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE [dbo].[ClassSession] (
        [Id] INT IDENTITY(1, 1) NOT NULL,
        [ClassId] INT NOT NULL,
        [CoachId] VARCHAR(400) NOT NULL,
        [RoomId] INT NOT NULL,
        [SessionDate] DATE NOT NULL,
        [StartTime] TIME(0) NOT NULL,
        [EndTime] TIME(0) NOT NULL,
        [Capacity] INT NOT NULL,
        [BookedCount] INT NOT NULL CONSTRAINT [DF_ClassSession_BookedCount] DEFAULT (0),
        [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassSession_Status] DEFAULT ('SCHEDULED'),
        [Notes] NVARCHAR(500) NULL,
        [CancelReason] NVARCHAR(500) NULL,
        [CanceledAt] DATETIME2(7) NULL,
        [CanceledBy] VARCHAR(400) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_ClassSession_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] VARCHAR(400) NULL,
        [UpdatedAt] DATETIME2(7) NULL,
        [RowVersion] ROWVERSION,

        CONSTRAINT [PK_ClassSession] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ClassSession_Class] FOREIGN KEY ([ClassId])
            REFERENCES [dbo].[Class] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassSession_Coach] FOREIGN KEY ([CoachId])
            REFERENCES [dbo].[Coach] ([AccountId])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassSession_Room] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[Room] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassSession_CanceledBy] FOREIGN KEY ([CanceledBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassSession_CreatedBy] FOREIGN KEY ([CreatedBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [CK_ClassSession_Time] CHECK ([StartTime] < [EndTime]),
        CONSTRAINT [CK_ClassSession_Capacity] CHECK ([Capacity] > 0),
        CONSTRAINT [CK_ClassSession_BookedCount] CHECK ([BookedCount] >= 0 AND [BookedCount] <= [Capacity]),
        CONSTRAINT [CK_ClassSession_Status] CHECK ([Status] IN ('SCHEDULED', 'CANCELED', 'COMPLETED'))
    );

    CREATE NONCLUSTERED INDEX [IX_ClassSession_Date_Room]
    ON [dbo].[ClassSession] ([SessionDate] ASC, [RoomId] ASC, [StartTime] ASC, [EndTime] ASC)
    WHERE [Status] = 'SCHEDULED';

    CREATE NONCLUSTERED INDEX [IX_ClassSession_Date_Coach]
    ON [dbo].[ClassSession] ([SessionDate] ASC, [CoachId] ASC, [StartTime] ASC, [EndTime] ASC)
    WHERE [Status] = 'SCHEDULED';

    CREATE NONCLUSTERED INDEX [IX_ClassSession_ClassId]
    ON [dbo].[ClassSession] ([ClassId] ASC, [SessionDate] ASC);
END;
GO

-- 5. Bảng ClassRegistration
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'ClassRegistration' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE [dbo].[ClassRegistration] (
        [Id] INT IDENTITY(1, 1) NOT NULL,
        [SessionId] INT NOT NULL,
        [MemberId] VARCHAR(400) NOT NULL,
        [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassRegistration_Status] DEFAULT ('BOOKED'),
        [RegisteredAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_ClassRegistration_RegisteredAt] DEFAULT (SYSUTCDATETIME()),
        [RegisteredVia] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassRegistration_RegisteredVia] DEFAULT ('MEMBER'),
        [RegisteredBy] VARCHAR(400) NOT NULL,
        [CanceledAt] DATETIME2(7) NULL,
        [CanceledBy] VARCHAR(400) NULL,
        [CancelReason] NVARCHAR(500) NULL,

        CONSTRAINT [PK_ClassRegistration] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ClassRegistration_ClassSession] FOREIGN KEY ([SessionId])
            REFERENCES [dbo].[ClassSession] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassRegistration_Member] FOREIGN KEY ([MemberId])
            REFERENCES [dbo].[Member] ([AccountId])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassRegistration_RegisteredBy] FOREIGN KEY ([RegisteredBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassRegistration_CanceledBy] FOREIGN KEY ([CanceledBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [CK_ClassRegistration_Status] CHECK ([Status] IN ('BOOKED', 'CANCELED')),
        CONSTRAINT [CK_ClassRegistration_RegisteredVia] CHECK ([RegisteredVia] IN ('MEMBER', 'RECEPTIONIST'))
    );

    -- Unique index ngăn Member đăng ký trùng buổi đang BOOKED
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_ClassRegistration_ActiveBooking]
    ON [dbo].[ClassRegistration] ([SessionId] ASC, [MemberId] ASC)
    WHERE [Status] = 'BOOKED';

    CREATE NONCLUSTERED INDEX [IX_ClassRegistration_Member]
    ON [dbo].[ClassRegistration] ([MemberId] ASC, [Status] ASC);
END;
GO

-- 6. Bảng Notification (Thông báo Flow 2)
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Notification' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE [dbo].[Notification] (
        [Id] INT IDENTITY(1, 1) NOT NULL,
        [RecipientId] VARCHAR(400) NOT NULL,
        [Type] VARCHAR(50) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Body] NVARCHAR(1000) NOT NULL,
        [EntityType] VARCHAR(50) NOT NULL,
        [EntityId] VARCHAR(100) NOT NULL,
        [EventAt] DATETIME2(7) NOT NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_Notification_IsRead] DEFAULT (0),
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Notification_CreatedAt] DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Notification_Recipient] FOREIGN KEY ([RecipientId])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION,
        CONSTRAINT [CK_Notification_Type] CHECK (
            [Type] IN ('CLASS_REGISTERED', 'SESSION_RESCHEDULED', 'SESSION_COACH_CHANGED', 'SESSION_CANCELED')
        )
    );

    -- Chống trùng lặp thông báo khi retry
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Notification_Dedup]
    ON [dbo].[Notification] ([RecipientId] ASC, [Type] ASC, [EntityType] ASC, [EntityId] ASC, [EventAt] ASC);

    CREATE NONCLUSTERED INDEX [IX_Notification_Recipient_IsRead]
    ON [dbo].[Notification] ([RecipientId] ASC, [IsRead] ASC, [CreatedAt] DESC);
END;
GO
