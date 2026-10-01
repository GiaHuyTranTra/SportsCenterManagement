SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Packages TABLE
(
    Name nvarchar(80) NOT NULL PRIMARY KEY,
    Price decimal(18, 0) NOT NULL,
    DurationMonths int NOT NULL,
    Benefits nvarchar(max) NOT NULL,
    IsActive bit NOT NULL
);

INSERT INTO @Packages (Name, Price, DurationMonths, Benefits, IsActive)
VALUES
    (N'Gói Cơ bản 1 tháng', 300000, 1, N'["Khu tập gym","Tủ đồ trong buổi tập"]', 1),
    (N'Gói Tiêu chuẩn 1 tháng', 500000, 1, N'["Khu tập gym","Tủ đồ","Một buổi đánh giá thể lực"]', 1),
    (N'Gói Premium 1 tháng', 800000, 1, N'["Khu tập gym","Tủ đồ","Lớp tập nhóm","Khăn tập"]', 1),
    (N'Gói Tiêu chuẩn 3 tháng', 1350000, 3, N'["Khu tập gym","Tủ đồ","Hai buổi đánh giá thể lực","Một buổi tư vấn dinh dưỡng"]', 1),
    (N'Gói Premium 3 tháng', 2100000, 3, N'["Khu tập gym","Tủ đồ","Lớp tập nhóm","Khăn tập","Hai buổi tư vấn huấn luyện"]', 1),
    (N'Gói Tiêu chuẩn 12 tháng', 4800000, 12, N'["Khu tập gym","Tủ đồ","Đánh giá thể lực định kỳ","Bốn buổi tư vấn dinh dưỡng"]', 1),
    (N'Gói Premium 12 tháng', 7200000, 12, N'["Khu tập gym","Tủ đồ","Lớp tập nhóm","Khăn tập","Tư vấn huấn luyện hàng tháng"]', 1),
    (N'Gói cũ ngừng bán 3 tháng', 999000, 3, N'["Dữ liệu kiểm thử gói ngừng hoạt động"]', 0);

UPDATE target
SET
    target.Price = source.Price,
    target.DurationMonths = source.DurationMonths,
    target.Benefits = source.Benefits,
    target.IsActive = source.IsActive,
    target.UpdatedAt = SYSUTCDATETIME()
FROM dbo.MembershipPackage AS target
INNER JOIN @Packages AS source ON source.Name = target.Name
WHERE target.Price <> source.Price
    OR target.DurationMonths <> source.DurationMonths
    OR target.Benefits <> source.Benefits
    OR target.IsActive <> source.IsActive;

INSERT INTO dbo.MembershipPackage (Name, Price, DurationMonths, Benefits, IsActive)
SELECT source.Name, source.Price, source.DurationMonths, source.Benefits, source.IsActive
FROM @Packages AS source
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.MembershipPackage AS target
    WHERE target.Name = source.Name
);

IF
(
    SELECT COUNT(*)
    FROM dbo.MembershipPackage AS target
    INNER JOIN @Packages AS source ON source.Name = target.Name
) <> 8
BEGIN
    THROW 51000, 'Demo membership package seed verification failed.', 1;
END;

COMMIT TRANSACTION;

SELECT Id, Name, Price, DurationMonths, IsActive
FROM dbo.MembershipPackage
WHERE Name IN (SELECT Name FROM @Packages)
ORDER BY IsActive DESC, DurationMonths, Price;
