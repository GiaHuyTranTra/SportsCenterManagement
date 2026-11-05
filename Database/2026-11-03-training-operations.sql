SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.ClassSessionAttendance', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClassSessionAttendance (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassSessionAttendance PRIMARY KEY,
        RegistrationId INT NOT NULL,
        SessionId INT NOT NULL,
        MemberId NVARCHAR(400) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_ClassSessionAttendance_Status DEFAULT (N'PRESENT'),
        Note NVARCHAR(500) NULL,
        RecordedBy NVARCHAR(400) NOT NULL,
        RecordedAt DATETIME2 NOT NULL CONSTRAINT DF_ClassSessionAttendance_RecordedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_ClassSessionAttendance_Registration UNIQUE (RegistrationId),
        CONSTRAINT FK_ClassSessionAttendance_Registration FOREIGN KEY (RegistrationId) REFERENCES dbo.ClassRegistration(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ClassSessionAttendance_Session FOREIGN KEY (SessionId) REFERENCES dbo.ClassSession(Id),
        CONSTRAINT FK_ClassSessionAttendance_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.CenterVisit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CenterVisit (
        Id NVARCHAR(64) NOT NULL CONSTRAINT PK_CenterVisit PRIMARY KEY,
        MemberId NVARCHAR(400) NOT NULL,
        VisitDate DATE NOT NULL,
        CheckedInAt DATETIME2 NOT NULL,
        CheckedOutAt DATETIME2 NULL,
        CheckedInBy NVARCHAR(400) NOT NULL,
        CheckedOutBy NVARCHAR(400) NULL,
        Note NVARCHAR(500) NULL,
        CONSTRAINT FK_CenterVisit_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SupportRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportRequest (
        Id NVARCHAR(64) NOT NULL CONSTRAINT PK_SupportRequest PRIMARY KEY,
        MemberId NVARCHAR(400) NOT NULL,
        Category NVARCHAR(80) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        Note NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_SupportRequest_Status DEFAULT (N'OPEN'),
        ResolutionNote NVARCHAR(2000) NULL,
        CreatedBy NVARCHAR(400) NOT NULL,
        UpdatedBy NVARCHAR(400) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportRequest_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SupportRequest_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SupportRequest_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.MemberTrainingProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MemberTrainingProfile (
        MemberId NVARCHAR(400) NOT NULL CONSTRAINT PK_MemberTrainingProfile PRIMARY KEY,
        FitnessGoal NVARCHAR(300) NOT NULL,
        FitnessLevel NVARCHAR(30) NOT NULL CONSTRAINT DF_MemberTrainingProfile_Level DEFAULT (N'BEGINNER'),
        CurrentWeightKg DECIMAL(5,1) NULL,
        TargetWeightKg DECIMAL(5,1) NULL,
        Notes NVARCHAR(1000) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MemberTrainingProfile_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_MemberTrainingProfile_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId) ON DELETE CASCADE
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.TrainingPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrainingPlan (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingPlan PRIMARY KEY,
        CoachId NVARCHAR(400) NOT NULL,
        PlanType NVARCHAR(20) NOT NULL CONSTRAINT DF_TrainingPlan_Type DEFAULT (N'INDIVIDUAL'),
        MemberId NVARCHAR(400) NULL,
        ClassId INT NULL,
        Title NVARCHAR(200) NOT NULL,
        Goal NVARCHAR(400) NOT NULL,
        ExercisesJson NVARCHAR(MAX) NOT NULL,
        Homework NVARCHAR(1000) NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TrainingPlan_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TrainingPlan_Coach FOREIGN KEY (CoachId) REFERENCES dbo.Coach(AccountId),
        CONSTRAINT FK_TrainingPlan_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId),
        CONSTRAINT FK_TrainingPlan_Class FOREIGN KEY (ClassId) REFERENCES dbo.Class(Id)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.TrainingResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrainingResult (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingResult PRIMARY KEY,
        CoachId NVARCHAR(400) NOT NULL,
        MemberId NVARCHAR(400) NOT NULL,
        SessionId INT NULL,
        PlanId INT NULL,
        RecordedDate DATE NOT NULL,
        CompletedExercises NVARCHAR(1000) NOT NULL,
        CompletionRate INT NOT NULL CONSTRAINT DF_TrainingResult_Rate DEFAULT (100),
        ProgressRating NVARCHAR(30) NOT NULL CONSTRAINT DF_TrainingResult_Rating DEFAULT (N'ON_TRACK'),
        ResultSummary NVARCHAR(1000) NOT NULL,
        CoachFeedback NVARCHAR(1500) NOT NULL,
        HomeworkAssigned NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TrainingResult_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TrainingResult_Coach FOREIGN KEY (CoachId) REFERENCES dbo.Coach(AccountId),
        CONSTRAINT FK_TrainingResult_Member FOREIGN KEY (MemberId) REFERENCES dbo.Member(AccountId),
        CONSTRAINT FK_TrainingResult_Session FOREIGN KEY (SessionId) REFERENCES dbo.ClassSession(Id),
        CONSTRAINT FK_TrainingResult_Plan FOREIGN KEY (PlanId) REFERENCES dbo.TrainingPlan(Id)
    );
END
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.RolePermissionConfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissionConfig (
        FeatureKey NVARCHAR(80) NOT NULL CONSTRAINT PK_RolePermissionConfig PRIMARY KEY,
        AllowedRolesCsv NVARCHAR(400) NOT NULL,
        UpdatedBy NVARCHAR(400) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_RolePermissionConfig_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO
SET QUOTED_IDENTIFIER ON;

-- Làm sạch dữ liệu bị lỗi mã hóa '?' và nạp lại chuẩn Unicode tiếng Việt
DELETE FROM dbo.TrainingResult;
DELETE FROM dbo.TrainingPlan;
DELETE FROM dbo.MemberTrainingProfile;
DELETE FROM dbo.CenterVisit WHERE Id = N'VIS-DEMO-001';
DELETE FROM dbo.SupportRequest WHERE Id = N'SUP-DEMO-001';
DELETE FROM dbo.Notification WHERE Title LIKE N'%??%' OR Body LIKE N'%??%';

-- 1. Hồ sơ mục tiêu tập luyện cho các hội viên
INSERT INTO dbo.MemberTrainingProfile (MemberId, FitnessGoal, FitnessLevel, CurrentWeightKg, TargetWeightKg, Notes, UpdatedAt)
SELECT
    m.AccountId,
    CASE m.AccountId
        WHEN N'acc-mem-001' THEN N'Tăng cơ giảm mỡ, cải thiện độ dẻo dai cột sống và sức bền tim mạch'
        WHEN N'acc-mem-002' THEN N'Xây dựng nền tảng sức mạnh cơ bản và kỹ thuật Squat/Deadlift chuẩn'
        WHEN N'acc-mem-101' THEN N'Tăng sức bền tim mạch, giảm mỡ nội tạng và duy trì vóc dáng cân đối'
        WHEN N'acc-mem-102' THEN N'Cải thiện sự linh hoạt khớp vai, cột sống cổ và kiểm soát nhịp thở Yoga'
        ELSE N'Nâng cao thể lực nền tảng, cải thiện độ linh hoạt và kiểm soát nhịp thở'
    END,
    CASE
        WHEN m.AccountId IN (N'acc-mem-001', N'acc-mem-101') THEN N'INTERMEDIATE'
        ELSE N'BEGINNER'
    END,
    CASE m.AccountId
        WHEN N'acc-mem-001' THEN 71.5
        WHEN N'acc-mem-002' THEN 56.0
        WHEN N'acc-mem-101' THEN 68.0
        ELSE 62.0
    END,
    CASE m.AccountId
        WHEN N'acc-mem-001' THEN 67.0
        WHEN N'acc-mem-002' THEN 53.5
        WHEN N'acc-mem-101' THEN 64.0
        ELSE 59.0
    END,
    CASE m.AccountId
        WHEN N'acc-mem-001' THEN N'Ưu tiên bài tập Core, Yoga phục hồi và tạ tự do mức trung bình.'
        WHEN N'acc-mem-002' THEN N'Cần chú ý khởi động kỹ khớp gối trước mỗi buổi tập.'
        WHEN N'acc-mem-101' THEN N'Có thể tập cường độ cao ngắt quãng (HIIT) kết hợp Pilates.'
        ELSE N'Khởi động kỹ khớp vai và khớp gối trước mỗi buổi tập.'
    END,
    SYSUTCDATETIME()
FROM dbo.Member m;

-- 2. Đảm bảo có 1 buổi học diễn ra NGAY HÔM NAY của HLV acc-coa-001 có học viên đăng ký để kiểm thử điểm danh hợp lệ
DECLARE @today DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE);
DECLARE @todaySessionId INT;

SELECT TOP 1 @todaySessionId = Id
FROM dbo.ClassSession
WHERE CoachId = N'acc-coa-001' AND SessionDate = @today AND Status = N'SCHEDULED';

IF @todaySessionId IS NULL
BEGIN
    INSERT INTO dbo.ClassSession (ClassId, RoomId, CoachId, SessionDate, StartTime, EndTime, Capacity, BookedCount, Status, Notes, CreatedBy, CreatedAt)
    VALUES (1, 1, N'acc-coa-001', @today, '08:00:00', '09:00:00', 15, 2, N'SCHEDULED', N'Buổi tập Yoga cân bằng định kỳ', N'acc-mgr-001', SYSUTCDATETIME());
    SET @todaySessionId = SCOPE_IDENTITY();

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @todaySessionId AND MemberId = N'acc-mem-001')
    BEGIN
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy, RegisteredAt)
        SELECT @todaySessionId, N'acc-mem-001', TOP_SUB.Id, N'BOOKED', N'MEMBER', N'acc-mem-001', DATEADD(DAY, -1, SYSUTCDATETIME())
        FROM (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-001' ORDER BY Id DESC) TOP_SUB;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.ClassRegistration WHERE SessionId = @todaySessionId AND MemberId = N'acc-mem-101')
    BEGIN
        INSERT INTO dbo.ClassRegistration (SessionId, MemberId, MemberSubscriptionId, Status, RegisteredVia, RegisteredBy, RegisteredAt)
        SELECT @todaySessionId, N'acc-mem-101', TOP_SUB.Id, N'BOOKED', N'MEMBER', N'acc-mem-101', DATEADD(DAY, -1, SYSUTCDATETIME())
        FROM (SELECT TOP 1 Id FROM dbo.MemberSubscription WHERE MemberId = N'acc-mem-101' ORDER BY Id DESC) TOP_SUB;
    END
END

-- 3. Kế hoạch tập luyện mẫu (Cá nhân + Cả lớp)
SET IDENTITY_INSERT dbo.TrainingPlan ON;
INSERT INTO dbo.TrainingPlan (Id, CoachId, PlanType, MemberId, ClassId, Title, Goal, ExercisesJson, Homework, StartDate, EndDate, CreatedAt)
VALUES
(1, N'acc-coa-001', N'INDIVIDUAL', N'acc-mem-001', NULL,
 N'Lộ trình 4 tuần Tăng cơ lõi & Linh hoạt cột sống',
 N'Giảm 2kg mỡ thừa, giữ vững Plank 90 giây và hoàn thiện tư thế chiến binh Yoga',
 N'[{"name":"Goblet Squat với tạ đơn","sets":4,"reps":"12 lần","durationMinutes":12,"note":"Giữ thẳng lưng, hạ sâu hết biên độ"},{"name":"Plank siết cơ bụng lõi (Core)","sets":3,"reps":"60 giây","durationMinutes":8,"note":"Hít thở đều, không võng lưng dưới"},{"name":"Chuỗi chào mặt trời (Sun Salutation B)","sets":3,"reps":"5 vòng","durationMinutes":15,"note":"Kết hợp nhịp thở sâu theo chuyển động"}]',
 N'Mỗi sáng thực hiện 10 phút giãn cơ cổ vai gáy và 3 hiệp Plank (45 giây/hiệp) tại nhà.',
 DATEADD(DAY, -7, @today), DATEADD(DAY, 21, @today), SYSUTCDATETIME()),
(2, N'acc-coa-001', N'CLASS', NULL, 1,
 N'Giáo án Chung: Cân bằng Hơi thở & Sức bền Nền tảng',
 N'Đồng bộ kỹ thuật hít thở 4-7-8 và tăng độ bền cơ trung tâm cho cả lớp Yoga',
 N'[{"name":"Khởi động khớp toàn thân & Hít thở cơ hoành","sets":1,"reps":"10 phút","durationMinutes":10,"note":"Thả lỏng vai và cổ"},{"name":"Tư thế Cây Cầu (Glute Bridge) giữ tĩnh","sets":3,"reps":"15 lần","durationMinutes":12,"note":"Siết cơ mông ở điểm cao nhất 2 giây"},{"name":"Giãn cơ phục hồi cuối buổi (Cool-down)","sets":1,"reps":"10 phút","durationMinutes":10,"note":"Giữ mỗi tư thế 30 giây"}]',
 N'Tập hít thở cơ hoành 5 phút trước khi ngủ mỗi tối.',
 DATEADD(DAY, -3, @today), DATEADD(DAY, 25, @today), SYSUTCDATETIME());
SET IDENTITY_INSERT dbo.TrainingPlan OFF;

-- 4. Kết quả tập luyện & Đánh giá tiến độ mẫu
INSERT INTO dbo.TrainingResult (CoachId, MemberId, SessionId, PlanId, RecordedDate, CompletedExercises, CompletionRate, ProgressRating, ResultSummary, CoachFeedback, HomeworkAssigned, CreatedAt)
VALUES
(N'acc-coa-001', N'acc-mem-001', 3, 1, DATEADD(DAY, -1, @today),
 N'Goblet Squat 4x12 (16kg), Plank 3x60s, Chuỗi Sun Salutation 5 vòng',
 95, N'EXCEEDED',
 N'Hoàn thành 95% khối lượng buổi tập, thời gian giữ Plank tăng từ 45s lên 65s.',
 N'Tư thế lưng khi Squat rất chuẩn, nhịp thở ổn định hơn tuần trước. Tuần tới có thể nâng tạ đơn lên 18kg.',
 N'Duy trì giãn cơ gân kheo 10 phút sau khi tập và uống đủ 2.5L nước/ngày.',
 SYSUTCDATETIME());

-- 5. Điểm danh lớp học mẫu cho buổi học đã diễn ra (Session #3 - ngày 2026-10-24)
DELETE FROM dbo.ClassSessionAttendance WHERE SessionId = 3;
INSERT INTO dbo.ClassSessionAttendance (RegistrationId, SessionId, MemberId, Status, Note, RecordedBy, RecordedAt)
SELECT r.Id, r.SessionId, r.MemberId, N'PRESENT', N'Đi học đúng giờ, hoàn thành tốt bài tập.', N'acc-coa-001', DATEADD(DAY, -2, SYSUTCDATETIME())
FROM dbo.ClassRegistration r
WHERE r.SessionId = 3 AND r.Status = N'BOOKED';

-- 6. Lượt điểm danh tại quầy & Yêu cầu hỗ trợ mẫu
INSERT INTO dbo.CenterVisit (Id, MemberId, VisitDate, CheckedInAt, CheckedOutAt, CheckedInBy, CheckedOutBy, Note)
VALUES (N'VIS-DEMO-001', N'acc-mem-001', DATEADD(DAY, -1, @today), DATEADD(HOUR, -26, SYSUTCDATETIME()), DATEADD(HOUR, -24, SYSUTCDATETIME()), N'acc-rec-001', N'acc-rec-001', N'Check-in tập buổi sáng đúng giờ.');

INSERT INTO dbo.SupportRequest (Id, MemberId, Category, Subject, Note, Status, ResolutionNote, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES (N'SUP-DEMO-001', N'acc-mem-001', N'Lớp học', N'Tư vấn đổi ca tập Yoga sang khung giờ tối', N'Hội viên muốn chuyển lịch tập từ Ca sáng sang Ca tối (18:00 - 19:30) các ngày Thứ 3 - Thứ 5.', N'IN_PROGRESS', N'Lễ tân đã kiểm tra chỗ trống lớp Yoga Ca tối và đang chờ hội viên xác nhận ngày bắt đầu.', N'acc-rec-001', N'acc-rec-001', DATEADD(HOUR, -3, SYSUTCDATETIME()), DATEADD(HOUR, -1, SYSUTCDATETIME()));
GO