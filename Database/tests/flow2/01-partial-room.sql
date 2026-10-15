-- Old-shape Room that the legacy v1 fixture assumes already exists (no NormalizedName/RowVersion/RoomType).
CREATE TABLE dbo.[Room] ([Id] int IDENTITY(1,1) NOT NULL, [Name] nvarchar(80) NOT NULL, [Location] nvarchar(120) NULL, [Capacity] int NOT NULL,
    [IsActive] bit NOT NULL CONSTRAINT DF_Room_IsActive DEFAULT 1, [CreatedAt] datetime2(7) NOT NULL CONSTRAINT DF_Room_CreatedAt DEFAULT (SYSUTCDATETIME()), [UpdatedAt] datetime2(7) NULL,
    CONSTRAINT PK_Room PRIMARY KEY ([Id]));
INSERT dbo.[Room]([Name],[Capacity]) VALUES (N'Studio A',15);
GO
