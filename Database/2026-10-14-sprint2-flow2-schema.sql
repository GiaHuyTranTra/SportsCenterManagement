-- Flow 2 source schema upgrade. Review the target database before executing.
-- No user data is deleted. Incompatible schema/data aborts the entire transaction.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRY
    IF DB_NAME() IN (N'master',N'model',N'msdb',N'tempdb')
        THROW 51000, 'Select the confirmed development database, not a system database.', 1;
    BEGIN TRANSACTION;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock @Resource=N'SCMS:Flow2:Write', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @lockResult < 0 THROW 51001, 'Could not acquire Flow 2 schema lock.', 1;


IF OBJECT_ID(N'dbo.Account',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.Account is missing.', 1;

IF OBJECT_ID(N'dbo.Member',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.Member is missing.', 1;

IF OBJECT_ID(N'dbo.Coach',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.Coach is missing.', 1;

IF OBJECT_ID(N'dbo.MembershipPackage',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.MembershipPackage is missing.', 1;

IF OBJECT_ID(N'dbo.MemberSubscription',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.MemberSubscription is missing.', 1;

IF OBJECT_ID(N'dbo.Discipline',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.Discipline is missing.', 1;

IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NULL THROW 51002, 'Required Sprint 1 table dbo.AuditLog is missing.', 1;

DECLARE @normalizer nvarchar(max)=N'CREATE FUNCTION dbo.NormalizeFlow2Name(@value nvarchar(100)) RETURNS nvarchar(100) WITH SCHEMABINDING AS
BEGIN
    DECLARE @name nvarchar(100) = TRANSLATE(@value,N''ÀÁÂÃÄÅÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜÝàáâãäåçèéêëìíîïñòóôõöùúûüýÿĀāĂăĄąĆćĈĉĊċČčĎďĒēĔĕĖėĘęĚěĜĝĞğĠġĢģĤĥĨĩĪīĬĭĮįİĴĵĶķĹĺĻļĽľŃńŅņŇňŌōŎŏŐőŔŕŖŗŘřŚśŜŝŞşŠšŢţŤťŨũŪūŬŭŮůŰűŲųŴŵŶŷŸŹźŻżŽžƠơƯưǍǎǏǐǑǒǓǔǕǖǗǘǙǚǛǜǞǟǠǡǦǧǨǩǪǫǬǭǰǴǵǸǹǺǻȀȁȂȃȄȅȆȇȈȉȊȋȌȍȎȏȐȑȒȓȔȕȖȗȘșȚțȞȟȦȧȨȩȪȫȬȭȮȯȰȱȲȳḀḁḂḃḄḅḆḇḈḉḊḋḌḍḎḏḐḑḒḓḔḕḖḗḘḙḚḛḜḝḞḟḠḡḢḣḤḥḦḧḨḩḪḫḬḭḮḯḰḱḲḳḴḵḶḷḸḹḺḻḼḽḾḿṀṁṂṃṄṅṆṇṈṉṊṋṌṍṎṏṐṑṒṓṔṕṖṗṘṙṚṛṜṝṞṟṠṡṢṣṤṥṦṧṨṩṪṫṬṭṮṯṰṱṲṳṴṵṶṷṸṹṺṻṼṽṾṿẀẁẂẃẄẅẆẇẈẉẊẋẌẍẎẏẐẑẒẓẔẕẖẗẘẙẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹđĐ'',N''AAAAAACEEEEIIIINOOOOOUUUUYaaaaaaceeeeiiiinooooouuuuyyAaAaAaCcCcCcCcDdEeEeEeEeEeGgGgGgGgHhIiIiIiIiIJjKkLlLlLlNnNnNnOoOoOoRrRrRrSsSsSsSsTtTtUuUuUuUuUuUuWwYyYZzZzZzOoUuAaIiOoUuUuUuUuUuAaAaGgKkOoOojGgNnAaAaAaEeEeIiIiOoOoRrRrUuUuSsTtHhAaEeOoOoOoOoYyAaBbBbBbCcDdDdDdDdDdEeEeEeEeEeFfGgHhHhHhHhHhIiIiKkKkKkLlLlLlLlMmMmMmNnNnNnNnOoOoOoOoPpPpRrRrRrRrSsSsSsSsSsTtTtTtTtUuUuUuUuUuVvVvWwWwWwWwWwXxXxYyZzZzZzhtwyAaAaAaAaAaAaAaAaAaAaAaAaEeEeEeEeEeEeEeEeIiIiOoOoOoOoOoOoOoOoOoOoOoOoUuUuUuUuUuUuUuYyYyYyYydD'');
    SET @name=TRANSLATE(@name,NCHAR(9)+NCHAR(10)+NCHAR(11)+NCHAR(12)+NCHAR(13)+NCHAR(133)+NCHAR(160)+NCHAR(5760)+NCHAR(8192)+NCHAR(8193)+NCHAR(8194)+NCHAR(8195)+NCHAR(8196)+NCHAR(8197)+NCHAR(8198)+NCHAR(8199)+NCHAR(8200)+NCHAR(8201)+NCHAR(8202)+NCHAR(8232)+NCHAR(8233)+NCHAR(8239)+NCHAR(8287)+NCHAR(12288),REPLICATE(N'' '',24));
    DECLARE @combining int=768;
    WHILE @combining<=879 BEGIN SET @name=REPLACE(@name,NCHAR(@combining),N''''); SET @combining+=1; END;
    WHILE CHARINDEX(N''  '',@name)>0 SET @name=REPLACE(@name,N''  '',N'' '');
    RETURN UPPER(LTRIM(RTRIM(@name)) COLLATE Latin1_General_100_CI_AS);
END';
IF OBJECT_ID(N'dbo.NormalizeFlow2Name',N'FN') IS NULL EXEC(@normalizer);
ELSE IF REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.NormalizeFlow2Name')),CHAR(13),N'') COLLATE Latin1_General_100_BIN2 <> REPLACE(@normalizer,CHAR(13),N'') COLLATE Latin1_General_100_BIN2
    THROW 51011, 'Existing name-normalization function differs; review computed-column migration explicitly.', 1;

IF OBJECT_ID(N'dbo.Room',N'U') IS NULL
    EXEC(N'CREATE TABLE dbo.[Room] ([Id] INT IDENTITY(1,1) NOT NULL,
[Name] NVARCHAR(80) NOT NULL,
[Location] NVARCHAR(120) NULL,
[RoomType] NVARCHAR(20) NULL,
[Capacity] INT NOT NULL,
[IsActive] BIT NOT NULL CONSTRAINT [DF_Room_IsActive] DEFAULT (1),
[CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Room_CreatedAt] DEFAULT (SYSUTCDATETIME()),
[UpdatedAt] DATETIME2(7) NULL,
CONSTRAINT [PK_Room] PRIMARY KEY ([Id]))');

IF COL_LENGTH(N'dbo.Room',N'Id') IS NULL THROW 51003, 'Existing dbo.Room lacks required column Id; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'Id' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Room.Id; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'Name') IS NULL THROW 51003, 'Existing dbo.Room lacks required column Name; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'Name' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 160 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Room.Name; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'Location') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD [Location] NVARCHAR(120) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'Location' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 240)) THROW 51004, 'Incompatible column dbo.Room.Location; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'RoomType') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD [RoomType] NVARCHAR(20) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'RoomType' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 40)) THROW 51004, 'Incompatible column dbo.Room.RoomType; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'Capacity') IS NULL THROW 51003, 'Existing dbo.Room lacks required column Capacity; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'Capacity' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Room.Capacity; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'IsActive') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD [IsActive] BIT NOT NULL CONSTRAINT [DF_Room_IsActive] DEFAULT (1)');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'IsActive' AND (TYPE_NAME(c.user_type_id) <> N'bit' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Room.IsActive; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'CreatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Room_CreatedAt] DEFAULT (SYSUTCDATETIME())');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'CreatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Room.CreatedAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Room',N'UpdatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD [UpdatedAt] DATETIME2(7) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Room') AND c.name=N'UpdatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2')) THROW 51004, 'Incompatible column dbo.Room.UpdatedAt; no automatic destructive conversion.', 1;

IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Room') AND type='PK') EXEC(N'ALTER TABLE dbo.[Room] ADD CONSTRAINT [PK_Room] PRIMARY KEY ([Id])');

IF OBJECT_ID(N'dbo.MembershipPackageSport',N'U') IS NULL
    EXEC(N'CREATE TABLE dbo.[MembershipPackageSport] ([PackageId] INT NOT NULL,
[DisciplineId] INT NOT NULL,
[CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_MembershipPackageSport_CreatedAt] DEFAULT (SYSUTCDATETIME()),
CONSTRAINT [PK_MembershipPackageSport] PRIMARY KEY CLUSTERED ([PackageId] ASC, [DisciplineId] ASC))');

IF COL_LENGTH(N'dbo.MembershipPackageSport',N'PackageId') IS NULL THROW 51003, 'Existing dbo.MembershipPackageSport lacks required column PackageId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND c.name=N'PackageId' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.MembershipPackageSport.PackageId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.MembershipPackageSport',N'DisciplineId') IS NULL THROW 51003, 'Existing dbo.MembershipPackageSport lacks required column DisciplineId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND c.name=N'DisciplineId' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.MembershipPackageSport.DisciplineId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.MembershipPackageSport',N'CreatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[MembershipPackageSport] ADD [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_MembershipPackageSport_CreatedAt] DEFAULT (SYSUTCDATETIME())');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND c.name=N'CreatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.MembershipPackageSport.CreatedAt; no automatic destructive conversion.', 1;

IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND type='PK') EXEC(N'ALTER TABLE dbo.[MembershipPackageSport] ADD CONSTRAINT [PK_MembershipPackageSport] PRIMARY KEY CLUSTERED ([PackageId] ASC, [DisciplineId] ASC)');

IF OBJECT_ID(N'dbo.Class',N'U') IS NULL
    EXEC(N'CREATE TABLE dbo.[Class] ([Id] INT IDENTITY(1, 1) NOT NULL,
[DisciplineId] INT NOT NULL,
[Name] NVARCHAR(100) NOT NULL,
[Description] NVARCHAR(500) NULL,
[Capacity] INT NOT NULL,
[Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_Class_Status] DEFAULT (''DRAFT''),
[CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Class_CreatedAt] DEFAULT (SYSUTCDATETIME()),
[CreatedBy] VARCHAR(400) NULL,
[UpdatedAt] DATETIME2(7) NULL,
CONSTRAINT [PK_Class] PRIMARY KEY CLUSTERED ([Id] ASC))');

IF COL_LENGTH(N'dbo.Class',N'Id') IS NULL THROW 51003, 'Existing dbo.Class lacks required column Id; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'Id' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Class.Id; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'DisciplineId') IS NULL THROW 51003, 'Existing dbo.Class lacks required column DisciplineId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'DisciplineId' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Class.DisciplineId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'Name') IS NULL THROW 51003, 'Existing dbo.Class lacks required column Name; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'Name' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 200 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Class.Name; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'Description') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD [Description] NVARCHAR(500) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'Description' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 1000)) THROW 51004, 'Incompatible column dbo.Class.Description; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'Capacity') IS NULL THROW 51003, 'Existing dbo.Class lacks required column Capacity; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'Capacity' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Class.Capacity; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'Status') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_Class_Status] DEFAULT (''DRAFT'')');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'Status' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 20 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Class.Status; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'CreatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Class_CreatedAt] DEFAULT (SYSUTCDATETIME())');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'CreatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Class.CreatedAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'CreatedBy') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD [CreatedBy] VARCHAR(400) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'CreatedBy' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400)) THROW 51004, 'Incompatible column dbo.Class.CreatedBy; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Class',N'UpdatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD [UpdatedAt] DATETIME2(7) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Class') AND c.name=N'UpdatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2')) THROW 51004, 'Incompatible column dbo.Class.UpdatedAt; no automatic destructive conversion.', 1;

IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Class') AND type='PK') EXEC(N'ALTER TABLE dbo.[Class] ADD CONSTRAINT [PK_Class] PRIMARY KEY CLUSTERED ([Id] ASC)');

IF OBJECT_ID(N'dbo.ClassSession',N'U') IS NULL
    EXEC(N'CREATE TABLE dbo.[ClassSession] ([Id] INT IDENTITY(1, 1) NOT NULL,
[ClassId] INT NOT NULL,
[CoachId] VARCHAR(400) NOT NULL,
[RoomId] INT NOT NULL,
[SessionDate] DATE NOT NULL,
[StartTime] TIME(0) NOT NULL,
[EndTime] TIME(0) NOT NULL,
[Capacity] INT NOT NULL,
[BookedCount] INT NOT NULL CONSTRAINT [DF_ClassSession_BookedCount] DEFAULT (0),
[Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassSession_Status] DEFAULT (''SCHEDULED''),
[Notes] NVARCHAR(500) NULL,
[CancelReason] NVARCHAR(500) NULL,
[CanceledAt] DATETIME2(7) NULL,
[CanceledBy] VARCHAR(400) NULL,
[CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_ClassSession_CreatedAt] DEFAULT (SYSUTCDATETIME()),
[CreatedBy] VARCHAR(400) NULL,
[UpdatedAt] DATETIME2(7) NULL,
[RowVersion] ROWVERSION,
CONSTRAINT [PK_ClassSession] PRIMARY KEY CLUSTERED ([Id] ASC))');

IF COL_LENGTH(N'dbo.ClassSession',N'Id') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column Id; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'Id' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.Id; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'ClassId') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column ClassId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'ClassId' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.ClassId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'CoachId') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column CoachId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'CoachId' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.CoachId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'RoomId') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column RoomId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'RoomId' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.RoomId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'SessionDate') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column SessionDate; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'SessionDate' AND (TYPE_NAME(c.user_type_id) <> N'date' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.SessionDate; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'StartTime') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column StartTime; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'StartTime' AND (TYPE_NAME(c.user_type_id) <> N'time' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.StartTime; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'EndTime') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column EndTime; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'EndTime' AND (TYPE_NAME(c.user_type_id) <> N'time' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.EndTime; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'Capacity') IS NULL THROW 51003, 'Existing dbo.ClassSession lacks required column Capacity; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'Capacity' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.Capacity; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'BookedCount') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [BookedCount] INT NOT NULL CONSTRAINT [DF_ClassSession_BookedCount] DEFAULT (0)');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'BookedCount' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.BookedCount; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'Status') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassSession_Status] DEFAULT (''SCHEDULED'')');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'Status' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 20 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.Status; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'Notes') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [Notes] NVARCHAR(500) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'Notes' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 1000)) THROW 51004, 'Incompatible column dbo.ClassSession.Notes; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'CancelReason') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [CancelReason] NVARCHAR(500) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'CancelReason' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 1000)) THROW 51004, 'Incompatible column dbo.ClassSession.CancelReason; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'CanceledAt') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [CanceledAt] DATETIME2(7) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'CanceledAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2')) THROW 51004, 'Incompatible column dbo.ClassSession.CanceledAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'CanceledBy') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [CanceledBy] VARCHAR(400) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'CanceledBy' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400)) THROW 51004, 'Incompatible column dbo.ClassSession.CanceledBy; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'CreatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_ClassSession_CreatedAt] DEFAULT (SYSUTCDATETIME())');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'CreatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassSession.CreatedAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'CreatedBy') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [CreatedBy] VARCHAR(400) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'CreatedBy' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400)) THROW 51004, 'Incompatible column dbo.ClassSession.CreatedBy; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'UpdatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [UpdatedAt] DATETIME2(7) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'UpdatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2')) THROW 51004, 'Incompatible column dbo.ClassSession.UpdatedAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassSession',N'RowVersion') IS NULL EXEC(N'ALTER TABLE dbo.[ClassSession] ADD [RowVersion] ROWVERSION');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassSession') AND c.name=N'RowVersion' AND (TYPE_NAME(c.user_type_id) <> N'timestamp')) THROW 51004, 'Incompatible column dbo.ClassSession.RowVersion; no automatic destructive conversion.', 1;

IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND type='PK') EXEC(N'ALTER TABLE dbo.[ClassSession] ADD CONSTRAINT [PK_ClassSession] PRIMARY KEY CLUSTERED ([Id] ASC)');

IF OBJECT_ID(N'dbo.ClassRegistration',N'U') IS NULL
    EXEC(N'CREATE TABLE dbo.[ClassRegistration] ([Id] INT IDENTITY(1, 1) NOT NULL,
[SessionId] INT NOT NULL,
[MemberId] VARCHAR(400) NOT NULL,
[Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassRegistration_Status] DEFAULT (''BOOKED''),
[RegisteredAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_ClassRegistration_RegisteredAt] DEFAULT (SYSUTCDATETIME()),
[RegisteredVia] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassRegistration_RegisteredVia] DEFAULT (''MEMBER''),
[RegisteredBy] VARCHAR(400) NOT NULL,
[CanceledAt] DATETIME2(7) NULL,
[CanceledBy] VARCHAR(400) NULL,
[CancelReason] NVARCHAR(500) NULL,
CONSTRAINT [PK_ClassRegistration] PRIMARY KEY CLUSTERED ([Id] ASC))');

IF COL_LENGTH(N'dbo.ClassRegistration',N'Id') IS NULL THROW 51003, 'Existing dbo.ClassRegistration lacks required column Id; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'Id' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.Id; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'SessionId') IS NULL THROW 51003, 'Existing dbo.ClassRegistration lacks required column SessionId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'SessionId' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.SessionId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'MemberId') IS NULL THROW 51003, 'Existing dbo.ClassRegistration lacks required column MemberId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'MemberId' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.MemberId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'Status') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassRegistration_Status] DEFAULT (''BOOKED'')');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'Status' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 20 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.Status; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'RegisteredAt') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [RegisteredAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_ClassRegistration_RegisteredAt] DEFAULT (SYSUTCDATETIME())');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'RegisteredAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.RegisteredAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'RegisteredVia') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [RegisteredVia] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClassRegistration_RegisteredVia] DEFAULT (''MEMBER'')');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'RegisteredVia' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 20 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.RegisteredVia; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'RegisteredBy') IS NULL THROW 51003, 'Existing dbo.ClassRegistration lacks required column RegisteredBy; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'RegisteredBy' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.ClassRegistration.RegisteredBy; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'CanceledAt') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [CanceledAt] DATETIME2(7) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'CanceledAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2')) THROW 51004, 'Incompatible column dbo.ClassRegistration.CanceledAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'CanceledBy') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [CanceledBy] VARCHAR(400) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'CanceledBy' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400)) THROW 51004, 'Incompatible column dbo.ClassRegistration.CanceledBy; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.ClassRegistration',N'CancelReason') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [CancelReason] NVARCHAR(500) NULL');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ClassRegistration') AND c.name=N'CancelReason' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 1000)) THROW 51004, 'Incompatible column dbo.ClassRegistration.CancelReason; no automatic destructive conversion.', 1;

IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND type='PK') EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD CONSTRAINT [PK_ClassRegistration] PRIMARY KEY CLUSTERED ([Id] ASC)');

IF OBJECT_ID(N'dbo.Notification',N'U') IS NULL
    EXEC(N'CREATE TABLE dbo.[Notification] ([Id] INT IDENTITY(1, 1) NOT NULL,
[RecipientId] VARCHAR(400) NOT NULL,
[Type] VARCHAR(50) NOT NULL,
[Title] NVARCHAR(200) NOT NULL,
[Body] NVARCHAR(1000) NOT NULL,
[EntityType] VARCHAR(50) NOT NULL,
[EntityId] VARCHAR(100) NOT NULL,
[EventAt] DATETIME2(7) NOT NULL,
[IsRead] BIT NOT NULL CONSTRAINT [DF_Notification_IsRead] DEFAULT (0),
[CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Notification_CreatedAt] DEFAULT (SYSUTCDATETIME()),
CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC))');

IF COL_LENGTH(N'dbo.Notification',N'Id') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column Id; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'Id' AND (TYPE_NAME(c.user_type_id) <> N'int' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.Id; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'RecipientId') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column RecipientId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'RecipientId' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 400 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.RecipientId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'Type') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column Type; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'Type' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 50 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.Type; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'Title') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column Title; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'Title' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 400 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.Title; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'Body') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column Body; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'Body' AND (TYPE_NAME(c.user_type_id) <> N'nvarchar' OR c.max_length <> 2000 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.Body; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'EntityType') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column EntityType; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'EntityType' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 50 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.EntityType; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'EntityId') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column EntityId; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'EntityId' AND (TYPE_NAME(c.user_type_id) <> N'varchar' OR c.max_length <> 100 OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.EntityId; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'EventAt') IS NULL THROW 51003, 'Existing dbo.Notification lacks required column EventAt; explicit data migration required.', 1;

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'EventAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.EventAt; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'IsRead') IS NULL EXEC(N'ALTER TABLE dbo.[Notification] ADD [IsRead] BIT NOT NULL CONSTRAINT [DF_Notification_IsRead] DEFAULT (0)');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'IsRead' AND (TYPE_NAME(c.user_type_id) <> N'bit' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.IsRead; no automatic destructive conversion.', 1;

IF COL_LENGTH(N'dbo.Notification',N'CreatedAt') IS NULL EXEC(N'ALTER TABLE dbo.[Notification] ADD [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Notification_CreatedAt] DEFAULT (SYSUTCDATETIME())');

IF EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Notification') AND c.name=N'CreatedAt' AND (TYPE_NAME(c.user_type_id) <> N'datetime2' OR c.is_nullable <> 0)) THROW 51004, 'Incompatible column dbo.Notification.CreatedAt; no automatic destructive conversion.', 1;

IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Notification') AND type='PK') EXEC(N'ALTER TABLE dbo.[Notification] ADD CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC)');

IF COL_LENGTH(N'dbo.Discipline',N'NormalizedName') IS NULL EXEC(N'ALTER TABLE dbo.[Discipline] ADD NormalizedName AS CONVERT(nvarchar(80),dbo.NormalizeFlow2Name([Name])) PERSISTED');

IF NOT EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=OBJECT_ID(N'dbo.Discipline') AND name=N'NormalizedName' AND is_persisted=1) THROW 51005, 'NormalizedName must be persisted computed; review existing custom column.', 1;

IF COL_LENGTH(N'dbo.Discipline',N'RowVersion') IS NULL EXEC(N'ALTER TABLE dbo.[Discipline] ADD RowVersion rowversion');

IF COL_LENGTH(N'dbo.Room',N'NormalizedName') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD NormalizedName AS CONVERT(nvarchar(80),dbo.NormalizeFlow2Name([Name])) PERSISTED');

IF NOT EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=OBJECT_ID(N'dbo.Room') AND name=N'NormalizedName' AND is_persisted=1) THROW 51005, 'NormalizedName must be persisted computed; review existing custom column.', 1;

IF COL_LENGTH(N'dbo.Room',N'RowVersion') IS NULL EXEC(N'ALTER TABLE dbo.[Room] ADD RowVersion rowversion');

IF COL_LENGTH(N'dbo.Class',N'NormalizedName') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD NormalizedName AS CONVERT(nvarchar(100),dbo.NormalizeFlow2Name([Name])) PERSISTED');

IF NOT EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=OBJECT_ID(N'dbo.Class') AND name=N'NormalizedName' AND is_persisted=1) THROW 51005, 'NormalizedName must be persisted computed; review existing custom column.', 1;

IF COL_LENGTH(N'dbo.Class',N'RowVersion') IS NULL EXEC(N'ALTER TABLE dbo.[Class] ADD RowVersion rowversion');

IF COL_LENGTH(N'dbo.ClassRegistration',N'MemberSubscriptionId') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [MemberSubscriptionId] int NULL');

IF COL_LENGTH(N'dbo.ClassRegistration',N'RowVersion') IS NULL EXEC(N'ALTER TABLE dbo.[ClassRegistration] ADD [RowVersion] rowversion');

IF COL_LENGTH(N'dbo.Notification',N'ReadAt') IS NULL EXEC(N'ALTER TABLE dbo.[Notification] ADD [ReadAt] datetime2(7) NULL');

IF COL_LENGTH(N'dbo.Notification',N'EventId') IS NULL EXEC(N'ALTER TABLE dbo.[Notification] ADD [EventId] uniqueidentifier NULL');

IF COL_LENGTH(N'dbo.AuditLog',N'ClassId') IS NULL EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [ClassId] int NULL');

IF COL_LENGTH(N'dbo.AuditLog',N'SessionId') IS NULL EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [SessionId] int NULL');

IF COL_LENGTH(N'dbo.AuditLog',N'OperationId') IS NULL EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [OperationId] uniqueidentifier NULL');

IF OBJECT_ID(N'dbo.ClassChangeEvent',N'U') IS NULL EXEC(N'CREATE TABLE dbo.ClassChangeEvent(
Id uniqueidentifier NOT NULL CONSTRAINT PK_ClassChangeEvent PRIMARY KEY,
OperationId uniqueidentifier NOT NULL, SessionId int NOT NULL,
EventType varchar(50) NOT NULL, ActorAccountId varchar(400) NULL,
OccurredAt datetime2(7) NOT NULL)');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Room') AND name=N'CK_Room_Capacity') EXEC(N'ALTER TABLE dbo.[Room] DROP CONSTRAINT [CK_Room_Capacity]');

EXEC(N'ALTER TABLE dbo.[Room] WITH CHECK ADD CONSTRAINT [CK_Room_Capacity] CHECK ([Capacity] > 0)');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Room') AND name=N'CK_Room_RoomType') EXEC(N'ALTER TABLE dbo.[Room] DROP CONSTRAINT [CK_Room_RoomType]');

EXEC(N'ALTER TABLE dbo.[Room] WITH CHECK ADD CONSTRAINT [CK_Room_RoomType] CHECK ([RoomType] IS NULL OR [RoomType] IN (''STUDIO'',''GYM'',''POOL'',''COURT'',''OTHER''))');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND name=N'FK_MembershipPackageSport_Package') EXEC(N'ALTER TABLE dbo.[MembershipPackageSport] DROP CONSTRAINT [FK_MembershipPackageSport_Package]');

EXEC(N'ALTER TABLE dbo.[MembershipPackageSport] WITH CHECK ADD CONSTRAINT [FK_MembershipPackageSport_Package] FOREIGN KEY ([PackageId])
            REFERENCES [dbo].[MembershipPackage] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND name=N'FK_MembershipPackageSport_Discipline') EXEC(N'ALTER TABLE dbo.[MembershipPackageSport] DROP CONSTRAINT [FK_MembershipPackageSport_Discipline]');

EXEC(N'ALTER TABLE dbo.[MembershipPackageSport] WITH CHECK ADD CONSTRAINT [FK_MembershipPackageSport_Discipline] FOREIGN KEY ([DisciplineId])
            REFERENCES [dbo].[Discipline] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Class') AND name=N'FK_Class_Discipline') EXEC(N'ALTER TABLE dbo.[Class] DROP CONSTRAINT [FK_Class_Discipline]');

EXEC(N'ALTER TABLE dbo.[Class] WITH CHECK ADD CONSTRAINT [FK_Class_Discipline] FOREIGN KEY ([DisciplineId])
            REFERENCES [dbo].[Discipline] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Class') AND name=N'FK_Class_CreatedBy') EXEC(N'ALTER TABLE dbo.[Class] DROP CONSTRAINT [FK_Class_CreatedBy]');

EXEC(N'ALTER TABLE dbo.[Class] WITH CHECK ADD CONSTRAINT [FK_Class_CreatedBy] FOREIGN KEY ([CreatedBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Class') AND name=N'CK_Class_Capacity') EXEC(N'ALTER TABLE dbo.[Class] DROP CONSTRAINT [CK_Class_Capacity]');

EXEC(N'ALTER TABLE dbo.[Class] WITH CHECK ADD CONSTRAINT [CK_Class_Capacity] CHECK ([Capacity] > 0)');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Class') AND name=N'CK_Class_Status') EXEC(N'ALTER TABLE dbo.[Class] DROP CONSTRAINT [CK_Class_Status]');

EXEC(N'ALTER TABLE dbo.[Class] WITH CHECK ADD CONSTRAINT [CK_Class_Status] CHECK ([Status] IN (''DRAFT'', ''OPEN'', ''CLOSED'', ''ARCHIVED''))');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'FK_ClassSession_Class') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [FK_ClassSession_Class]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [FK_ClassSession_Class] FOREIGN KEY ([ClassId])
            REFERENCES [dbo].[Class] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'FK_ClassSession_Coach') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [FK_ClassSession_Coach]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [FK_ClassSession_Coach] FOREIGN KEY ([CoachId])
            REFERENCES [dbo].[Coach] ([AccountId])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'FK_ClassSession_Room') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [FK_ClassSession_Room]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [FK_ClassSession_Room] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[Room] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'FK_ClassSession_CanceledBy') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [FK_ClassSession_CanceledBy]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [FK_ClassSession_CanceledBy] FOREIGN KEY ([CanceledBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'FK_ClassSession_CreatedBy') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [FK_ClassSession_CreatedBy]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [FK_ClassSession_CreatedBy] FOREIGN KEY ([CreatedBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'CK_ClassSession_Time') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [CK_ClassSession_Time]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [CK_ClassSession_Time] CHECK ([StartTime] < [EndTime])');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'CK_ClassSession_Capacity') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [CK_ClassSession_Capacity]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [CK_ClassSession_Capacity] CHECK ([Capacity] > 0)');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'CK_ClassSession_BookedCount') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [CK_ClassSession_BookedCount]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [CK_ClassSession_BookedCount] CHECK ([BookedCount] >= 0 AND [BookedCount] <= [Capacity])');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'CK_ClassSession_Status') EXEC(N'ALTER TABLE dbo.[ClassSession] DROP CONSTRAINT [CK_ClassSession_Status]');

EXEC(N'ALTER TABLE dbo.[ClassSession] WITH CHECK ADD CONSTRAINT [CK_ClassSession_Status] CHECK ([Status] IN (''SCHEDULED'', ''CANCELED'', ''COMPLETED''))');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'FK_ClassRegistration_ClassSession') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [FK_ClassRegistration_ClassSession]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT [FK_ClassRegistration_ClassSession] FOREIGN KEY ([SessionId])
            REFERENCES [dbo].[ClassSession] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'FK_ClassRegistration_Member') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [FK_ClassRegistration_Member]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT [FK_ClassRegistration_Member] FOREIGN KEY ([MemberId])
            REFERENCES [dbo].[Member] ([AccountId])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'FK_ClassRegistration_RegisteredBy') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [FK_ClassRegistration_RegisteredBy]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT [FK_ClassRegistration_RegisteredBy] FOREIGN KEY ([RegisteredBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'FK_ClassRegistration_CanceledBy') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [FK_ClassRegistration_CanceledBy]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT [FK_ClassRegistration_CanceledBy] FOREIGN KEY ([CanceledBy])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'CK_ClassRegistration_Status') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [CK_ClassRegistration_Status]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT [CK_ClassRegistration_Status] CHECK ([Status] IN (''BOOKED'', ''CANCELED''))');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'CK_ClassRegistration_RegisteredVia') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [CK_ClassRegistration_RegisteredVia]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT [CK_ClassRegistration_RegisteredVia] CHECK ([RegisteredVia] IN (''MEMBER'', ''RECEPTIONIST''))');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Notification') AND name=N'FK_Notification_Recipient') EXEC(N'ALTER TABLE dbo.[Notification] DROP CONSTRAINT [FK_Notification_Recipient]');

EXEC(N'ALTER TABLE dbo.[Notification] WITH CHECK ADD CONSTRAINT [FK_Notification_Recipient] FOREIGN KEY ([RecipientId])
            REFERENCES [dbo].[Account] ([Id])
            ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Notification') AND name=N'CK_Notification_Type') EXEC(N'ALTER TABLE dbo.[Notification] DROP CONSTRAINT [CK_Notification_Type]');

EXEC(N'ALTER TABLE dbo.[Notification] WITH CHECK ADD CONSTRAINT [CK_Notification_Type] CHECK (
            [Type] IN (''CLASS_REGISTERED'', ''SESSION_RESCHEDULED'', ''SESSION_COACH_CHANGED'', ''SESSION_CANCELED'')
        )');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'FK_ClassRegistration_Subscription') EXEC(N'ALTER TABLE dbo.[ClassRegistration] DROP CONSTRAINT [FK_ClassRegistration_Subscription]');

EXEC(N'ALTER TABLE dbo.[ClassRegistration] WITH CHECK ADD CONSTRAINT FK_ClassRegistration_Subscription FOREIGN KEY(MemberSubscriptionId) REFERENCES dbo.MemberSubscription(Id) ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'FK_ClassChangeEvent_Session') EXEC(N'ALTER TABLE dbo.[ClassChangeEvent] DROP CONSTRAINT [FK_ClassChangeEvent_Session]');

EXEC(N'ALTER TABLE dbo.[ClassChangeEvent] WITH CHECK ADD CONSTRAINT FK_ClassChangeEvent_Session FOREIGN KEY(SessionId) REFERENCES dbo.ClassSession(Id) ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'FK_ClassChangeEvent_Actor') EXEC(N'ALTER TABLE dbo.[ClassChangeEvent] DROP CONSTRAINT [FK_ClassChangeEvent_Actor]');

EXEC(N'ALTER TABLE dbo.[ClassChangeEvent] WITH CHECK ADD CONSTRAINT FK_ClassChangeEvent_Actor FOREIGN KEY(ActorAccountId) REFERENCES dbo.Account(Id) ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.Notification') AND name=N'FK_Notification_Event') EXEC(N'ALTER TABLE dbo.[Notification] DROP CONSTRAINT [FK_Notification_Event]');

EXEC(N'ALTER TABLE dbo.[Notification] WITH CHECK ADD CONSTRAINT FK_Notification_Event FOREIGN KEY(EventId) REFERENCES dbo.ClassChangeEvent(Id) ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'FK_AuditLog_Class') EXEC(N'ALTER TABLE dbo.[AuditLog] DROP CONSTRAINT [FK_AuditLog_Class]');

EXEC(N'ALTER TABLE dbo.[AuditLog] WITH CHECK ADD CONSTRAINT FK_AuditLog_Class FOREIGN KEY(ClassId) REFERENCES dbo.Class(Id) ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.objects WHERE parent_object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'FK_AuditLog_Session') EXEC(N'ALTER TABLE dbo.[AuditLog] DROP CONSTRAINT [FK_AuditLog_Session]');

EXEC(N'ALTER TABLE dbo.[AuditLog] WITH CHECK ADD CONSTRAINT FK_AuditLog_Session FOREIGN KEY(SessionId) REFERENCES dbo.ClassSession(Id) ON DELETE NO ACTION');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Notification') AND name=N'UQ_Notification_Dedup') DROP INDEX UQ_Notification_Dedup ON dbo.Notification;

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Discipline') AND name=N'UQ_Discipline_NormalizedName') EXEC(N'DROP INDEX [UQ_Discipline_NormalizedName] ON dbo.[Discipline]');

EXEC(N'CREATE UNIQUE INDEX [UQ_Discipline_NormalizedName] ON dbo.[Discipline](NormalizedName) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Room') AND name=N'UQ_Room_NormalizedName') EXEC(N'DROP INDEX [UQ_Room_NormalizedName] ON dbo.[Room]');

EXEC(N'CREATE UNIQUE INDEX [UQ_Room_NormalizedName] ON dbo.[Room](NormalizedName) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Class') AND name=N'UQ_Class_NormalizedName_Active') EXEC(N'DROP INDEX [UQ_Class_NormalizedName_Active] ON dbo.[Class]');

EXEC(N'CREATE UNIQUE INDEX [UQ_Class_NormalizedName_Active] ON dbo.[Class](NormalizedName) WHERE Status <> ''ARCHIVED''');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'IX_ClassSession_Date_Room') EXEC(N'DROP INDEX [IX_ClassSession_Date_Room] ON dbo.[ClassSession]');

EXEC(N'CREATE  INDEX [IX_ClassSession_Date_Room] ON dbo.[ClassSession](RoomId,SessionDate,StartTime) WHERE Status = ''SCHEDULED''');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'IX_ClassSession_Date_Coach') EXEC(N'DROP INDEX [IX_ClassSession_Date_Coach] ON dbo.[ClassSession]');

EXEC(N'CREATE  INDEX [IX_ClassSession_Date_Coach] ON dbo.[ClassSession](CoachId,SessionDate,StartTime) WHERE Status = ''SCHEDULED''');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassSession') AND name=N'IX_ClassSession_ClassId') EXEC(N'DROP INDEX [IX_ClassSession_ClassId] ON dbo.[ClassSession]');

EXEC(N'CREATE  INDEX [IX_ClassSession_ClassId] ON dbo.[ClassSession](ClassId,SessionDate) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'UQ_ClassRegistration_ActiveBooking') EXEC(N'DROP INDEX [UQ_ClassRegistration_ActiveBooking] ON dbo.[ClassRegistration]');

EXEC(N'CREATE UNIQUE INDEX [UQ_ClassRegistration_ActiveBooking] ON dbo.[ClassRegistration](SessionId,MemberId) WHERE Status=''BOOKED''');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'IX_ClassRegistration_Member') EXEC(N'DROP INDEX [IX_ClassRegistration_Member] ON dbo.[ClassRegistration]');

EXEC(N'CREATE  INDEX [IX_ClassRegistration_Member] ON dbo.[ClassRegistration](MemberId,Status,SessionId) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'IX_ClassRegistration_Session') EXEC(N'DROP INDEX [IX_ClassRegistration_Session] ON dbo.[ClassRegistration]');

EXEC(N'CREATE  INDEX [IX_ClassRegistration_Session] ON dbo.[ClassRegistration](SessionId,Status) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MembershipPackageSport') AND name=N'IX_MembershipPackageSport_DisciplineId') EXEC(N'DROP INDEX [IX_MembershipPackageSport_DisciplineId] ON dbo.[MembershipPackageSport]');

EXEC(N'CREATE  INDEX [IX_MembershipPackageSport_DisciplineId] ON dbo.[MembershipPackageSport](DisciplineId) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Class') AND name=N'IX_Class_DisciplineId') EXEC(N'DROP INDEX [IX_Class_DisciplineId] ON dbo.[Class]');

EXEC(N'CREATE  INDEX [IX_Class_DisciplineId] ON dbo.[Class](DisciplineId) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'UQ_ClassChangeEvent_Operation') EXEC(N'DROP INDEX [UQ_ClassChangeEvent_Operation] ON dbo.[ClassChangeEvent]');

EXEC(N'CREATE UNIQUE INDEX [UQ_ClassChangeEvent_Operation] ON dbo.[ClassChangeEvent](OperationId,SessionId,EventType) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Notification') AND name=N'UQ_Notification_EventRecipient') EXEC(N'DROP INDEX [UQ_Notification_EventRecipient] ON dbo.[Notification]');

EXEC(N'CREATE UNIQUE INDEX [UQ_Notification_EventRecipient] ON dbo.[Notification](EventId,RecipientId) WHERE EventId IS NOT NULL');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Notification') AND name=N'IX_Notification_Recipient_ReadAt') EXEC(N'DROP INDEX [IX_Notification_Recipient_ReadAt] ON dbo.[Notification]');

EXEC(N'CREATE  INDEX [IX_Notification_Recipient_ReadAt] ON dbo.[Notification](RecipientId,ReadAt,CreatedAt DESC) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'IX_AuditLog_Class_Time') EXEC(N'DROP INDEX [IX_AuditLog_Class_Time] ON dbo.[AuditLog]');

EXEC(N'CREATE  INDEX [IX_AuditLog_Class_Time] ON dbo.[AuditLog](ClassId,CreatedAt) ');

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'IX_AuditLog_Session_Time') EXEC(N'DROP INDEX [IX_AuditLog_Session_Time] ON dbo.[AuditLog]');

EXEC(N'CREATE  INDEX [IX_AuditLog_Session_Time] ON dbo.[AuditLog](SessionId,CreatedAt) ');

EXEC(N'UPDATE dbo.Notification SET ReadAt=CreatedAt WHERE IsRead=1 AND ReadAt IS NULL;');

EXEC(N'IF EXISTS(SELECT 1 FROM dbo.ClassSession s WHERE s.BookedCount<>(SELECT COUNT(*) FROM dbo.ClassRegistration r WHERE r.SessionId=s.Id AND r.Status=''BOOKED'')) THROW 51006, ''BookedCount differs from BOOKED registrations; review data before upgrading.'', 1;');

EXEC(N'IF EXISTS(SELECT 1 FROM dbo.ClassRegistration r JOIN dbo.ClassSession s ON s.Id=r.SessionId WHERE r.Status=''BOOKED'' AND s.Status=''CANCELED'') THROW 51007, ''Active registrations exist on canceled sessions; review data.'', 1;');

IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'MemberSubscriptionId' AND TYPE_NAME(user_type_id)=N'int') THROW 51008, 'Incompatible dbo.ClassRegistration.MemberSubscriptionId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassRegistration') AND name=N'RowVersion' AND TYPE_NAME(user_type_id)=N'timestamp') THROW 51008, 'Incompatible dbo.ClassRegistration.RowVersion; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Notification') AND name=N'EventId' AND TYPE_NAME(user_type_id)=N'uniqueidentifier') THROW 51008, 'Incompatible dbo.Notification.EventId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Notification') AND name=N'ReadAt' AND TYPE_NAME(user_type_id)=N'datetime2') THROW 51008, 'Incompatible dbo.Notification.ReadAt; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'ClassId' AND TYPE_NAME(user_type_id)=N'int') THROW 51008, 'Incompatible dbo.AuditLog.ClassId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'SessionId' AND TYPE_NAME(user_type_id)=N'int') THROW 51008, 'Incompatible dbo.AuditLog.SessionId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'OperationId' AND TYPE_NAME(user_type_id)=N'uniqueidentifier') THROW 51008, 'Incompatible dbo.AuditLog.OperationId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'Id' AND TYPE_NAME(user_type_id)=N'uniqueidentifier') THROW 51008, 'Incompatible dbo.ClassChangeEvent.Id; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'OperationId' AND TYPE_NAME(user_type_id)=N'uniqueidentifier') THROW 51008, 'Incompatible dbo.ClassChangeEvent.OperationId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'SessionId' AND TYPE_NAME(user_type_id)=N'int') THROW 51008, 'Incompatible dbo.ClassChangeEvent.SessionId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'EventType' AND TYPE_NAME(user_type_id)=N'varchar' AND max_length=50) THROW 51008, 'Incompatible dbo.ClassChangeEvent.EventType; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'ActorAccountId' AND TYPE_NAME(user_type_id)=N'varchar' AND max_length=400) THROW 51008, 'Incompatible dbo.ClassChangeEvent.ActorAccountId; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ClassChangeEvent') AND name=N'OccurredAt' AND TYPE_NAME(user_type_id)=N'datetime2') THROW 51008, 'Incompatible dbo.ClassChangeEvent.OccurredAt; review schema before upgrade.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=OBJECT_ID(N'dbo.Discipline') AND name=N'NormalizedName' AND max_length=160 AND definition LIKE N'%NormalizeFlow2Name%') THROW 51009, 'Incompatible normalized-name definition in dbo.Discipline.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=OBJECT_ID(N'dbo.Room') AND name=N'NormalizedName' AND max_length=160 AND definition LIKE N'%NormalizeFlow2Name%') THROW 51009, 'Incompatible normalized-name definition in dbo.Room.', 1;
IF NOT EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=OBJECT_ID(N'dbo.Class') AND name=N'NormalizedName' AND max_length=200 AND definition LIKE N'%NormalizeFlow2Name%') THROW 51009, 'Incompatible normalized-name definition in dbo.Class.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
