SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

DECLARE @today date = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS date);
DECLARE @passwordHash varchar(200) = N'$2a$11$Pg2II6mLLJckDVq3MotNgei88QWFHN2hXmeDkv4jwSajh.4npLnYG'; -- Admin@123

-- =========================================================================
-- 1. UC17: Chuẩn hóa danh mục Bộ môn (Discipline)
-- =========================================================================
UPDATE dbo.Discipline
SET Name = N'Gym & Fitness',
    Description = N'Rèn luyện sức mạnh, kỹ thuật tạ và thể lực nền tảng theo khung giờ cố định cùng HLV.',
    IsActive = 1
WHERE Id = 1;

UPDATE dbo.Discipline
SET Description = N'Cân bằng hơi thở, độ dẻo dai và phục hồi cơ xương khớp.',
    IsActive = 1
WHERE Id = 2;

UPDATE dbo.Discipline
SET Description = N'Kỹ thuật di chuyển, tổ hợp đòn và phản xạ đối kháng.',
    IsActive = 1
WHERE Id = 3;

UPDATE dbo.Discipline
SET Description = N'Kỹ thuật bơi sải, bơi ếch, bơi ngửa và an toàn dưới nước.',
    IsActive = 1
WHERE Id = 4;

UPDATE dbo.Discipline
SET Description = N'Bộ môn nhảy thể lực theo nhạc (đang tạm ngưng mở lớp mới).',
    IsActive = 0
WHERE Id = 5;

IF NOT EXISTS (SELECT 1 FROM dbo.Discipline WHERE Name = N'Pilates')
BEGIN
    INSERT INTO dbo.Discipline (Name, Description, IsActive)
    VALUES (N'Pilates', N'Tập trung kiểm soát cơ lõi (core), chỉnh tư thế và tăng sự linh hoạt.', 1);
END

DECLARE @discPilates int = (SELECT TOP 1 Id FROM dbo.Discipline WHERE Name = N'Pilates');

-- =========================================================================
-- 2. UC18: Bổ sung Phòng tập (Room) để demo chọn phòng rảnh / trùng lịch
-- =========================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Room WHERE Name = N'Phòng Gym & Thể lực')
BEGIN
    INSERT INTO dbo.Room (Name, Location, RoomType, Capacity, IsActive)
    VALUES (N'Phòng Gym & Thể lực', N'Tầng 1', N'GYM', 24, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.Room WHERE Name = N'Studio C (Yoga & Pilates)')
BEGIN
    INSERT INTO dbo.Room (Name, Location, RoomType, Capacity, IsActive)
    VALUES (N'Studio C (Yoga & Pilates)', N'Tầng 3', N'STUDIO', 16, 1);
END

DECLARE @roomGym int = (SELECT TOP 1 Id FROM dbo.Room WHERE Name = N'Phòng Gym & Thể lực');
DECLARE @roomStudioC int = (SELECT TOP 1 Id FROM dbo.Room WHERE Name = N'Studio C (Yoga & Pilates)');

-- =========================================================================
-- 3. UC20 & Review #3: Bổ sung HLV và Chuyên môn (2-3 HLV/bộ môn)
-- =========================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = N'acc-coa-005')
BEGIN
    INSERT INTO dbo.Account (Id, RoleId, Email, PasswordHash, Phone, Status, FailedLoginCount, IsLocked)
    VALUES (N'acc-coa-005', 2, N'coach05@sportscenter.com', @passwordHash, N'0912345605', N'Active', 0, 0);
END
ELSE
BEGIN
    UPDATE dbo.Account SET RoleId = 2 WHERE Id = N'acc-coa-005';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Coach WHERE AccountId = N'acc-coa-005')
BEGIN
    INSERT INTO dbo.Coach (AccountId, FullName, Specialization, WorkSchedule)
    VALUES (N'acc-coa-005', N'Trần Quốc Bảo', N'Boxing, Bơi lội', N'Thứ 2 - Thứ 7 (Ca sáng & tối)');
END

IF NOT EXISTS (SELECT 1 FROM dbo.Account WHERE Id = N'acc-coa-006')
BEGIN
    INSERT INTO dbo.Account (Id, RoleId, Email, PasswordHash, Phone, Status, FailedLoginCount, IsLocked)
    VALUES (N'acc-coa-006', 2, N'coach06@sportscenter.com', @passwordHash, N'0912345606', N'Active', 0, 0);
