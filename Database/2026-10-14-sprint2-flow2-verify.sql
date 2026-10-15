-- Read-only verification AFTER a successful upgrade. Empty violation result sets are expected.
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName;

SELECT s.Id, s.BookedCount, COUNT(r.Id) AS ActualBooked
FROM dbo.ClassSession s LEFT JOIN dbo.ClassRegistration r ON r.SessionId=s.Id AND r.Status='BOOKED'
GROUP BY s.Id,s.BookedCount HAVING s.BookedCount<>COUNT(r.Id);

SELECT r.Id AS InvalidRegistration, r.SessionId
FROM dbo.ClassRegistration r JOIN dbo.ClassSession s ON s.Id=r.SessionId
WHERE r.Status='BOOKED' AND s.Status='CANCELED';

SELECT SessionId,MemberId,COUNT(*) AS Duplicates
FROM dbo.ClassRegistration WHERE Status='BOOKED'
GROUP BY SessionId,MemberId HAVING COUNT(*)>1;

SELECT name,is_disabled,is_not_trusted FROM sys.foreign_keys
WHERE parent_object_id IN (OBJECT_ID(N'dbo.ClassSession'),OBJECT_ID(N'dbo.ClassRegistration'),OBJECT_ID(N'dbo.Notification'))
    AND (is_disabled=1 OR is_not_trusted=1 OR delete_referential_action<>0);

SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints
WHERE parent_object_id IN (OBJECT_ID(N'dbo.Room'),OBJECT_ID(N'dbo.Class'),OBJECT_ID(N'dbo.ClassSession'),OBJECT_ID(N'dbo.ClassRegistration'))
    AND (is_disabled=1 OR is_not_trusted=1);

SELECT OBJECT_NAME(object_id) AS TableName,name,is_unique,filter_definition FROM sys.indexes
WHERE name IN (N'UQ_Discipline_NormalizedName',N'UQ_Room_NormalizedName',N'UQ_Class_NormalizedName_Active',
    N'UQ_ClassRegistration_ActiveBooking',N'UQ_ClassChangeEvent_Operation',N'UQ_Notification_EventRecipient');
