"""Generates the Titan Arena demo dataset as one re-runnable SQL script.

The live database had accumulated end-to-end test residue ("E2E Sport 205457",
rooms named "xxxxxxxx…", members born in the year 0200). This rebuilds it as a
plausible Vietnamese sports centre: real-looking people, a slot-based timetable,
subscriptions and invoices spread over months so the reports have a shape.

Dates are emitted relative to @today so the dataset stays current whenever the
script is run.
"""
import io
import os
import unicodedata

_REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(_REPO, "Database", "2026-11-06-titan-arena-seed.sql")

# BCrypt hash of "Admin@123"; every seeded account shares it.
PWD = "$2a$11$Pg2II6mLLJckDVq3MotNgei88QWFHN2hXmeDkv4jwSajh.4npLnYG"


def normalize(value: str) -> str:
    """Mirror of Services/ClassScheduling/Flow2Name.Normalize."""
    out, pending = [], False
    for ch in unicodedata.normalize("NFD", value):
        if unicodedata.category(ch) == "Mn":
            continue
        if ch.isspace():
            pending = bool(out)
            continue
        if pending:
            out.append(" ")
        pending = False
        out.append("D" if ch in ("đ", "Đ") else ch.upper())
    return unicodedata.normalize("NFC", "".join(out))


def q(v):
    return "NULL" if v is None else "N'" + str(v).replace("'", "''") + "'"


def a(v):
    return "NULL" if v is None else "'" + str(v).replace("'", "''") + "'"


def jsonl(items):
    return "[" + ",".join('"' + i.replace('"', '\\"') + '"' for i in items) + "]"


L = []
w = L.append

# ===========================================================================
# Header: capture the passwords of accounts people already sign in with, then
# clear every operational table.
# ===========================================================================
w("""-- Titan Arena — demo dataset for the Sports Center Management System.
--
-- Replaces the accumulated end-to-end test rows with a coherent centre: staff,
-- members, a slot-based timetable, subscriptions, invoices, attendance and
-- training records.
--
-- Re-runnable: it clears the operational tables first and keeps the passwords
-- of the accounts that already existed. Every other seeded account signs in
-- with: Admin@123
--
-- Session times only ever use the centre's fixed slots
-- (Services/ClassScheduling/CentreTimeSlots.cs).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID('tempdb..#KeptHash') IS NOT NULL DROP TABLE #KeptHash;
SELECT Email, PasswordHash INTO #KeptHash FROM dbo.Account;

BEGIN TRANSACTION;

DECLARE @today DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE);
DECLARE @now DATETIME2 = SYSUTCDATETIME();
DECLARE @pwd VARCHAR(255) = '""" + PWD + """';

-- Child rows first so the foreign keys stay satisfied throughout.
DELETE FROM dbo.AuditLog;              -- references Class and ClassSession
DELETE FROM dbo.ClassSessionAttendance;
DELETE FROM dbo.TrainingResult;
DELETE FROM dbo.TrainingPlan;
DELETE FROM dbo.MemberTrainingProfile;
DELETE FROM dbo.CenterVisit;
DELETE FROM dbo.SupportRequest;
DELETE FROM dbo.RolePermissionConfig;
DELETE FROM dbo.Notification;          -- references ClassChangeEvent through EventId
DELETE FROM dbo.ClassChangeEvent;
DELETE FROM dbo.ClassRegistration;
DELETE FROM dbo.MembershipInvoice;
DELETE FROM dbo.MemberSubscription;
DELETE FROM dbo.ClassSession;
DELETE FROM dbo.Class;
DELETE FROM dbo.MembershipPackageSport;
DELETE FROM dbo.MembershipPackage;
DELETE FROM dbo.CoachDiscipline;
DELETE FROM dbo.Discipline;
DELETE FROM dbo.Room;
DELETE FROM dbo.AuditLog;
DELETE FROM dbo.PasswordChangeOtp;
DELETE FROM dbo.Member;
DELETE FROM dbo.Coach;
DELETE FROM dbo.Receptionist;
DELETE FROM dbo.CenterManager;
DELETE FROM dbo.Account;
""")

# ===========================================================================
# Disciplines
# ===========================================================================
DISCIPLINES = [
    (1, "Gym & Thể hình", "Khu tạ tự do và máy tập sức mạnh, có huấn luyện viên hướng dẫn kỹ thuật.", 1),
    (2, "Yoga", "Các lớp Hatha, Vinyasa và Yoga phục hồi cho mọi trình độ.", 1),
    (3, "Pilates", "Tập trung vào cơ lõi, tư thế và sự linh hoạt của cột sống.", 1),
    (4, "Boxing", "Kỹ thuật quyền Anh từ cơ bản đến nâng cao, có đối luyện cùng HLV.", 1),
    (5, "Kickfit", "Bài tập đốt mỡ cường độ cao kết hợp đòn tay và đòn chân.", 1),
    (6, "Bơi lội", "Hồ bơi bốn mùa nước ấm, lớp kỹ thuật bơi và bơi tự do.", 1),
    (7, "Zumba & Dance", "Nhảy theo nhạc Latin, phù hợp với hội viên muốn giảm cân vui vẻ.", 1),
    (8, "Cardio HIIT", "Chu kỳ cường độ cao ngắt quãng trên máy chạy, xe đạp và dây trận.", 1),
    (9, "Cầu lông", "Sân tiêu chuẩn thi đấu, lớp kỹ thuật và giao lưu cuối tuần.", 1),
    (10, "Khiêu vũ thể thao", "Lớp dancesport tạm ngừng khai giảng trong quý này.", 0),
]
w("-- 1. Bộ môn ---------------------------------------------------------------")
w("SET IDENTITY_INSERT dbo.Discipline ON;")
for did, name, desc, active in DISCIPLINES:
    # NormalizedName is a computed column in the database, so it is never inserted.
    w(f"INSERT dbo.Discipline (Id, Name, Description, IsActive, CreatedAt)"
      f" VALUES ({did}, {q(name)}, {q(desc)}, {active}, DATEADD(MONTH, -18, @now));")
w("SET IDENTITY_INSERT dbo.Discipline OFF;")
w("")

