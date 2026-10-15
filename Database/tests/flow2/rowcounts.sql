SET NOCOUNT ON;
SELECT t.name + '=' + CAST(SUM(p.rows) AS varchar(20))
FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY t.name ORDER BY t.name;
