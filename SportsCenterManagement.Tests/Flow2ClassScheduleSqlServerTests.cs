using APIViewModel.Class;
using APIViewModel.ClassRegistration;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.ClassRegistrationService;
using Services.ClassService;
using Services.NotificationService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

/// <summary>
/// Luồng lớp học trên SQL Server thật: tạo lớp kèm lịch, gán thêm đợt lịch cho
/// lớp đã có, và đăng ký/hủy theo lớp. Ba việc này đều ghi nhiều bảng trong một
/// giao dịch nên In-Memory provider không chứng minh được gì — khóa ứng dụng,
/// `BookedCount` cập nhật bằng SQL và rollback chỉ có nghĩa trên SQL Server.
///
/// Bỏ qua khi chưa cấu hình <c>SCMS_SQLSERVER_TEST_CONNECTION</c>, theo đúng
/// giao thức ở <c>Database/tests/flow2/README.md</c>.
/// </summary>
public class Flow2ClassScheduleSqlServerTests
{
    [Flow2SqlServerFact]
    public async Task CreateClass_GeneratesEverySessionOfTheRecurrence()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();
        await using SportsCenterManagementContext context = CreateContext();
        ClassService service = ClassServiceFor(context);

        ClassViewModel result = await service.CreateClassAsync(fixture.ManagerId, new CreateClassAPIViewModel
        {
            SportId = fixture.DisciplineId,
            Name = fixture.UniqueName("Create"),
            Capacity = 12,
            Status = "OPEN",
            Schedule = fixture.Schedule()
        });

        // Bốn tuần, thứ hai và thứ tư là tám buổi.
        Assert.Equal(8, result.TotalSessionCount);
        Assert.Equal(8, result.UpcomingSessionCount);
        Assert.Equal("OPEN", result.Status);
        ClassScheduleBlockViewModel block = Assert.Single(result.ScheduleBlocks);
        Assert.Equal(fixture.CoachId, block.CoachId);
        Assert.Equal(fixture.RoomId, block.RoomId);
        Assert.Equal(new List<int> { 1, 3 }, block.DaysOfWeek);
        Assert.Equal(8, block.SessionCount);

