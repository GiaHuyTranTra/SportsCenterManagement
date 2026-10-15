SET NOCOUNT ON;
SELECT 'VERSION ' + CAST(SERVERPROPERTY('ProductVersion') AS varchar(40)) + ' compat=' + CAST((SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) AS varchar) + ' dbcollation=' + CAST(DATABASEPROPERTYEX(DB_NAME(),'Collation') AS varchar(100)) AS Info;
SELECT 'FUNC NormalizeFlow2Name exists=' + CAST(CASE WHEN OBJECT_ID('dbo.NormalizeFlow2Name') IS NULL THEN 0 ELSE 1 END AS varchar) AS Info;
SELECT 'ROWS Account=' + CAST((SELECT COUNT(*) FROM dbo.Account) AS varchar) + ' Coach=' + CAST((SELECT COUNT(*) FROM dbo.Coach) AS varchar) + ' Member=' + CAST((SELECT COUNT(*) FROM dbo.Member) AS varchar) + ' Package=' + CAST((SELECT COUNT(*) FROM dbo.MembershipPackage) AS varchar) + ' Sub=' + CAST((SELECT COUNT(*) FROM dbo.MemberSubscription) AS varchar) + ' Discipline=' + CAST((SELECT COUNT(*) FROM dbo.Discipline) AS varchar) + ' Audit=' + CAST((SELECT COUNT(*) FROM dbo.AuditLog) AS varchar) AS Info;
SELECT 'COL ' + OBJECT_NAME(c.object_id) + '.' + c.name + ' ' + TYPE_NAME(c.user_type_id) + '(' + CAST(c.max_length AS varchar) + ') null=' + CAST(c.is_nullable AS varchar) AS Info
FROM sys.columns c
WHERE (c.object_id = OBJECT_ID('dbo.Account') AND c.name IN ('Id')) OR (c.object_id = OBJECT_ID('dbo.Coach') AND c.name = 'AccountId')
   OR (c.object_id = OBJECT_ID('dbo.Member') AND c.name = 'AccountId') OR (c.object_id = OBJECT_ID('dbo.MembershipPackage') AND c.name = 'Id')
   OR (c.object_id = OBJECT_ID('dbo.MemberSubscription') AND c.name = 'Id')
   OR (c.object_id = OBJECT_ID('dbo.AuditLog'))
ORDER BY OBJECT_NAME(c.object_id), c.column_id;
SELECT 'PK ' + OBJECT_NAME(parent_object_id) + ' ' + name AS Info FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id IN (OBJECT_ID('dbo.Account'), OBJECT_ID('dbo.Coach'), OBJECT_ID('dbo.Member'), OBJECT_ID('dbo.MembershipPackage'), OBJECT_ID('dbo.MemberSubscription'), OBJECT_ID('dbo.Discipline'), OBJECT_ID('dbo.AuditLog'));
SELECT 'IDX Discipline ' + name + ' unique=' + CAST(is_unique AS varchar) AS Info FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Discipline') AND name IS NOT NULL;
SELECT 'DISCIPLINE ' + CAST(Id AS varchar) + ' [' + Name + ']' AS Info FROM dbo.Discipline;
SELECT 'DISC_NAME_DUP_CI ' + UPPER(LTRIM(RTRIM(Name))) + ' x' + CAST(COUNT(*) AS varchar) AS Info FROM dbo.Discipline GROUP BY UPPER(LTRIM(RTRIM(Name))) HAVING COUNT(*) > 1;
SELECT 'SUBSCRIPTION_STATUS ' + Status + ' x' + CAST(COUNT(*) AS varchar) AS Info FROM dbo.MemberSubscription GROUP BY Status;
SELECT 'OTHER_OBJECTS_ON_NAMES ' + name AS Info FROM sys.objects WHERE name IN ('NormalizeFlow2Name','Room','Class','ClassSession','ClassRegistration','ClassChangeEvent','Notification','MembershipPackageSport');