END
ELSE
BEGIN
    UPDATE dbo.Account SET RoleId = 2 WHERE Id = N'acc-coa-006';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Coach WHERE AccountId = N'acc-coa-006')
BEGIN
    INSERT INTO dbo.Coach (AccountId, FullName, Specialization, WorkSchedule)
    VALUES (N'acc-coa-006', N'Võ Thanh Thảo', N'Yoga, Pilates, Gym & Fitness', N'Thứ 2 - Chủ nhật (Ca sáng & chiều)');
END

-- Đồng bộ chuỗi Specialization và bảng CoachDiscipline
UPDATE dbo.Coach SET Specialization = N'Gym & Fitness, Yoga, Pilates' WHERE AccountId = N'acc-coa-001';
UPDATE dbo.Coach SET Specialization = N'Boxing, Gym & Fitness' WHERE AccountId = N'acc-coa-002';
UPDATE dbo.Coach SET Specialization = N'Bơi lội, Yoga, Pilates' WHERE AccountId = N'acc-coa-003';

IF @discPilates IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-001' AND DisciplineId = @discPilates)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-001', @discPilates);

IF NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-002' AND DisciplineId = 1)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-002', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-003' AND DisciplineId = 2)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-003', 2);

IF @discPilates IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-003' AND DisciplineId = @discPilates)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-003', @discPilates);

IF NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-005' AND DisciplineId = 3)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-005', 3);

IF NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-005' AND DisciplineId = 4)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-005', 4);

IF NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-006' AND DisciplineId = 1)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-006', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-006' AND DisciplineId = 2)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-006', 2);

IF @discPilates IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CoachDiscipline WHERE CoachAccountId = N'acc-coa-006' AND DisciplineId = @discPilates)
    INSERT INTO dbo.CoachDiscipline (CoachAccountId, DisciplineId) VALUES (N'acc-coa-006', @discPilates);

-- =========================================================================
-- 4. UC19: Bổ sung Lớp học Pilates phục hồi cơ lõi (OPEN)
-- =========================================================================
IF @discPilates IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Class WHERE Name = N'Pilates phục hồi cơ lõi')
BEGIN
    INSERT INTO dbo.Class (DisciplineId, Name, Description, Capacity, Status, CreatedBy)
    VALUES (@discPilates, N'Pilates phục hồi cơ lõi', N'Tăng cường sức mạnh cơ core, cải thiện tư thế cột sống theo khung giờ cố định cùng HLV.', 14, N'OPEN', N'acc-mgr-001');
END

DECLARE @classYoga int = (SELECT TOP 1 Id FROM dbo.Class WHERE Name = N'Yoga cân bằng cơ bản');
DECLARE @classBoxing int = (SELECT TOP 1 Id FROM dbo.Class WHERE Name = N'Boxing trung cấp');
DECLARE @classSwim int = (SELECT TOP 1 Id FROM dbo.Class WHERE Name = N'Bơi lội kỹ thuật toàn diện');
DECLARE @classGym int = (SELECT TOP 1 Id FROM dbo.Class WHERE Name = N'Gym & Thể lực nền tảng');
DECLARE @classPilates int = (SELECT TOP 1 Id FROM dbo.Class WHERE Name = N'Pilates phục hồi cơ lõi');
DECLARE @classDraftYoga int = (SELECT TOP 1 Id FROM dbo.Class WHERE Name = N'Yoga nâng cao buổi tối');

-- Thêm 1 buổi cho lớp DRAFT (Yoga nâng cao buổi tối) để Manager có thể demo chuyển trạng thái DRAFT -> OPEN ngay trên UI (UC19)
IF @classDraftYoga IS NOT NULL AND @roomStudioC IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.ClassSession WHERE ClassId = @classDraftYoga AND Status = N'SCHEDULED')
BEGIN
    INSERT INTO dbo.ClassSession (ClassId, CoachId, RoomId, SessionDate, StartTime, EndTime, Capacity, BookedCount, Status, Notes, CreatedBy)
    VALUES (@classDraftYoga, N'acc-coa-006', @roomStudioC, DATEADD(day, 4, @today), '19:30:00', '21:00:00', 12, 0, N'SCHEDULED', N'Ca 7 tối muộn · Buổi học sẵn sàng để mở lớp.', N'acc-mgr-001');