        await using SportsCenterManagementContext verify = CreateContext();
        List<ClassSession> stored = await verify.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == result.Id)
            .OrderBy(item => item.SessionDate)
            .ToListAsync();

        Assert.Equal(8, stored.Count);
        Assert.All(stored, session =>
        {
            Assert.Equal(fixture.CoachId, session.CoachId);
            Assert.Equal(fixture.RoomId, session.RoomId);
            Assert.Equal(TimeSpan.FromHours(18), session.StartTime);
            Assert.Equal(new TimeSpan(19, 30, 0), session.EndTime);
            Assert.Equal(12, session.Capacity);
            Assert.Equal(0, session.BookedCount);
            Assert.Equal("SCHEDULED", session.Status);
            Assert.Contains(session.SessionDate.DayOfWeek, new[] { DayOfWeek.Monday, DayOfWeek.Wednesday });
        });

        Assert.True(await verify.AuditLogs.AsNoTracking()
            .AnyAsync(item => item.Action == "CLASS_CREATED" && item.EntityId == result.Id.ToString()));
    }

    [Flow2SqlServerFact]
    public async Task CreateClass_CoachAlreadyBooked_RollsBackWholeClass()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();

        // Lớp đầu chiếm chỗ của giáo viên trong khung giờ đó.
        await using (SportsCenterManagementContext first = CreateContext())
        {
            await ClassServiceFor(first).CreateClassAsync(fixture.ManagerId, new CreateClassAPIViewModel
            {
                SportId = fixture.DisciplineId,
                Name = fixture.UniqueName("Holder"),
                Capacity = 10,
                Status = "OPEN",
                Schedule = fixture.Schedule()
            });
        }

        int classCountBefore;
        await using (SportsCenterManagementContext snapshot = CreateContext())
            classCountBefore = await snapshot.Classes.CountAsync();

        await using SportsCenterManagementContext context = CreateContext();
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ClassServiceFor(context).CreateClassAsync(fixture.ManagerId, new CreateClassAPIViewModel
            {
                SportId = fixture.DisciplineId,
                Name = fixture.UniqueName("Clash"),
                Capacity = 10,
                Status = "OPEN",
                Schedule = fixture.Schedule()
            }));

        Assert.Equal("COACH_SCHEDULE_CONFLICT", error.Code);

        // Không còn lớp rỗng sót lại: lớp và buổi nằm trong cùng giao dịch.
        await using SportsCenterManagementContext verify = CreateContext();
        Assert.Equal(classCountBefore, await verify.Classes.CountAsync());
    }

    [Flow2SqlServerFact]
    public async Task AddSchedule_AppendsAnotherBlockToTheSameClass()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();
        int classId;
        await using (SportsCenterManagementContext create = CreateContext())
        {
            ClassViewModel created = await ClassServiceFor(create).CreateClassAsync(
                fixture.ManagerId,
                new CreateClassAPIViewModel
                {
                    SportId = fixture.DisciplineId,
                    Name = fixture.UniqueName("Assign"),
                    Capacity = 12,
                    Status = "OPEN",
                    Schedule = fixture.Schedule()
                });
            classId = created.Id;
        }

        await using SportsCenterManagementContext context = CreateContext();
        // Đợt thứ hai: thứ sáu, ca khác, nên không chồng giờ với đợt đầu.
        AddClassScheduleAPIViewModel second = fixture.Schedule(days: new List<int> { 5 });
        second.StartTime = "08:00";
        second.EndTime = "09:00";

        AddClassScheduleResultViewModel result = await ClassServiceFor(context)
            .AddScheduleAsync(fixture.ManagerId, classId, second);

        Assert.Equal(4, result.CreatedSessionCount);
        Assert.Equal(classId, result.ClassId);

        // Hai đợt riêng, mỗi đợt giữ đúng giờ của mình: gộp một dòng thì đợt
        // sáng thứ sáu sẽ bị hiện theo giờ tối của đợt đầu.
        Assert.Equal(2, result.ScheduleBlocks.Count);
        ClassScheduleBlockViewModel evening = result.ScheduleBlocks
            .Single(item => item.StartTime == "18:00");
        ClassScheduleBlockViewModel morning = result.ScheduleBlocks
            .Single(item => item.StartTime == "08:00");
        Assert.Equal("19:30", evening.EndTime);
        Assert.Equal(new List<int> { 1, 3 }, evening.DaysOfWeek);
        Assert.Equal(8, evening.SessionCount);
        Assert.Equal("09:00", morning.EndTime);
        Assert.Equal(new List<int> { 5 }, morning.DaysOfWeek);
        Assert.Equal(4, morning.SessionCount);

        await using SportsCenterManagementContext verify = CreateContext();
        Assert.Equal(12, await verify.ClassSessions.CountAsync(item => item.ClassId == classId));
        Assert.True(await verify.AuditLogs.AsNoTracking()
            .AnyAsync(item => item.Action == "CLASS_SCHEDULE_ADDED" && item.EntityId == classId.ToString()));
    }

    [Flow2SqlServerFact]
    public async Task AddSchedule_OverlappingOwnSessions_IsRejected()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();
        int classId;
        await using (SportsCenterManagementContext create = CreateContext())
        {
            classId = (await ClassServiceFor(create).CreateClassAsync(
                fixture.ManagerId,
                new CreateClassAPIViewModel
                {
                    SportId = fixture.DisciplineId,
                    Name = fixture.UniqueName("Overlap"),
                    Capacity = 12,
                    Status = "OPEN",
                    Schedule = fixture.Schedule()
                })).Id;
        }

        await using SportsCenterManagementContext context = CreateContext();
        // Cùng thứ, cùng ca với đợt đầu nên lớp sẽ tự chồng giờ chính nó.
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ClassServiceFor(context).AddScheduleAsync(fixture.ManagerId, classId, fixture.Schedule()));

        Assert.Contains(error.Code, new[] { "CLASS_SCHEDULE_OVERLAP", "COACH_SCHEDULE_CONFLICT" });

        await using SportsCenterManagementContext verify = CreateContext();
        Assert.Equal(8, await verify.ClassSessions.CountAsync(item => item.ClassId == classId));
    }

    [Flow2SqlServerFact]
    public async Task RegisterClass_BooksEveryUpcomingSession_AndCancelReleasesThem()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();
        int classId;
        await using (SportsCenterManagementContext create = CreateContext())
        {
            classId = (await ClassServiceFor(create).CreateClassAsync(
                fixture.ManagerId,
                new CreateClassAPIViewModel
                {
                    SportId = fixture.DisciplineId,
                    Name = fixture.UniqueName("Enroll"),
                    Capacity = 10,
                    Status = "OPEN",
                    Schedule = fixture.Schedule()
                })).Id;
        }

        await using SportsCenterManagementContext context = CreateContext();
        ClassRegistrationService registrations = new ClassRegistrationService(
            context, new AuditLogService(context), new NotificationService(context));

        ClassEnrollmentViewModel enrollment = await registrations.RegisterClassAsync(
            fixture.MemberId, classId, "MEMBER", fixture.MemberId);

        // Một lượt đăng ký giữ chỗ cho cả tám buổi.
        Assert.Equal("BOOKED", enrollment.Status);
        Assert.Equal(8, enrollment.BookedSessionCount);
        Assert.Equal(8, enrollment.UpcomingSessionCount);
        Assert.Equal(8, enrollment.Sessions.Count);
        // Lịch phía thành viên cũng theo từng đợt, cùng cách gộp với quản lý.
        ClassScheduleBlockViewModel enrolledBlock = Assert.Single(enrollment.ScheduleBlocks);
        Assert.Equal("18:00", enrolledBlock.StartTime);
        Assert.Equal(new List<int> { 1, 3 }, enrolledBlock.DaysOfWeek);

        await using (SportsCenterManagementContext verify = CreateContext())
        {
            Assert.Equal(8, await verify.ClassRegistrations.CountAsync(item =>
                item.MemberId == fixture.MemberId && item.Status == "BOOKED" && item.Session.ClassId == classId));
            List<ClassSession> booked = await verify.ClassSessions.AsNoTracking()
                .Where(item => item.ClassId == classId).ToListAsync();
            Assert.All(booked, session => Assert.Equal(1, session.BookedCount));
            // Một thông báo cho cả lượt, không phải tám.
            Assert.Equal(1, await verify.Notifications.CountAsync(item =>
                item.RecipientId == fixture.MemberId && item.Type == "CLASS_REGISTERED"));
        }

        // Đăng ký lại cùng lớp là trùng lặp.
        await using (SportsCenterManagementContext duplicate = CreateContext())
        {
            ClassRegistrationService again = new ClassRegistrationService(
                duplicate, new AuditLogService(duplicate), new NotificationService(duplicate));
            BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                again.RegisterClassAsync(fixture.MemberId, classId, "MEMBER", fixture.MemberId));
            Assert.Equal("DUPLICATE_REGISTRATION", error.Code);
        }

        await using SportsCenterManagementContext cancelContext = CreateContext();
        ClassRegistrationService canceller = new ClassRegistrationService(
            cancelContext, new AuditLogService(cancelContext), new NotificationService(cancelContext));
        ClassEnrollmentViewModel canceled = await canceller.CancelClassRegistrationAsync(
            fixture.MemberId, classId, "Đổi kế hoạch tập", fixture.ManagerId, true);

        Assert.Equal("CANCELED", canceled.Status);
        Assert.Equal(0, canceled.BookedSessionCount);

        await using SportsCenterManagementContext final = CreateContext();
        Assert.Equal(0, await final.ClassRegistrations.CountAsync(item =>
            item.MemberId == fixture.MemberId && item.Status == "BOOKED" && item.Session.ClassId == classId));
        List<ClassSession> released = await final.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId).ToListAsync();
        // Chỗ được nhả lại ở mọi buổi chưa bắt đầu.
        Assert.All(released, session => Assert.Equal(0, session.BookedCount));
    }

    [Flow2SqlServerFact]
    public async Task RegisterClass_WhenOneSessionIsFull_RejectsTheWholeEnrollment()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();
        int classId;
        await using (SportsCenterManagementContext create = CreateContext())
        {
            classId = (await ClassServiceFor(create).CreateClassAsync(
                fixture.ManagerId,
                new CreateClassAPIViewModel
                {
                    SportId = fixture.DisciplineId,
                    Name = fixture.UniqueName("Full"),
                    Capacity = 10,
                    Status = "OPEN",
                    Schedule = fixture.Schedule()
                })).Id;
        }

        // Một buổi giữa đợt đã kín chỗ.
        int fullSessionId;
        await using (SportsCenterManagementContext fill = CreateContext())
        {
            ClassSession target = await fill.ClassSessions
                .Where(item => item.ClassId == classId)
                .OrderBy(item => item.SessionDate)
                .Skip(3)
                .FirstAsync();
            target.BookedCount = target.Capacity;
            fullSessionId = target.Id;
            await fill.SaveChangesAsync();
        }

        await using SportsCenterManagementContext context = CreateContext();
        ClassRegistrationService registrations = new ClassRegistrationService(
            context, new AuditLogService(context), new NotificationService(context));

        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            registrations.RegisterClassAsync(fixture.MemberId, classId, "MEMBER", fixture.MemberId));

        Assert.Equal("CLASS_FULL", error.Code);

        // Không giữ chỗ nửa vời: không buổi nào được đặt, không chỗ nào bị trừ.
        await using SportsCenterManagementContext verify = CreateContext();
        Assert.False(await verify.ClassRegistrations.AnyAsync(item =>
            item.MemberId == fixture.MemberId && item.Session.ClassId == classId));
        List<ClassSession> sessions = await verify.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId).ToListAsync();
        Assert.All(sessions, session =>
            Assert.Equal(session.Id == fullSessionId ? session.Capacity : 0, session.BookedCount));
    }

    [Flow2SqlServerFact]
    public async Task RegisterClass_TwoMembersForTheLastSeat_OnlyOneSucceeds()
    {
        ScheduleFixture fixture = await ScheduleFixture.CreateAsync();
        int classId;
        await using (SportsCenterManagementContext create = CreateContext())
        {
            // Sức chứa 1: hai thành viên tranh chỗ cuối của cả lớp.
            classId = (await ClassServiceFor(create).CreateClassAsync(
                fixture.ManagerId,
                new CreateClassAPIViewModel
                {
                    SportId = fixture.DisciplineId,
                    Name = fixture.UniqueName("Race"),
                    Capacity = 1,
                    Status = "OPEN",
                    Schedule = fixture.Schedule()
                })).Id;
        }

        string[] outcomes = await Task.WhenAll(
            EnrollAsync(fixture.MemberId, classId),
            EnrollAsync(fixture.SecondMemberId, classId));

        Assert.Single(outcomes, value => value == "OK");
        Assert.Single(outcomes, value => value == "CLASS_FULL");

        await using SportsCenterManagementContext verify = CreateContext();
        List<ClassSession> sessions = await verify.ClassSessions.AsNoTracking()
            .Where(item => item.ClassId == classId).ToListAsync();
        Assert.All(sessions, session => Assert.Equal(1, session.BookedCount));
        Assert.Equal(8, await verify.ClassRegistrations.CountAsync(item =>
            item.Status == "BOOKED" && item.Session.ClassId == classId));
    }

    private static async Task<string> EnrollAsync(string memberId, int classId)
    {
        try
        {
            await using SportsCenterManagementContext context = CreateContext();
            ClassRegistrationService service = new ClassRegistrationService(
                context, new AuditLogService(context), new NotificationService(context));
            await service.RegisterClassAsync(memberId, classId, "MEMBER", memberId);
            return "OK";
        }
        catch (BusinessRuleException error)
        {
            return error.Code;
        }
    }

    private static ClassService ClassServiceFor(SportsCenterManagementContext context) =>
        new ClassService(context, new AuditLogService(context), new NotificationService(context));

    private static SportsCenterManagementContext CreateContext()
    {
        string connection = Environment.GetEnvironmentVariable("SCMS_SQLSERVER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("No SQL Server test environment configured.");
        SqlConnectionStringBuilder settings = new SqlConnectionStringBuilder(connection);
        if (!settings.InitialCatalog.StartsWith("SCMS_Flow2_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Only an explicitly provisioned SCMS_Flow2_Test_ database is permitted.");
        return new SportsCenterManagementContext(new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseSqlServer(connection).Options);
    }

    /// <summary>
    /// Bộ môn, giáo viên, phòng và thành viên riêng cho mỗi lần chạy, cùng một
    /// khối lịch mẫu bắt đầu từ thứ hai tới. Mỗi fixture dùng dữ liệu tên riêng
    /// nên các test không tranh nhau lịch của cùng một giáo viên.
    /// </summary>
    private sealed record ScheduleFixture(
        string ManagerId,
        string CoachId,
        string MemberId,
        string SecondMemberId,
        int DisciplineId,
        int RoomId,
        DateTime FirstMonday)
    {
        public string UniqueName(string prefix) => prefix + " " + Guid.NewGuid().ToString("N");

        /// <summary>Lịch bốn tuần, mặc định thứ hai và thứ tư, ca tối 18:00–19:30.</summary>
        public AddClassScheduleAPIViewModel Schedule(List<int>? days = null) => new AddClassScheduleAPIViewModel
        {
            CoachId = CoachId,
            RoomId = RoomId,
            StartDate = FirstMonday.ToString("yyyy-MM-dd"),
            EndDate = FirstMonday.AddDays(27).ToString("yyyy-MM-dd"),
            DaysOfWeek = days ?? new List<int> { 1, 3 },
            StartTime = "18:00",
            EndTime = "19:30"
        };

        public static async Task<ScheduleFixture> CreateAsync()
        {
            await using SportsCenterManagementContext context = CreateContext();
            Role managerRole = await context.Roles.SingleAsync(item => item.Name == "CenterManager");
            Role memberRole = await context.Roles.SingleAsync(item => item.Name == "Member");
            Role coachRole = await context.Roles.SingleAsync(item => item.Name == "Coach");

            Account manager = AccountFor(managerRole);
            Account coach = AccountFor(coachRole);
            Account[] members = [AccountFor(memberRole), AccountFor(memberRole)];
            context.Accounts.AddRange([manager, coach, .. members]);
            context.CenterManagers.Add(new CenterManager
            {
                AccountId = manager.Id,
                FullName = "Test Manager",
                CreatedAt = DateTime.UtcNow
            });
            context.Coaches.Add(new Coach
            {
                AccountId = coach.Id,
                FullName = "Test Coach",
                CreatedAt = DateTime.UtcNow
            });
            foreach (Account member in members)
                context.Members.Add(new Member
                {
                    AccountId = member.Id,
                    FullName = "Test Member",
                    MemberCode = "T" + member.Id[..24],
                    CreatedAt = DateTime.UtcNow
                });

            Discipline discipline = new Discipline
            {
                Name = "Test " + Guid.NewGuid().ToString("N"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Room room = new Room
            {
                Name = "Test " + Guid.NewGuid().ToString("N"),
                Capacity = 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            MembershipPackage package = new MembershipPackage
            {
                Name = "Test " + Guid.NewGuid().ToString("N"),
                Benefits = "[]",
                DurationMonths = 6,
                Price = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Disciplines.Add(discipline);
            context.Rooms.Add(room);
            context.MembershipPackages.Add(package);
            await context.SaveChangesAsync();

            // Thứ hai đầu tiên còn ở tương lai, để buổi đầu không nằm trong quá khứ.
            DateTime today = DateTime.UtcNow.AddHours(7).Date;
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            DateTime firstMonday = today.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);

            // Gói tập phải phủ hết thời gian lớp, vì đăng ký là đăng ký cả lớp.
            foreach (Account member in members)
                context.MemberSubscriptions.Add(new MemberSubscription
                {
                    MemberId = member.Id,
                    PackageId = package.Id,
                    PackageName = package.Name,
                    Benefits = "[]",
                    Kind = "REGISTER",
                    Status = "CONFIRMED",
                    DurationMonths = 6,
                    StartDate = DateOnly.FromDateTime(today.AddDays(-1)),
                    EndDate = DateOnly.FromDateTime(firstMonday.AddDays(120)),
                    CreatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();

            return new ScheduleFixture(
                manager.Id,
                coach.Id,
                members[0].Id,
                members[1].Id,
                discipline.Id,
                room.Id,
                firstMonday);
        }

        private static Account AccountFor(Role role)
        {
            string id = Guid.NewGuid().ToString("N");
            return new Account
            {
                Id = id,
                Email = id + "@example.com",
                PasswordHash = Guid.NewGuid().ToString("N"),
                RoleId = role.Id,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
