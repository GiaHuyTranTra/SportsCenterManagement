USE [SportsCenterManagement];
GO

IF COL_LENGTH('dbo.Account', 'DeletedAt') IS NULL
    ALTER TABLE [dbo].[Account] ADD [DeletedAt] datetime2(7) NULL;
GO

IF COL_LENGTH('dbo.MemberSubscription', 'IsSuspended') IS NULL
    ALTER TABLE [dbo].[MemberSubscription]
        ADD [IsSuspended] bit NOT NULL
            CONSTRAINT [DF_MemberSubscription_IsSuspended] DEFAULT (0);
GO

IF COL_LENGTH('dbo.MemberSubscription', 'SuspensionReason') IS NULL
    ALTER TABLE [dbo].[MemberSubscription]
        ADD [SuspensionReason] nvarchar(500) NULL;
GO
