USE [SportsCenterManagement];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[Discipline]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[Discipline]
        (
            [Id] INT IDENTITY(1, 1) NOT NULL,
            [Name] NVARCHAR(80) NOT NULL,
            [Description] NVARCHAR(500) NULL,
            [IsActive] BIT NOT NULL,
            [CreatedAt] DATETIME2(7) NOT NULL,
            [UpdatedAt] DATETIME2(7) NULL,
            CONSTRAINT [PK_Discipline]
                PRIMARY KEY CLUSTERED ([Id] ASC)
        );
    END;

    IF COL_LENGTH(N'dbo.Discipline', N'Id') IS NULL
        OR COL_LENGTH(N'dbo.Discipline', N'Name') IS NULL
        OR COL_LENGTH(N'dbo.Discipline', N'Description') IS NULL
        OR COL_LENGTH(N'dbo.Discipline', N'IsActive') IS NULL
        OR COL_LENGTH(N'dbo.Discipline', N'CreatedAt') IS NULL
        OR COL_LENGTH(N'dbo.Discipline', N'UpdatedAt') IS NULL
    BEGIN
        THROW 50010, 'Existing dbo.Discipline schema is incompatible.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Discipline]')
          AND c.name = N'IsActive'
    )
    BEGIN
        ALTER TABLE [dbo].[Discipline]
            ADD CONSTRAINT [DF_Discipline_IsActive]
            DEFAULT (1) FOR [IsActive];
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Discipline]')
          AND c.name = N'CreatedAt'
    )
    BEGIN
        ALTER TABLE [dbo].[Discipline]
            ADD CONSTRAINT [DF_Discipline_CreatedAt]
            DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt];
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'[dbo].[Discipline]')
          AND name = N'CK_Discipline_Name'
    )
    BEGIN
        ALTER TABLE [dbo].[Discipline] WITH CHECK
            ADD CONSTRAINT [CK_Discipline_Name]
            CHECK
            (
                LEN(LTRIM(RTRIM([Name]))) BETWEEN 2 AND 80
                AND DATALENGTH([Name]) = DATALENGTH(LTRIM(RTRIM([Name])))
            );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[dbo].[Discipline]')
          AND name = N'UQ_Discipline_Name'
    )
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_Discipline_Name]
            ON [dbo].[Discipline] ([Name] ASC);
    END;

    IF OBJECT_ID(N'[dbo].[CoachDiscipline]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[CoachDiscipline]
        (
            [CoachAccountId] VARCHAR(400) NOT NULL,
            [DisciplineId] INT NOT NULL,
            [CreatedAt] DATETIME2(7) NOT NULL,
            CONSTRAINT [PK_CoachDiscipline]
                PRIMARY KEY CLUSTERED
                ([CoachAccountId] ASC, [DisciplineId] ASC)
        );
    END;

    IF COL_LENGTH(N'dbo.CoachDiscipline', N'CoachAccountId') IS NULL
        OR COL_LENGTH(N'dbo.CoachDiscipline', N'DisciplineId') IS NULL
        OR COL_LENGTH(N'dbo.CoachDiscipline', N'CreatedAt') IS NULL
    BEGIN
        THROW 50011, 'Existing dbo.CoachDiscipline schema is incompatible.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[CoachDiscipline]')
          AND c.name = N'CreatedAt'
    )
    BEGIN
        ALTER TABLE [dbo].[CoachDiscipline]
            ADD CONSTRAINT [DF_CoachDiscipline_CreatedAt]
            DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt];
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'[dbo].[CoachDiscipline]')
          AND name = N'FK_CoachDiscipline_Coach'
    )
    BEGIN
        ALTER TABLE [dbo].[CoachDiscipline] WITH CHECK
            ADD CONSTRAINT [FK_CoachDiscipline_Coach]
            FOREIGN KEY ([CoachAccountId])
            REFERENCES [dbo].[Coach] ([AccountId])
            ON DELETE NO ACTION;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'[dbo].[CoachDiscipline]')
          AND name = N'FK_CoachDiscipline_Discipline'
    )
    BEGIN
        ALTER TABLE [dbo].[CoachDiscipline] WITH CHECK
            ADD CONSTRAINT [FK_CoachDiscipline_Discipline]
            FOREIGN KEY ([DisciplineId])
            REFERENCES [dbo].[Discipline] ([Id])
            ON DELETE NO ACTION;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[dbo].[CoachDiscipline]')
          AND name = N'IX_CoachDiscipline_DisciplineId'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_CoachDiscipline_DisciplineId]
            ON [dbo].[CoachDiscipline] ([DisciplineId] ASC);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[dbo].[Coach]')
          AND name = N'IX_Coach_Pagination'
    )
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Coach_Pagination]
            ON [dbo].[Coach] ([CreatedAt] DESC, [AccountId] DESC)
            INCLUDE ([FullName]);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM [dbo].[Coach]
        WHERE [Specialization] IS NOT NULL
          AND
          (
              LEN(LTRIM(RTRIM([Specialization]))) < 2
              OR LEN(LTRIM(RTRIM([Specialization]))) > 80
          )
    )
    BEGIN
        THROW 50012, 'Coach.Specialization contains a value outside the 2-80 character range.', 1;
    END;

    INSERT INTO [dbo].[Discipline]
        ([Name], [Description], [IsActive], [CreatedAt])
    SELECT DISTINCT
        LTRIM(RTRIM(c.[Specialization])),
        N'Migrated from the legacy Coach.Specialization value',
        1,
        SYSUTCDATETIME()
    FROM [dbo].[Coach] AS c
    WHERE c.[Specialization] IS NOT NULL
      AND LEN(LTRIM(RTRIM(c.[Specialization]))) BETWEEN 2 AND 80
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[Discipline] AS d
          WHERE d.[Name] = LTRIM(RTRIM(c.[Specialization]))
      );

    INSERT INTO [dbo].[CoachDiscipline]
        ([CoachAccountId], [DisciplineId], [CreatedAt])
    SELECT
        c.[AccountId],
        d.[Id],
        SYSUTCDATETIME()
    FROM [dbo].[Coach] AS c
    INNER JOIN [dbo].[Discipline] AS d
        ON d.[Name] = LTRIM(RTRIM(c.[Specialization]))
    WHERE c.[Specialization] IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[CoachDiscipline] AS cd
          WHERE cd.[CoachAccountId] = c.[AccountId]
            AND cd.[DisciplineId] = d.[Id]
      );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Discipline]')
      AND name = N'CK_Discipline_Name'
      AND is_disabled = 0
      AND is_not_trusted = 0
)
    THROW 50020, 'Discipline check constraint verification failed.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[CoachDiscipline]')
      AND name = N'FK_CoachDiscipline_Coach'
      AND delete_referential_action = 0
      AND is_disabled = 0
      AND is_not_trusted = 0
)
    THROW 50021, 'CoachDiscipline Coach FK verification failed.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[CoachDiscipline]')
      AND name = N'FK_CoachDiscipline_Discipline'
      AND delete_referential_action = 0
      AND is_disabled = 0
      AND is_not_trusted = 0
)
    THROW 50022, 'CoachDiscipline Discipline FK verification failed.', 1;

IF EXISTS
(
    SELECT 1
    FROM [dbo].[Coach] AS c
    WHERE c.[Specialization] IS NOT NULL
      AND LEN(LTRIM(RTRIM(c.[Specialization]))) BETWEEN 2 AND 80
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[CoachDiscipline] AS cd
          INNER JOIN [dbo].[Discipline] AS d
              ON d.[Id] = cd.[DisciplineId]
          WHERE cd.[CoachAccountId] = c.[AccountId]
            AND d.[Name] = LTRIM(RTRIM(c.[Specialization]))
      )
)
    THROW 50023, 'Coach specialization backfill verification failed.', 1;
GO