END

-- =========================================================================
-- 5. UC21/UC24/UC29: Tạo kịch bản Buổi học ĐÃ ĐẦY CHỖ (4/4) và CÒN 1 CHỖ CUỐI (3/4)
-- =========================================================================
IF @classPilates IS NOT NULL AND @roomStudioC IS NOT NULL
BEGIN
    -- Buổi Ca 5 (16:00-17:00) ngày mai: ĐÃ ĐỦ CHỖ (4/4) để demo CLASS_FULL
    IF NOT EXISTS (SELECT 1 FROM dbo.ClassSession WHERE ClassId = @classPilates AND SessionDate = DATEADD(day, 1, @today) AND StartTime = '16:00:00')
    BEGIN
        INSERT INTO dbo.ClassSession (ClassId, CoachId, RoomId, SessionDate, StartTime, EndTime, Capacity, BookedCount, Status, Notes, CreatedBy)
        VALUES (@classPilates, N'acc-coa-006', @roomStudioC, DATEADD(day, 1, @today), '16:00:00', '17:00:00', 4, 0, N'SCHEDULED', N'Ca 5 chiều muộn · Nhóm nhỏ 4 học viên (demo lớp đầy chỗ).', N'acc-mgr-001');
    END

    -- Buổi Ca 6 (18:00-19:30) ngày mốt: CÒN ĐÚNG 1 CHỖ TRỐNG (3/4) để demo đặt chỗ cuối
    IF NOT EXISTS (SELECT 1 FROM dbo.ClassSession WHERE ClassId = @classPilates AND SessionDate = DATEADD(day, 2, @today) AND StartTime = '18:00:00')
    BEGIN
        INSERT INTO dbo.ClassSession (ClassId, CoachId, RoomId, SessionDate, StartTime, EndTime, Capacity, BookedCount, Status, Notes, CreatedBy)
        VALUES (@classPilates, N'acc-coa-001', @roomStudioC, DATEADD(day, 2, @today), '18:00:00', '19:30:00', 4, 0, N'SCHEDULED', N'Ca 6 tối · Nhóm nhỏ 4 học viên (còn 1 chỗ trống cuối).', N'acc-mgr-001');
    END
END

-- Bổ sung buổi học mở (OPEN) cho Ca 7 (19:30-21:00) để đủ cả 7/7 khung giờ cố định trên thanh SlotFilterBar
IF @classGym IS NOT NULL AND @roomGym IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.ClassSession WHERE ClassId = @classGym AND SessionDate = DATEADD(day, 1, @today) AND StartTime = '19:30:00')
BEGIN
    INSERT INTO dbo.ClassSession (ClassId, CoachId, RoomId, SessionDate, StartTime, EndTime, Capacity, BookedCount, Status, Notes, CreatedBy)
    VALUES (@classGym, N'acc-coa-002', @roomGym, DATEADD(day, 1, @today), '19:30:00', '21:00:00', 18, 0, N'SCHEDULED', N'Ca 7 tối muộn · Rèn luyện sức mạnh & thể lực sau giờ làm cùng HLV.', N'acc-mgr-001');
END

-- Xóa mọi buổi học nằm ngoài 7 khung giờ cố định (ví dụ 21:00-22:00) để đảm bảo 100% không có giờ tự do
DELETE FROM dbo.ClassSession WHERE StartTime = '21:00:00' AND EndTime = '22:00:00';

DECLARE @sessionFull int = (
    SELECT TOP 1 Id FROM dbo.ClassSession
    WHERE ClassId = @classPilates AND SessionDate = DATEADD(day, 1, @today) AND StartTime = '16:00:00'
);
DECLARE @sessionLastSeat int = (
    SELECT TOP 1 Id FROM dbo.ClassSession
    WHERE ClassId = @classPilates AND SessionDate = DATEADD(day, 2, @today) AND StartTime = '18:00:00'
);

