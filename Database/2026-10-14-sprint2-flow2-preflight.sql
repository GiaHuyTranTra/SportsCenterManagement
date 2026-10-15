-- Read-only inspection. Run against the intended database BEFORE reviewing/applying the upgrade.
SET NOCOUNT ON;
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;

SELECT expected.TableName,
       CASE WHEN OBJECT_ID(N'dbo.' + expected.TableName, N'U') IS NULL THEN N'MISSING' ELSE N'EXISTS' END AS State
FROM (VALUES (N'Account'),(N'Member'),(N'Coach'),(N'MembershipPackage'),(N'MemberSubscription'),
             (N'Discipline'),(N'AuditLog'),(N'Room'),(N'Class'),(N'ClassSession'),(N'ClassRegistration'),
             (N'MembershipPackageSport'),(N'ClassChangeEvent'),(N'Notification')) expected(TableName);

SELECT OBJECT_NAME(c.object_id) AS TableName, c.name AS ColumnName,
       TYPE_NAME(c.user_type_id) AS SqlType, c.max_length AS ByteLength, c.is_nullable, c.is_computed
FROM sys.columns c
WHERE c.object_id IN (OBJECT_ID(N'dbo.Room'),OBJECT_ID(N'dbo.Class'),OBJECT_ID(N'dbo.ClassSession'),
    OBJECT_ID(N'dbo.ClassRegistration'),OBJECT_ID(N'dbo.Notification'),OBJECT_ID(N'dbo.Discipline'))
ORDER BY TableName, c.column_id;

SELECT OBJECT_NAME(parent_object_id) AS TableName, name, delete_referential_action_desc,
       is_disabled, is_not_trusted
FROM sys.foreign_keys
WHERE parent_object_id IN (OBJECT_ID(N'dbo.ClassSession'),OBJECT_ID(N'dbo.ClassRegistration'),OBJECT_ID(N'dbo.Notification'));

SELECT OBJECT_NAME(object_id) AS TableName, name, is_unique, filter_definition, is_disabled
FROM sys.indexes
WHERE object_id IN (OBJECT_ID(N'dbo.Room'),OBJECT_ID(N'dbo.Class'),OBJECT_ID(N'dbo.ClassSession'),
    OBJECT_ID(N'dbo.ClassRegistration'),OBJECT_ID(N'dbo.Notification'));

IF COL_LENGTH(N'dbo.MemberSubscription',N'Status') IS NOT NULL
    EXEC(N'SELECT Status, COUNT(*) AS Records FROM dbo.MemberSubscription GROUP BY Status;');

IF COL_LENGTH(N'dbo.ClassSession',N'BookedCount') IS NOT NULL AND OBJECT_ID(N'dbo.ClassRegistration',N'U') IS NOT NULL
BEGIN
    EXEC(N'SELECT s.Id, s.Status, s.Capacity, s.BookedCount,
        (SELECT COUNT(*) FROM dbo.ClassRegistration r WHERE r.SessionId=s.Id AND r.Status=''BOOKED'') AS ActualBooked
        FROM dbo.ClassSession s
        WHERE s.BookedCount<0 OR s.BookedCount>s.Capacity OR s.StartTime>=s.EndTime OR
            s.BookedCount<>(SELECT COUNT(*) FROM dbo.ClassRegistration r WHERE r.SessionId=s.Id AND r.Status=''BOOKED'');');
    EXEC(N'SELECT SessionId,MemberId,COUNT(*) AS ActiveDuplicates FROM dbo.ClassRegistration
        WHERE Status=''BOOKED'' GROUP BY SessionId,MemberId HAVING COUNT(*)>1;');
END;