# ===========================================================================
# Rooms
# ===========================================================================
ROOMS = [
    (1, "Phòng Gym Titan A", "Tầng 1 — Khu A", "GYM", 40, 1),
    (2, "Phòng Gym Titan B", "Tầng 1 — Khu B", "GYM", 30, 1),
    (3, "Phòng Cardio & HIIT", "Tầng 1 — Khu C", "GYM", 28, 1),
    (4, "Studio Yoga Lotus", "Tầng 2 — Khu A", "STUDIO", 24, 1),
    (5, "Studio Pilates Zen", "Tầng 2 — Khu B", "STUDIO", 16, 1),
    (6, "Studio Zumba Rhythm", "Tầng 2 — Khu C", "STUDIO", 30, 1),
    (7, "Sàn Boxing Ring", "Tầng 3 — Khu A", "OTHER", 20, 1),
    (8, "Sàn Kickfit Arena", "Tầng 3 — Khu B", "OTHER", 24, 1),
    (9, "Hồ bơi bốn mùa", "Tầng hầm B1", "POOL", 30, 1),
    (10, "Sân cầu lông số 1", "Tầng 4 — Khu A", "COURT", 16, 1),
    (11, "Sân cầu lông số 2", "Tầng 4 — Khu B", "COURT", 16, 1),
    (12, "Studio Dancesport", "Tầng 2 — Khu D", "STUDIO", 20, 0),
]
w("-- 2. Phòng tập ------------------------------------------------------------")
w("SET IDENTITY_INSERT dbo.Room ON;")
for rid, name, loc, rtype, cap, active in ROOMS:
    w(f"INSERT dbo.Room (Id, Name, Location, RoomType, Capacity, IsActive, CreatedAt)"
      f" VALUES ({rid}, {q(name)}, {q(loc)}, {q(rtype)}, {cap}, {active},"
      f" DATEADD(MONTH, -18, @now));")
w("SET IDENTITY_INSERT dbo.Room OFF;")
w("")

# ===========================================================================
# People
# ===========================================================================
MANAGERS = [
    ("acc-mgr-001", "uchihaminh6969@gmail.com", "Nguyễn Minh Quân", "0901472583", "1988-03-14"),
    ("acc-mgr-002", "manager@sportscenter.com", "Trần Thị Hồng Ngọc", "0901472584", "1990-07-22"),
]
RECEPTIONISTS = [
    ("acc-rec-001", "makitonati@gmail.com", "Lê Thị Thu Hà", "0902583691", "Ca sáng (06:00 - 14:00)"),
    ("acc-rec-002", "truc.pt@titanarena.vn", "Phạm Thanh Trúc", "0902583692", "Ca chiều (14:00 - 22:00)"),
    ("acc-rec-003", "nhu.dq@titanarena.vn", "Đỗ Quỳnh Như", "0902583693", "Ca sáng (06:00 - 14:00)"),
    ("acc-rec-004", "long.vh@titanarena.vn", "Vũ Hoàng Long", "0902583694", "Ca chiều (14:00 - 22:00)"),
]
COACHES = [
    ("acc-coa-001", "thienvonhat@gmail.com", "Võ Nhật Thiên", "0888999444",
     "Gym & Thể hình, Cardio HIIT", "Thứ 2 - Thứ 6: 06:00 - 14:00", [1, 8]),
    ("acc-coa-002", "linh.nt@titanarena.vn", "Nguyễn Thùy Linh", "0903741852",
     "Yoga, Pilates", "Thứ 2 - Thứ 7: 06:00 - 12:00", [2, 3]),
    ("acc-coa-003", "khoa.tm@titanarena.vn", "Trần Minh Khoa", "0903741853",
     "Boxing, Kickfit", "Thứ 2 - Thứ 6: 14:00 - 22:00", [4, 5]),
    ("acc-coa-004", "mai.ptq@titanarena.vn", "Phan Thị Quỳnh Mai", "0903741854",
     "Zumba & Dance, Cardio HIIT", "Thứ 3 - Thứ 7: 16:00 - 21:30", [7, 8]),
    ("acc-coa-005", "duy.lh@titanarena.vn", "Lê Hoàng Duy", "0903741855",
     "Bơi lội", "Thứ 2 - Thứ 7: 06:00 - 14:00", [6]),
    ("acc-coa-006", "an.ht@titanarena.vn", "Hoàng Tuấn An", "0903741856",
     "Gym & Thể hình", "Thứ 2 - Thứ 6: 14:00 - 22:00", [1]),
    ("acc-coa-007", "chi.dnk@titanarena.vn", "Đặng Ngọc Khánh Chi", "0903741857",
     "Yoga", "Thứ 4 - Chủ nhật: 08:00 - 16:00", [2]),
    ("acc-coa-008", "son.bt@titanarena.vn", "Bùi Thanh Sơn", "0903741858",
     "Cầu lông", "Thứ 3 - Chủ nhật: 15:00 - 21:30", [9]),
    ("acc-coa-009", "tram.ntb@titanarena.vn", "Ngô Thị Bảo Trâm", "0903741859",
     "Pilates, Yoga", "Thứ 2 - Thứ 6: 08:00 - 16:00", [3, 2]),
    ("acc-coa-010", "nam.vq@titanarena.vn", "Vương Quốc Nam", "0903741860",
     "Kickfit, Cardio HIIT", "Thứ 2 - Thứ 7: 16:00 - 21:30", [5, 8]),
]
MEMBER_NAMES = [
    "Nguyễn Bình Minh", "Nguyễn Quốc Anh", "Trần Gia Hân", "Lê Văn Thành",
    "Phạm Thị Mai Anh", "Hoàng Đức Trọng", "Vũ Thị Ngọc Diệp", "Đỗ Hoàng Phúc",
    "Bùi Thanh Tâm", "Ngô Minh Tuấn", "Đặng Thu Phương", "Lý Gia Bảo",
    "Trịnh Khánh Vy", "Phan Văn Hậu", "Dương Thị Kim Chi", "Tạ Quang Huy",
    "Võ Thị Thanh Thảo", "Hồ Nhật Trường", "Châu Mỹ Linh", "Lâm Chí Kiệt",
    "Đinh Thị Hồng Nhung", "Mai Xuân Bách", "Tô Ngọc Lan", "Cao Văn Nghĩa",
    "Huỳnh Thị Tuyết Mai", "Nguyễn Hữu Phát", "Trần Thị Bích Ngọc", "Lê Quang Vinh",
    "Phạm Hoàng Nam", "Vũ Đức Mạnh", "Đỗ Thị Hoài Thu", "Bùi Gia Khiêm",
    "Nguyễn Thị Lan Anh", "Trương Minh Hiếu", "Lưu Thị Thuỳ Dương", "Phùng Anh Khoa",
]
EMAIL_SLUGS = [
    "giahan.tran", "thanh.levan", "maianh.pham", "trong.hoang", "ngocdiep.vu",
    "phuc.dohoang", "tam.buithanh", "tuan.ngominh", "phuong.dangthu", "bao.lygia",
    "vy.trinhkhanh", "hau.phanvan", "kimchi.duong", "huy.taquang", "thao.vothi",
    "truong.honhat", "mylinh.chau", "kiet.lamchi", "nhung.dinhthi", "bach.maixuan",
    "lan.tongoc", "nghia.caovan", "tuyetmai.huynh", "phat.nguyenhuu", "bichngoc.tran",
    "vinh.lequang", "nam.phamhoang", "manh.vuduc", "hoaithu.dothi", "khiem.buigia",
    "lananh.nguyen", "hieu.truongminh", "thuyduong.luu", "khoa.phunganh",
]
FIXED_MEMBER_EMAILS = {0: "minhnakino@gmail.com", 1: "nguyenquocanh211004@gmail.com"}