-- Đảm bảo acc-mem-002 có gói tập CONFIRMED để tham gia đăng ký mẫu
IF NOT EXISTS (SELECT 1 FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-002' AND Status = N'CONFIRMED')
BEGIN
    INSERT INTO dbo.MemberSubscription (MemberId, PackageId, PackageName, PackagePrice, DurationMonths, Benefits, StartDate, EndDate, Kind, Status, IsSuspended)
    VALUES (N'acc-mem-002', 3, N'Gói Năng Động 6 Tháng', 2400000.00, 6, N'["Toàn quyền sử dụng Gym","Bể bơi bốn mùa","Xông hơi sauna"]', DATEADD(day, -20, @today), DATEADD(day, 160, @today), N'NEW', N'CONFIRMED', 0);
END

DECLARE @subMem001 int = (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-001' AND Status = N'CONFIRMED' ORDER BY Id DESC);
DECLARE @subMem002 int = (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-002' AND Status = N'CONFIRMED' ORDER BY Id DESC);
DECLARE @subMem101 int = (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-101' AND Status = N'CONFIRMED' ORDER BY Id DESC);
DECLARE @subMem102 int = (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-102' AND Status = N'CONFIRMED' ORDER BY Id DESC);
DECLARE @subMem104 int = (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-104' AND Status = N'CONFIRMED' ORDER BY Id DESC);
DECLARE @subMem105 int = (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-105' AND Status = N'CONFIRMED' ORDER BY Id DESC);

-- Điền đủ 4 học viên vào @sessionFull (acc-mem-002, acc-mem-101, acc-mem-102, acc-mem-104) để Phạm Hoàng Nam (acc-mem-001) nhìn thấy nút "Buổi học đã đủ chỗ"
IF @sessionFull IS NOT NULL
BEGIN
    DELETE FROM dbo.ClassRegistration WHERE SessionId = @sessionFull AND MemberId = N'acc-mem-105';

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionFull AND MemberId = N'acc-mem-002' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionFull, N'acc-mem-002', @subMem002, N'BOOKED', N'MEMBER', N'acc-mem-002');

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionFull AND MemberId = N'acc-mem-101' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionFull, N'acc-mem-101', @subMem101, N'BOOKED', N'MEMBER', N'acc-mem-101');

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionFull AND MemberId = N'acc-mem-102' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionFull, N'acc-mem-102', @subMem102, N'BOOKED', N'RECEPTIONIST', N'acc-rec-001');

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionFull AND MemberId = N'acc-mem-104' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionFull, N'acc-mem-104', @subMem104, N'BOOKED', N'MEMBER', N'acc-mem-104');
END

-- Điền 3 học viên vào @sessionLastSeat (còn đúng 1 chỗ trống cho Phạm Hoàng Nam đặt thử!)
IF @sessionLastSeat IS NOT NULL
BEGIN
    DELETE FROM dbo.ClassRegistration WHERE SessionId = @sessionLastSeat AND MemberId = N'acc-mem-105';

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionLastSeat AND MemberId = N'acc-mem-002' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionLastSeat, N'acc-mem-002', @subMem002, N'BOOKED', N'MEMBER', N'acc-mem-002');

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionLastSeat AND MemberId = N'acc-mem-101' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionLastSeat, N'acc-mem-101', @subMem101, N'BOOKED', N'MEMBER', N'acc-mem-101');

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionLastSeat AND MemberId = N'acc-mem-102' AND Status = N'BOOKED')
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy)
        VALUES (@sessionLastSeat, N'acc-mem-102', @subMem102, N'BOOKED', N'RECEPTIONIST', N'acc-rec-001');
END

-- Thêm 1 đăng ký đã hủy (CANCELED) cho Phạm Hoàng Nam (acc-mem-001) để trang "Lịch của tôi" (UC26) có sẵn lịch sử hủy kèm lý do
DECLARE @sessionBoxingTomorrow int = (
    SELECT TOP 1 Id FROM dbo.ClassSession
    WHERE ClassId = @classBoxing AND SessionDate = DATEADD(day, 1, @today) AND Status = N'SCHEDULED'
);
IF @sessionBoxingTomorrow IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @sessionBoxingTomorrow AND MemberId = N'acc-mem-001')
BEGIN
    INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy, CanceledAt, CanceledBy, CancelReason)
    VALUES (@sessionBoxingTomorrow, N'acc-mem-001', @subMem001, N'CANCELED', N'MEMBER', N'acc-mem-001', SYSDATETIME(), N'acc-mem-001', N'Bận lịch họp đột xuất, xin chuyển sang ca tập ngày hôm sau.');
