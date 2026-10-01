IF COL_LENGTH(N'dbo.Account', N'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account] ADD [DeletedAt] datetime2(7) NULL;
END;
GO

IF COL_LENGTH(N'dbo.MemberSubscription', N'IsSuspended') IS NULL
BEGIN
    ALTER TABLE [dbo].[MemberSubscription]
        ADD [IsSuspended] bit NOT NULL
            CONSTRAINT [DF_MemberSubscription_IsSuspended] DEFAULT (0);
END;
GO

IF COL_LENGTH(N'dbo.MemberSubscription', N'SuspensionReason') IS NULL
BEGIN
    ALTER TABLE [dbo].[MemberSubscription]
        ADD [SuspensionReason] nvarchar(500) NULL;
END;
GO