members = []
for i, name in enumerate(MEMBER_NAMES):
    members.append({
        "id": f"acc-mem-{i + 1:03d}",
        "email": FIXED_MEMBER_EMAILS.get(i) or f"{EMAIL_SLUGS[i - 2]}@gmail.com",
        "name": name,
        "phone": f"09{11000000 + i * 137:08d}",
        "dob": f"{1986 + (i * 7) % 20}-{1 + (i * 5) % 12:02d}-{1 + (i * 11) % 28:02d}",
        "code": f"TA{2600 + i:04d}",
        "joined": 1 + (i * 3) % 14,
        "active": 0 if i in (30, 33) else 1,
    })

ACC = ("INSERT dbo.Account (Id, Email, PasswordHash, Status, FailedLoginCount, IsLocked,"
       " CreatedAt, Phone, RoleId) VALUES ({id}, {email},"
       " ISNULL((SELECT TOP 1 PasswordHash FROM #KeptHash WHERE Email = {email}), @pwd),"
       " '{status}', 0, 0, DATEADD(MONTH, -{ago}, @now), {phone}, {role});")

w("-- 3. Tài khoản & nhân sự ---------------------------------------------------")
for acc_id, email, name, phone, dob in MANAGERS:
    w(ACC.format(id=a(acc_id), email=a(email), status="Active", ago=18, phone=a(phone), role=1))
    w(f"INSERT dbo.CenterManager (AccountId, FullName, CreatedAt, DateOfBirth)"
      f" VALUES ({a(acc_id)}, {q(name)}, DATEADD(MONTH, -18, @now), '{dob}');")
w("")
for acc_id, email, name, phone, shift in RECEPTIONISTS:
    w(ACC.format(id=a(acc_id), email=a(email), status="Active", ago=14, phone=a(phone), role=4))
    w(f"INSERT dbo.Receptionist (AccountId, FullName, WorkShift, CreatedAt)"
      f" VALUES ({a(acc_id)}, {q(name)}, {q(shift)}, DATEADD(MONTH, -14, @now));")
w("")
for acc_id, email, name, phone, spec, sched, _ in COACHES:
    w(ACC.format(id=a(acc_id), email=a(email), status="Active", ago=12, phone=a(phone), role=2))
    w(f"INSERT dbo.Coach (AccountId, FullName, Specialization, WorkSchedule, CreatedAt)"
      f" VALUES ({a(acc_id)}, {q(name)}, {q(spec)}, {q(sched)}, DATEADD(MONTH, -12, @now));")
for acc_id, *_rest in COACHES:
    for did in _rest[-1]:
        w(f"INSERT dbo.CoachDiscipline (CoachAccountId, DisciplineId, CreatedAt)"
          f" VALUES ({a(acc_id)}, {did}, DATEADD(MONTH, -12, @now));")
w("")
w("-- 4. Hội viên -------------------------------------------------------------")
for m in members:
    w(ACC.format(id=a(m["id"]), email=a(m["email"]),
                 status="Active" if m["active"] else "Inactive",
                 ago=m["joined"], phone=a(m["phone"]), role=3))
    w(f"INSERT dbo.Member (AccountId, MemberCode, FullName, DateOfBirth, CreatedAt)"
      f" VALUES ({a(m['id'])}, {a(m['code'])}, {q(m['name'])}, '{m['dob']}',"
      f" DATEADD(MONTH, -{m['joined']}, @now));")
w("")

# ===========================================================================
# Packages + which disciplines each one unlocks
# ===========================================================================
PACKAGES = [
    (1, "Gói Trải Nghiệm - 1 Tháng", 350000, 1, 1, [
        "Sử dụng toàn bộ trang thiết bị Gym và khu Cardio",
        "Tủ đồ cá nhân thông minh trong giờ tập",
        "Khu vực tắm nóng lạnh và phòng thay đồ",
    ], [1, 8]),
    (2, "Gói Yoga & Fitness - 1 Tháng", 450000, 1, 1, [
        "Toàn quyền tập luyện Gym và các lớp Yoga",
        "Tham gia tối đa 8 lớp Yoga hoặc Pilates mỗi tháng",
        "Thảm và dụng cụ hỗ trợ do trung tâm cung cấp",
        "Tư vấn dinh dưỡng cơ bản 1 lần mỗi tháng",
    ], [1, 2, 3]),
    (3, "Gói Năng Động - 3 Tháng", 950000, 3, 1, [
        "Tập luyện không giới hạn mọi khung giờ",
        "Đo chỉ số cơ thể InBody định kỳ hàng tháng",
        "1 buổi định hướng tập luyện cùng huấn luyện viên",
        "Tham gia các lớp Zumba, Cardio HIIT và Cầu lông",
    ], [1, 2, 7, 8, 9]),
    (4, "Gói Bơi Lội & Gym - 3 Tháng", 1250000, 3, 1, [
        "Sử dụng hồ bơi bốn mùa nước ấm và khu Gym",
        "2 buổi kiểm tra thể lực chuyên sâu",
        "Tủ gửi đồ cá nhân cao cấp",
        "Giảm 10% dịch vụ xông hơi và massage thể thao",
    ], [1, 6, 8]),
    (5, "Gói Boxing Chuyên Sâu - 3 Tháng", 1500000, 3, 1, [
        "Tập luyện Boxing và Kickfit cùng HLV chuyên nghiệp",
        "Trang bị găng tay và bảo hộ trong suốt khóa học",
        "2 buổi đối luyện có ghi hình phân tích kỹ thuật",
    ], [4, 5]),
    (6, "Gói Hội Viên Vàng - 12 Tháng", 3200000, 12, 1, [
        "Tập luyện không giới hạn toàn hệ thống trong 1 năm",
        "Tặng 3 buổi tập riêng 1 kèm 1 cùng huấn luyện viên",
        "Đo InBody và tư vấn dinh dưỡng hàng tháng",
        "Ưu tiên giữ chỗ các lớp học đông hội viên",
        "Miễn phí 2 lần bảo lưu gói tập trong năm",
    ], []),
    (7, "Gói VIP Kim Cương - 12 Tháng", 4800000, 12, 1, [
        "Toàn quyền Gym, Hồ bơi, Xông hơi, Yoga và Boxing",
        "Tặng 6 buổi huấn luyện viên cá nhân (PT)",
        "Tủ đồ VIP cố định riêng cả năm",
        "Khăn tập và nước uống thể thao miễn phí mỗi buổi",
        "Dẫn theo 1 khách mời miễn phí 2 lần mỗi tháng",
        "Hỗ trợ đặt lịch ưu tiên qua tổng đài riêng",
    ], []),
    (8, "Gói Dancesport - 1 Tháng", 500000, 1, 0, [
        "Lớp khiêu vũ thể thao cơ bản theo cặp",
        "Tạm ngừng mở bán trong quý này",
    ], [10]),
]
w("-- 5. Gói thành viên -------------------------------------------------------")
w("SET IDENTITY_INSERT dbo.MembershipPackage ON;")
for pid, name, price, months, active, benefits, _ in PACKAGES:
    w(f"INSERT dbo.MembershipPackage (Id, Name, Price, DurationMonths, Benefits, IsActive, CreatedAt)"
      f" VALUES ({pid}, {q(name)}, {price}, {months}, {q(jsonl(benefits))}, {active},"
      f" DATEADD(MONTH, -18, @now));")