END

-- Đồng bộ chính xác cột BookedCount của mọi ClassSession theo tổng số đăng ký BOOKED
UPDATE s
SET s.BookedCount = (
    SELECT COUNT(*)
    FROM dbo.ClassRegistration r
    WHERE r.SessionId = s.Id AND r.Status = N'BOOKED'
)
FROM dbo.ClassSession s;

-- =========================================================================
-- 6. UC31: Bổ sung Thông báo mẫu (Notification) cho Thành viên & HLV
-- =========================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Notification WHERE RecipientId = N'acc-mem-001' AND Type = N'SESSION_RESCHEDULED')
BEGIN
    INSERT INTO dbo.Notification (RecipientId, Type, Title, Body, EntityType, EntityId, EventAt, IsRead)
    VALUES (
        N'acc-mem-001',
        N'SESSION_RESCHEDULED',
        N'Cập nhật phòng tập cho buổi Yoga cân bằng cơ bản',
        N'Buổi học Ca 2 (08:00–09:00) tại Studio A đã được xác nhận danh sách lớp cùng HLV Nguyễn Văn Thể.',
        N'ClassSession',
        N'3',
        DATEADD(minute, -45, SYSDATETIME()),
        0
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Notification WHERE RecipientId = N'acc-coa-001' AND Type = N'SESSION_COACH_CHANGED')
BEGIN
    INSERT INTO dbo.Notification (RecipientId, Type, Title, Body, EntityType, EntityId, EventAt, IsRead)
    VALUES (
        N'acc-coa-001',
        N'SESSION_COACH_CHANGED',
        N'Phân công phụ trách lớp Pilates phục hồi cơ lõi',
        N'Quản lý trung tâm đã phân công bạn phụ trách buổi Pilates phục hồi cơ lõi · Ca 6 (18:00–19:30) tại Studio C.',
        N'ClassSession',
        CAST(ISNULL(@sessionLastSeat, 3) AS varchar(40)),
        DATEADD(minute, -30, SYSDATETIME()),
        0
    );
END

-- =========================================================================
-- 7. UC32: Bổ sung Nhật ký hệ thống (AuditLog) cho Lớp học & Lịch học
-- =========================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.AuditLog WHERE Action = N'CLASS_CREATED' AND ClassId = @classPilates)
BEGIN
    INSERT INTO dbo.AuditLog (Id, AccountId, Action, EntityType, EntityId, Description, CreatedAt, ClassId, SessionId, OperationId)
    VALUES (
        REPLACE(LOWER(CAST(NEWID() AS varchar(36))), '-', ''),
        N'acc-mgr-001',
        N'CLASS_CREATED',
        N'Class',
        CAST(@classPilates AS varchar(50)),
        N'Quản lý trung tâm tạo lớp mới: Pilates phục hồi cơ lõi (sức chứa 14, trạng thái OPEN).',
        DATEADD(hour, -3, SYSDATETIME()),
        @classPilates,
        NULL,
        NEWID()
    );
END

IF @sessionLastSeat IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.AuditLog WHERE Action = N'SESSION_CREATED' AND SessionId = @sessionLastSeat)
BEGIN
    INSERT INTO dbo.AuditLog (Id, AccountId, Action, EntityType, EntityId, Description, CreatedAt, ClassId, SessionId, OperationId)
    VALUES (
        REPLACE(LOWER(CAST(NEWID() AS varchar(36))), '-', ''),
        N'acc-mgr-001',
        N'SESSION_CREATED',
        N'ClassSession',
        CAST(@sessionLastSeat AS varchar(50)),
        N'Lập lịch buổi học Pilates phục hồi cơ lõi · Ca 6 (18:00–19:30) tại Studio C, HLV Nguyễn Văn Thể.',
        DATEADD(hour, -2, SYSDATETIME()),
        @classPilates,
        @sessionLastSeat,
        NEWID()
    );
END