w("SET IDENTITY_INSERT dbo.MembershipPackage OFF;")
for pid, _n, _p, _m, _a, _b, sports in PACKAGES:
    for did in sports:
        w(f"INSERT dbo.MembershipPackageSport (PackageId, DisciplineId, CreatedAt)"
          f" VALUES ({pid}, {did}, DATEADD(MONTH, -18, @now));")
w("")

# ===========================================================================
# Classes — one per discipline focus, each taught by a qualified coach
# ===========================================================================
SLOTS = {
    1: ("06:30:00", "07:30:00"),
    2: ("08:00:00", "09:00:00"),
    3: ("09:00:00", "10:00:00"),
    4: ("14:00:00", "15:30:00"),
    5: ("16:00:00", "17:00:00"),
    6: ("18:00:00", "19:30:00"),
    7: ("19:30:00", "21:00:00"),
}
# (id, name, discipline, capacity, status, description, coach, room, slot, weekdays)
# weekday: 0 = Monday … 6 = Sunday
CLASSES = [
    (1, "Gym Nền Tảng Buổi Sáng", 1, 20, "OPEN",
     "Lớp hướng dẫn kỹ thuật tạ cơ bản, phù hợp hội viên mới bắt đầu.",
     "acc-coa-001", 1, 2, [0, 2, 4]),
    (2, "Gym Tăng Cơ Nâng Cao", 1, 16, "OPEN",
     "Giáo án tăng cơ theo nhóm cơ, dành cho hội viên đã tập trên 6 tháng.",
     "acc-coa-006", 2, 6, [1, 3]),
    (3, "Yoga Buổi Sáng An Yên", 2, 20, "OPEN",
     "Chuỗi Hatha nhẹ nhàng giúp khởi động cơ thể và điều hòa hơi thở.",
     "acc-coa-002", 4, 1, [0, 2, 4]),
    (4, "Yoga Vinyasa Buổi Tối", 2, 20, "OPEN",
     "Chuỗi động tác liên hoàn theo nhịp thở, tăng sức bền và độ dẻo.",
     "acc-coa-007", 4, 6, [1, 3]),
    (5, "Pilates Cơ Lõi", 3, 14, "OPEN",
     "Bài tập thảm tập trung vào cơ lõi, cải thiện tư thế và giảm đau lưng.",
     "acc-coa-009", 5, 3, [1, 3]),
    (6, "Boxing Căn Bản", 4, 16, "OPEN",
     "Học tấn, di chuyển và các đòn tay cơ bản của quyền Anh.",
     "acc-coa-003", 7, 5, [0, 2]),
    (7, "Kickfit Đốt Mỡ", 5, 20, "OPEN",
     "Lớp cường độ cao kết hợp đòn tay, đòn chân và bài tập thể lực.",
     "acc-coa-010", 8, 6, [0, 2, 4]),
    (8, "Bơi Kỹ Thuật Cơ Bản", 6, 12, "OPEN",
     "Lớp dạy bơi ếch và bơi sải cho người mới, tối đa 12 hội viên mỗi buổi.",
     "acc-coa-005", 9, 2, [1, 3, 5]),
    (9, "Zumba Buổi Tối", 7, 25, "OPEN",
     "Nhảy theo nhạc Latin sôi động, đốt khoảng 500 kcal mỗi buổi.",
     "acc-coa-004", 6, 7, [1, 3, 5]),
    (10, "Cardio HIIT 45 Phút", 8, 18, "OPEN",
     "Chu kỳ cường độ cao ngắt quãng, phù hợp hội viên muốn giảm mỡ nhanh.",
     "acc-coa-001", 3, 5, [1, 3]),
    (11, "Cầu Lông Giao Lưu", 9, 12, "OPEN",
     "Lớp kỹ thuật kết hợp đánh đôi giao lưu vào cuối tuần.",
     "acc-coa-008", 10, 7, [5, 6]),
    (12, "Yoga Phục Hồi Cuối Tuần", 2, 18, "OPEN",
     "Lớp yoga trị liệu nhẹ nhàng dành cho hội viên văn phòng.",
     "acc-coa-007", 4, 4, [6]),
    (13, "Gym Cường Độ Cao Mùa Hè", 1, 16, "ARCHIVED",
     "Khóa học theo mùa đã kết thúc, giữ lại để tra cứu lịch sử.",
     "acc-coa-006", 2, 4, []),
    (14, "Pilates Thiết Bị Reformer", 3, 10, "DRAFT",
     "Lớp đang chuẩn bị khai giảng, chờ nhập thiết bị Reformer.",
     "acc-coa-009", 5, 4, []),
]
w("-- 6. Lớp học -------------------------------------------------------------")
w("SET IDENTITY_INSERT dbo.Class ON;")
for cid, name, did, cap, status, desc, _c, _r, _s, _d in CLASSES:
    w(f"INSERT dbo.Class (Id, DisciplineId, Name, Description, Capacity, Status, CreatedAt,"
      f" CreatedBy) VALUES ({cid}, {did}, {q(name)}, {q(desc)}, {cap},"
      f" {a(status)}, DATEADD(MONTH, -6, @now), 'acc-mgr-001');")
w("SET IDENTITY_INSERT dbo.Class OFF;")
w("")

# ===========================================================================
# Sessions — 5 weeks behind and 4 weeks ahead of today, on the fixed slots
# ===========================================================================
WEEKS_BACK, WEEKS_AHEAD = 5, 4
sessions = []           # (id, class, coach, room, offset_expr, slot, capacity, status)
session_id = 0
# A couple of cancelled sessions so the cancellation screens have real data.
CANCELLED = {(7, 1), (9, 2)}   # (class id, week index) relative to the future weeks

for cid, name, did, cap, status, desc, coach, room, slot, weekdays in CLASSES:
    if status != "OPEN":
        continue
    session_cap = min(cap, dict((r[0], r[4]) for r in ROOMS)[room])
    for week in range(-WEEKS_BACK, WEEKS_AHEAD + 1):
        for wd in weekdays:
            session_id += 1
            # Offset from "the Monday of this week" so the grid lines up.
            offset = week * 7 + wd
            if week < 0:
                st = "COMPLETED"
            elif week == 0:
                st = "SCHEDULED"
            else:
                st = "CANCELED" if (cid, week) in CANCELLED else "SCHEDULED"
            sessions.append({
                "id": session_id, "class": cid, "coach": coach, "room": room,
                "offset": offset, "slot": slot, "cap": session_cap, "status": st,
                "discipline": did,
            })

w("-- 7. Buổi học (5 tuần trước và 4 tuần tới, theo khung giờ cố định) ---------")
w("DECLARE @monday DATE = DATEADD(DAY, -(DATEDIFF(DAY, '2000-01-03', @today) % 7), @today);")
w("SET IDENTITY_INSERT dbo.ClassSession ON;")
for s in sessions:
    start, end = SLOTS[s["slot"]]
    cancel_cols = ""
    cancel_vals = ""
    if s["status"] == "CANCELED":
        cancel_cols = ", CancelReason, CanceledAt, CanceledBy"
        cancel_vals = (", N'Huấn luyện viên bận lịch thi đấu, trung tâm xin lỗi hội viên.',"
                       " DATEADD(DAY, -3, @now), 'acc-mgr-001'")
    w(f"INSERT dbo.ClassSession (Id, ClassId, CoachId, RoomId, SessionDate, StartTime, EndTime,"
      f" Capacity, BookedCount, Status, CreatedAt, CreatedBy{cancel_cols})"
      f" VALUES ({s['id']}, {s['class']}, {a(s['coach'])}, {s['room']},"
      f" DATEADD(DAY, {s['offset']}, @monday), '{start}', '{end}', {s['cap']}, 0,"
      f" {a(s['status'])}, DATEADD(MONTH, -2, @now), 'acc-mgr-001'{cancel_vals});")
w("SET IDENTITY_INSERT dbo.ClassSession OFF;")
w("")

# ===========================================================================
# Subscriptions + invoices (one invoice per subscription — unique constraint)
# ===========================================================================
PKG_BY_ID = {p[0]: p for p in PACKAGES}
# Spread members over the sellable packages so the revenue mix looks organic.
PLAN = [1, 3, 6, 2, 4, 3, 7, 1, 5, 6, 3, 2, 4, 6, 1, 3, 7, 5, 2, 6,
        3, 1, 4, 6, 2, 3, 5, 7, 1, 6, 3, 2, 4, 6, 1, 3]
METHODS = ["CASH", "BANK_TRANSFER", "CARD"]

# Members whose current package is about to lapse, so the "sắp hết hạn" tile on
# the counter screens has something to show.
EXPIRING_SOON = {4, 22}
# Members who have not paid yet; cash so the cash desk has a queue.
AWAITING_PAYMENT = {7, 19}

subs, invoices = [], []
sub_id = 0
for i, m in enumerate(members):
    pkg = PKG_BY_ID[PLAN[i]]
    duration = pkg[3]
    history = 2 if i % 5 == 0 else 1      # every fifth member has renewed once

    # The current term starts inside the package window so an active member is
    # genuinely in-date today; a lapsed member's term ended two months ago.
    if m["active"]:
        current_months, current_days = i % duration, 0
        if i in EXPIRING_SOON:
            # Ends in three days' time.
            current_months, current_days = duration, 4
    else:
        current_months, current_days = duration + 2, 0

    for h in range(history):
        sub_id += 1
        # The oldest term is the original registration, later ones are renewals.
        back = history - 1 - h
        months = current_months + back * duration
        kind = "REGISTER" if h == 0 else "RENEW"
        is_current = h == history - 1

        status, inv_status = "CONFIRMED", "PAID"
        method = METHODS[(i + h) % 3]
        if not m["active"] and is_current:
            status, inv_status = "CANCELED", "CANCELED"
        elif i in AWAITING_PAYMENT and is_current:
            status, inv_status, method = "PENDING_PAYMENT", "PENDING_PAYMENT", "CASH"

        subs.append({
            "id": sub_id, "member": m["id"], "pkg": pkg, "kind": kind,
            "months": months, "days": current_days if is_current else 0,
            "status": status,
        })
        invoices.append({
            "id": sub_id, "sub": sub_id, "member": m["id"], "amount": pkg[2],
            "status": inv_status, "method": method, "months": months,
        })

w("-- 8. Đăng ký gói & hóa đơn ------------------------------------------------")
w("SET IDENTITY_INSERT dbo.MemberSubscription ON;")
for s in subs:
    pkg = s["pkg"]
    start = f"DATEADD(DAY, {s['days']}, DATEADD(MONTH, -{s['months']}, @today))"
    end = f"DATEADD(DAY, -1, DATEADD(MONTH, {pkg[3]}, {start}))"
    w(f"INSERT dbo.MemberSubscription (Id, MemberId, PackageId, PackageName, PackagePrice,"
      f" DurationMonths, Benefits, StartDate, EndDate, Kind, Status, CreatedAt, IsSuspended)"
      f" VALUES ({s['id']}, {a(s['member'])}, {pkg[0]}, {q(pkg[1])}, {pkg[2]}, {pkg[3]},"
      f" {q(jsonl(pkg[5]))}, {start}, {end}, {a(s['kind'])},"
      f" {a(s['status'])}, DATEADD(MONTH, -{s['months']}, @now), 0);")
w("SET IDENTITY_INSERT dbo.MemberSubscription OFF;")
w("")
w("SET IDENTITY_INSERT dbo.MembershipInvoice ON;")
for inv in invoices:
    paid = ("DATEADD(MONTH, -%d, @now), 'acc-rec-001'" % inv["months"]
            if inv["status"] == "PAID" else "NULL, NULL")
    number = f"INV-TA{inv['id']:05d}"
    w(f"INSERT dbo.MembershipInvoice (Id, InvoiceNumber, SubscriptionId, MemberId, Amount,"
      f" Status, PaymentMethod, CreatedAt, CreatedBy, PaidAt, PaidBy)"
      f" VALUES ({inv['id']}, {a(number)}, {inv['sub']}, {a(inv['member'])}, {inv['amount']},"
      f" {a(inv['status'])}, {a(inv['method'])}, DATEADD(MONTH, -{inv['months']}, @now),"
      f" 'acc-rec-001', {paid});")
w("SET IDENTITY_INSERT dbo.MembershipInvoice OFF;")
w("")

# ===========================================================================
# Class registrations — only members whose package unlocks the discipline
# ===========================================================================
pkg_sports = {p[0]: set(p[6]) for p in PACKAGES}
member_pkg = {members[i]["id"]: PLAN[i] for i in range(len(members))}
active_sub = {}
for s in subs:
    if s["status"] == "CONFIRMED":
        active_sub[s["member"]] = s["id"]


def may_join(member_id, discipline_id):
    pid = member_pkg[member_id]
    allowed = pkg_sports[pid]
    return not allowed or discipline_id in allowed


registrations = []
reg_id = 0
booked_per_session = {}
for idx, s in enumerate(sessions):
    if s["status"] == "CANCELED":
        continue
    eligible = [m for m in members
                if m["active"] and m["id"] in active_sub and may_join(m["id"], s["discipline"])]
    if not eligible:
        continue
    # A deterministic, uneven fill: popular evening slots run fuller.
    target = max(2, min(len(eligible), s["cap"] * (6 if s["slot"] >= 6 else 4) // 10))
    picked = [eligible[(idx * 7 + k * 3) % len(eligible)] for k in range(target)]
    seen = set()
    chosen = []
    for m in picked:
        if m["id"] not in seen:
            seen.add(m["id"])
            chosen.append(m)
    for m in chosen:
        reg_id += 1
        cancelled = (reg_id % 23 == 0) and s["status"] == "SCHEDULED"
        registrations.append({
            "id": reg_id, "session": s["id"], "member": m["id"],
            "sub": active_sub[m["id"]], "status": "CANCELED" if cancelled else "BOOKED",
            "via": "RECEPTIONIST" if reg_id % 7 == 0 else "MEMBER",
            "session_status": s["status"], "offset": s["offset"],
        })
        if not cancelled:
            booked_per_session[s["id"]] = booked_per_session.get(s["id"], 0) + 1

w("-- 9. Đăng ký lớp học -----------------------------------------------------")
w("SET IDENTITY_INSERT dbo.ClassRegistration ON;")
for r in registrations:
    by = "acc-rec-002" if r["via"] == "RECEPTIONIST" else r["member"]
    extra_cols, extra_vals = "", ""
    if r["status"] == "CANCELED":
        extra_cols = ", CanceledAt, CanceledBy, CancelReason"
        extra_vals = (", DATEADD(DAY, -2, @now), " + a(r["member"]) +
                      ", N'Hội viên bận việc cá nhân nên hủy buổi tập.'")
    w(f"INSERT dbo.ClassRegistration (Id, SessionId, MemberId, Status, RegisteredAt,"
      f" RegisteredVia, RegisteredBy, MemberSubscriptionId{extra_cols})"
      f" VALUES ({r['id']}, {r['session']}, {a(r['member'])}, {a(r['status'])},"
      f" DATEADD(DAY, {r['offset'] - 3}, CAST(@monday AS DATETIME2)), {a(r['via'])},"
      f" {a(by)}, {r['sub']}{extra_vals});")
w("SET IDENTITY_INSERT dbo.ClassRegistration OFF;")
w("UPDATE s SET BookedCount = ISNULL(b.Total, 0)")
w("FROM dbo.ClassSession s")
w("LEFT JOIN (SELECT SessionId, COUNT(*) AS Total FROM dbo.ClassRegistration"
  " WHERE Status = 'BOOKED' GROUP BY SessionId) b ON b.SessionId = s.Id;")
w("")

# ===========================================================================
# Attendance for sessions that already happened
# ===========================================================================
w("-- 10. Điểm danh lớp đã diễn ra --------------------------------------------")
w("""INSERT dbo.ClassSessionAttendance (RegistrationId, SessionId, MemberId, Status, Note, RecordedBy, RecordedAt)
SELECT r.Id, r.SessionId, r.MemberId,
       CASE WHEN r.Id % 11 = 0 THEN 'ABSENT' WHEN r.Id % 7 = 0 THEN 'LATE' ELSE 'PRESENT' END,
       CASE WHEN r.Id % 11 = 0 THEN N'Hội viên báo vắng trước buổi tập.'
            WHEN r.Id % 7 = 0 THEN N'Hội viên đến muộn khoảng 10 phút.'
            ELSE N'Tham gia đầy đủ, hoàn thành bài tập.' END,
       s.CoachId,
       CAST(s.SessionDate AS DATETIME2)
FROM dbo.ClassRegistration r
JOIN dbo.ClassSession s ON s.Id = r.SessionId
WHERE s.Status = 'COMPLETED' AND r.Status = 'BOOKED';
""")

# ===========================================================================
# Training profiles, plans, results
# ===========================================================================
w("-- 11. Hồ sơ tập luyện ------------------------------------------------------")
w("""INSERT dbo.MemberTrainingProfile (MemberId, FitnessGoal, FitnessLevel, CurrentWeightKg, TargetWeightKg, Notes, UpdatedAt)
SELECT m.AccountId,
       CASE ABS(CHECKSUM(m.MemberCode)) % 4
            WHEN 0 THEN N'Giảm 4kg mỡ thừa và giữ vóc dáng cân đối trong 3 tháng'
            WHEN 1 THEN N'Tăng cơ vai, lưng và cải thiện sức mạnh tổng thể'
            WHEN 2 THEN N'Cải thiện độ dẻo dai cột sống và kiểm soát nhịp thở'
            ELSE N'Nâng cao sức bền tim mạch, chạy liên tục 5km không nghỉ' END,
       CASE ABS(CHECKSUM(m.MemberCode)) % 3
            WHEN 0 THEN 'BEGINNER' WHEN 1 THEN 'INTERMEDIATE' ELSE 'ADVANCED' END,
       55.0 + (ABS(CHECKSUM(m.MemberCode)) % 25),
       52.0 + (ABS(CHECKSUM(m.MemberCode)) % 20),
       CASE ABS(CHECKSUM(m.MemberCode)) % 3
            WHEN 0 THEN N'Cần khởi động kỹ khớp gối trước mỗi buổi tập.'
            WHEN 1 THEN N'Ưu tiên bài tập cơ lõi và giãn cơ cuối buổi.'
            ELSE N'Có tiền sử đau vai phải, tránh đẩy tạ quá nặng.' END,
       @now
FROM dbo.Member m;
""")

w("""SET IDENTITY_INSERT dbo.TrainingPlan ON;
INSERT dbo.TrainingPlan (Id, CoachId, PlanType, MemberId, ClassId, Title, Goal, ExercisesJson, Homework, StartDate, EndDate, CreatedAt)
VALUES
(1, 'acc-coa-001', 'INDIVIDUAL', 'acc-mem-001', NULL,
 N'Lộ trình 4 tuần tăng cơ lõi và sức bền',
 N'Giảm 2kg mỡ thừa, giữ Plank 90 giây và hoàn thiện kỹ thuật Squat',
 N'[{"name":"Goblet Squat với tạ đơn","sets":4,"reps":"12 lần","durationMinutes":12,"note":"Giữ thẳng lưng, hạ sâu hết biên độ"},{"name":"Plank siết cơ lõi","sets":3,"reps":"60 giây","durationMinutes":8,"note":"Hít thở đều, không võng lưng dưới"},{"name":"Chèo thuyền với dây kháng lực","sets":3,"reps":"15 lần","durationMinutes":10,"note":"Siết bả vai ở cuối động tác"}]',
 N'Mỗi sáng giãn cơ cổ vai gáy 10 phút và thực hiện 3 hiệp Plank 45 giây tại nhà.',
 DATEADD(DAY, -7, @today), DATEADD(DAY, 21, @today), DATEADD(DAY, -7, @now)),
(2, 'acc-coa-001', 'CLASS', NULL, 1,
 N'Giáo án chung: nền tảng kỹ thuật tạ cho hội viên mới',
 N'Chuẩn hóa kỹ thuật Squat, Deadlift và Bench Press cho cả lớp',
 N'[{"name":"Khởi động khớp toàn thân","sets":1,"reps":"10 phút","durationMinutes":10,"note":"Xoay khớp vai, hông, gối"},{"name":"Squat với thanh đòn không tạ","sets":3,"reps":"12 lần","durationMinutes":15,"note":"Tập trung vào biên độ và nhịp thở"},{"name":"Giãn cơ phục hồi cuối buổi","sets":1,"reps":"10 phút","durationMinutes":10,"note":"Giữ mỗi tư thế 30 giây"}]',
 N'Ghi nhật ký tập luyện sau mỗi buổi và nộp lại vào cuối tuần.',
 DATEADD(DAY, -14, @today), DATEADD(DAY, 14, @today), DATEADD(DAY, -14, @now)),
(3, 'acc-coa-002', 'INDIVIDUAL', 'acc-mem-003', NULL,
 N'Lộ trình 6 tuần Yoga cải thiện cột sống',
 N'Giảm đau lưng dưới và giữ được tư thế Chiến binh III trong 30 giây',
 N'[{"name":"Chuỗi chào mặt trời A","sets":3,"reps":"5 vòng","durationMinutes":15,"note":"Kết hợp nhịp thở sâu theo chuyển động"},{"name":"Tư thế Cây Cầu","sets":3,"reps":"15 lần","durationMinutes":12,"note":"Siết cơ mông ở điểm cao nhất 2 giây"},{"name":"Tư thế Em Bé thư giãn","sets":1,"reps":"5 phút","durationMinutes":5,"note":"Thở bằng cơ hoành"}]',
 N'Tập hít thở cơ hoành 5 phút trước khi ngủ mỗi tối.',
 DATEADD(DAY, -21, @today), DATEADD(DAY, 21, @today), DATEADD(DAY, -21, @now)),
(4, 'acc-coa-003', 'CLASS', NULL, 6,
 N'Giáo án Boxing căn bản tháng này',
 N'Thuần thục bộ tấn, jab, cross và di chuyển vòng tròn',
 N'[{"name":"Nhảy dây khởi động","sets":3,"reps":"3 phút","durationMinutes":9,"note":"Giữ nhịp đều, tiếp đất bằng nửa bàn chân"},{"name":"Đấm bao cát tổ hợp jab-cross","sets":5,"reps":"2 phút","durationMinutes":15,"note":"Xoay hông theo đòn, thu tay về bảo vệ mặt"},{"name":"Bài tập bụng và cổ","sets":3,"reps":"20 lần","durationMinutes":10,"note":"Siết bụng khi thở ra"}]',
 N'Tập shadow boxing 10 phút mỗi ngày trước gương.',
 DATEADD(DAY, -10, @today), DATEADD(DAY, 20, @today), DATEADD(DAY, -10, @now));
SET IDENTITY_INSERT dbo.TrainingPlan OFF;
""")

w("""INSERT dbo.TrainingResult (CoachId, MemberId, SessionId, PlanId, RecordedDate, CompletedExercises, CompletionRate, ProgressRating, ResultSummary, CoachFeedback, HomeworkAssigned, CreatedAt)
SELECT TOP 12
       s.CoachId, r.MemberId, s.Id,
       CASE WHEN s.ClassId = 1 THEN 2 WHEN s.ClassId = 6 THEN 4 ELSE NULL END,
       s.SessionDate,
       N'Hoàn thành phần khởi động, bài chính và giãn cơ cuối buổi',
       80 + (r.Id % 20),
       CASE r.Id % 3 WHEN 0 THEN 'EXCEEDED' WHEN 1 THEN 'ON_TRACK' ELSE 'BEHIND' END,
       N'Hội viên theo kịp giáo án, thể lực cải thiện rõ so với tuần trước.',
       N'Kỹ thuật đã chắc hơn, cần chú ý nhịp thở ở hiệp cuối. Tuần tới tăng nhẹ khối lượng.',
       N'Duy trì giãn cơ 10 phút sau buổi tập và uống đủ 2 lít nước mỗi ngày.',
       @now
FROM dbo.ClassRegistration r
JOIN dbo.ClassSession s ON s.Id = r.SessionId
WHERE s.Status = 'COMPLETED' AND r.Status = 'BOOKED' AND s.ClassId IN (1, 6)
ORDER BY s.SessionDate DESC, r.Id;
""")

# ===========================================================================
# Counter operations: visits + support requests
# ===========================================================================
w("-- 12. Hoạt động tại quầy ---------------------------------------------------")
w("""INSERT dbo.CenterVisit (Id, MemberId, VisitDate, CheckedInAt, CheckedOutAt, CheckedInBy, CheckedOutBy, Note)
SELECT TOP 24
       'VIS-' + m.MemberCode + '-' + CONVERT(VARCHAR(8), DATEADD(DAY, -(ABS(CHECKSUM(m.MemberCode)) % 10), @today), 112),
       m.AccountId,
       DATEADD(DAY, -(ABS(CHECKSUM(m.MemberCode)) % 10), @today),
       DATEADD(HOUR, -(ABS(CHECKSUM(m.MemberCode)) % 200) - 2, @now),
       DATEADD(HOUR, -(ABS(CHECKSUM(m.MemberCode)) % 200), @now),
       'acc-rec-001', 'acc-rec-001',
       N'Hội viên quét mã tại quầy, sử dụng khu Gym và phòng tắm.'
FROM dbo.Member m
JOIN dbo.Account acc ON acc.Id = m.AccountId AND acc.Status = 'Active'
ORDER BY m.MemberCode;

INSERT dbo.SupportRequest (Id, MemberId, Category, Subject, Note, Status, ResolutionNote, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
('SUP-TA0001', 'acc-mem-001', N'Lớp học', N'Xin đổi ca tập Yoga sang khung giờ tối',
 N'Hội viên muốn chuyển lịch tập từ ca sáng sang ca tối 18:00 - 19:30 các ngày thứ 3 và thứ 5.',
 'IN_PROGRESS', N'Lễ tân đã kiểm tra chỗ trống lớp Yoga ca tối, đang chờ hội viên xác nhận ngày bắt đầu.',
 'acc-rec-001', 'acc-rec-001', DATEADD(DAY, -2, @now), DATEADD(HOUR, -5, @now)),
('SUP-TA0002', 'acc-mem-005', N'Thiết bị', N'Máy chạy bộ số 4 phát ra tiếng kêu lạ',
 N'Hội viên phản ánh máy chạy bộ số 4 khu Cardio phát ra tiếng kêu khi chạy trên 8 km/h.',
 'RESOLVED', N'Kỹ thuật đã tra dầu và căn lại băng tải, máy hoạt động bình thường.',
 'acc-rec-002', 'acc-rec-002', DATEADD(DAY, -6, @now), DATEADD(DAY, -5, @now)),
('SUP-TA0003', 'acc-mem-012', N'Thanh toán', N'Cần xuất lại biên nhận gói 3 tháng',
 N'Hội viên làm mất biên nhận giấy và cần bản in lại để thanh toán với công ty.',
 'OPEN', NULL, 'acc-rec-003', NULL, DATEADD(HOUR, -3, @now), DATEADD(HOUR, -3, @now)),
('SUP-TA0004', 'acc-mem-020', N'Gói tập', N'Hỏi về thủ tục bảo lưu gói tập khi đi công tác',
 N'Hội viên sẽ công tác nước ngoài 6 tuần và muốn bảo lưu gói Hội Viên Vàng.',
 'IN_PROGRESS', N'Đã hướng dẫn hội viên gửi đơn bảo lưu kèm lịch công tác trước ngày đi 3 ngày.',
 'acc-rec-001', 'acc-rec-004', DATEADD(DAY, -1, @now), DATEADD(HOUR, -8, @now));
""")

# ===========================================================================
# Notifications + audit trail
# ===========================================================================
w("-- 13. Thông báo & nhật ký thao tác -----------------------------------------")
w("""INSERT dbo.Notification (RecipientId, Type, Title, Body, EntityType, EntityId, EventAt, IsRead, CreatedAt, ReadAt)
SELECT r.MemberId, 'CLASS_REGISTERED',
       N'Đăng ký lớp thành công: ' + c.Name,
       N'Bạn đã đăng ký buổi học ngày ' + FORMAT(s.SessionDate, 'dd/MM/yyyy')
           + N' lúc ' + LEFT(CONVERT(VARCHAR(8), s.StartTime), 5) + N' tại ' + ro.Name + N'.',
       'ClassSession', CAST(s.Id AS VARCHAR(100)),
       CAST(s.SessionDate AS DATETIME2), CASE WHEN r.Id % 3 = 0 THEN 0 ELSE 1 END,
       DATEADD(DAY, -2, @now),
       CASE WHEN r.Id % 3 = 0 THEN NULL ELSE DATEADD(DAY, -1, @now) END
FROM dbo.ClassRegistration r
JOIN dbo.ClassSession s ON s.Id = r.SessionId
JOIN dbo.Class c ON c.Id = s.ClassId
JOIN dbo.Room ro ON ro.Id = s.RoomId
WHERE s.Status = 'SCHEDULED' AND r.Status = 'BOOKED';

INSERT dbo.Notification (RecipientId, Type, Title, Body, EntityType, EntityId, EventAt, IsRead, CreatedAt)
SELECT r.MemberId, 'SESSION_CANCELED',
       N'Buổi học đã bị hủy: ' + c.Name,
       N'Buổi học ngày ' + FORMAT(s.SessionDate, 'dd/MM/yyyy')
           + N' đã bị hủy. Lý do: ' + ISNULL(s.CancelReason, N'Trung tâm điều chỉnh lịch.'),
       'ClassSession', CAST(s.Id AS VARCHAR(100)),
       CAST(s.SessionDate AS DATETIME2), 0, DATEADD(DAY, -3, @now)
FROM dbo.ClassRegistration r
JOIN dbo.ClassSession s ON s.Id = r.SessionId
JOIN dbo.Class c ON c.Id = s.ClassId
WHERE s.Status = 'CANCELED';
""")

w("""INSERT dbo.AuditLog (Id, AccountId, Action, EntityType, EntityId, Description, CreatedAt)
SELECT REPLACE(CAST(NEWID() AS VARCHAR(40)), '-', ''), 'acc-mgr-001', 'CREATE_CLASS', 'Class',
       CAST(c.Id AS VARCHAR(100)),
       N'Tạo lớp học "' + c.Name + N'" với sức chứa ' + CAST(c.Capacity AS NVARCHAR(10)) + N' hội viên.',
       c.CreatedAt
FROM dbo.Class c;

INSERT dbo.AuditLog (Id, AccountId, Action, EntityType, EntityId, Description, CreatedAt)
SELECT REPLACE(CAST(NEWID() AS VARCHAR(40)), '-', ''), 'acc-rec-001', 'PAY_INVOICE', 'MembershipInvoice',
       CAST(i.Id AS VARCHAR(100)),
       N'Xác nhận thanh toán hóa đơn ' + i.InvoiceNumber + N' số tiền '
           + FORMAT(i.Amount, 'N0') + N' đ.',
       i.PaidAt
FROM dbo.MembershipInvoice i
WHERE i.Status = 'PAID';

INSERT dbo.AuditLog (Id, AccountId, Action, EntityType, EntityId, Description, CreatedAt)
SELECT REPLACE(CAST(NEWID() AS VARCHAR(40)), '-', ''), 'acc-mgr-001', 'CANCEL_SESSION', 'ClassSession',
       CAST(s.Id AS VARCHAR(100)),
       N'Hủy buổi học ngày ' + FORMAT(s.SessionDate, 'dd/MM/yyyy') + N'. Lý do: '
           + ISNULL(s.CancelReason, N'Trung tâm điều chỉnh lịch.'),
       s.CanceledAt
FROM dbo.ClassSession s
WHERE s.Status = 'CANCELED';
""")

# ===========================================================================
# Role permissions, matching the front-end defaults
# ===========================================================================
PERMISSIONS = [
    ("members", "CENTER_MANAGER"),
    ("packages", "CENTER_MANAGER"),
    ("classes", "CENTER_MANAGER"),
    ("sports", "CENTER_MANAGER"),
    ("centerSchedule", "CENTER_MANAGER,RECEPTIONIST"),
    ("memberClasses", "MEMBER"),
    ("notifications", "MEMBER,COACH"),
    ("permissions", "CENTER_MANAGER"),
    ("membership", "MEMBER,CENTER_MANAGER"),
    ("counter", "RECEPTIONIST,CENTER_MANAGER"),
    ("payments", "RECEPTIONIST,CENTER_MANAGER"),
    ("invoices", "MEMBER,RECEPTIONIST,CENTER_MANAGER"),
    ("reports", "CENTER_MANAGER"),
    ("coach", "COACH,CENTER_MANAGER"),
]
w("-- 14. Ma trận phân quyền mặc định ------------------------------------------")
for key, roles in PERMISSIONS:
    w(f"INSERT dbo.RolePermissionConfig (FeatureKey, AllowedRolesCsv, UpdatedBy, UpdatedAt)"
      f" VALUES ({a(key)}, {a(roles)}, 'acc-mgr-001', @now);")
w("")
w("COMMIT TRANSACTION;")
w("GO")
w("")
w("PRINT 'Titan Arena dataset loaded.';")
w("SELECT 'Account' AS [Table], COUNT(*) AS Rows FROM dbo.Account")
w("UNION ALL SELECT 'Member', COUNT(*) FROM dbo.Member")
w("UNION ALL SELECT 'Coach', COUNT(*) FROM dbo.Coach")
w("UNION ALL SELECT 'Class', COUNT(*) FROM dbo.Class")
w("UNION ALL SELECT 'ClassSession', COUNT(*) FROM dbo.ClassSession")
w("UNION ALL SELECT 'ClassRegistration', COUNT(*) FROM dbo.ClassRegistration")
w("UNION ALL SELECT 'MemberSubscription', COUNT(*) FROM dbo.MemberSubscription")
w("UNION ALL SELECT 'MembershipInvoice', COUNT(*) FROM dbo.MembershipInvoice")
w("UNION ALL SELECT 'Notification', COUNT(*) FROM dbo.Notification;")
w("GO")

io.open(OUT, "w", encoding="utf-8-sig", newline="\r\n").write("\n".join(L) + "\n")
print("written", OUT, len(L), "lines;",
      len(sessions), "sessions;", len(registrations), "registrations;",
      len(subs), "subscriptions")
