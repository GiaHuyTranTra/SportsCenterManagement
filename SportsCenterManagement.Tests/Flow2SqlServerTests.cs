using APIViewModel.ClassSession;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.ClassRegistrationService;
using Services.ClassSessionService;
using Services.NotificationService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public sealed class Flow2SqlServerFactAttribute : FactAttribute
{
    public Flow2SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCMS_SQLSERVER_TEST_CONNECTION")))
            Skip = "Requires an explicitly provisioned SCMS_Flow2_Test_ database with the reviewed SQL schema.";
    }
}

public class Flow2SqlServerTests
{
    [Flow2SqlServerFact]
    public async Task AuditFailure_RollsBackCounterRegistrationAndNotifications()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        await using SportsCenterManagementContext context = CreateContext();
        ClassRegistrationService service = new ClassRegistrationService(context, new FailingAudit(), new NotificationService(context));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(
            fixture.MemberIds[0], fixture.SessionIds[0], "MEMBER", fixture.MemberIds[0]));
        await using SportsCenterManagementContext verify = CreateContext();
        Assert.False(await verify.ClassRegistrations.AnyAsync(item => item.SessionId == fixture.SessionIds[0]));
        Assert.False(await verify.Notifications.AnyAsync(item => item.RecipientId == fixture.MemberIds[0]));
        Assert.Equal(0, (await verify.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[0])).BookedCount);
    }

    [Flow2SqlServerFact]
    public async Task FinalSlot_OnlyOneMemberSucceeds()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        string[] outcomes = await Task.WhenAll(
            BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]),
            BookAsync(fixture.MemberIds[1], fixture.SessionIds[0]));
        Assert.Single(outcomes, value => value == "OK");
        Assert.Single(outcomes, value => value == "CLASS_FULL");
        await AssertInvariantAsync(fixture.SessionIds[0]);
    }

    [Flow2SqlServerFact]
    public async Task DuplicateRequest_CreatesOneBookingAndOneNotification()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        string[] outcomes = await Task.WhenAll(
            BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]),
            BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]));
        Assert.Single(outcomes, value => value == "OK");
        Assert.Single(outcomes, value => value == "DUPLICATE_REGISTRATION");
        await using SportsCenterManagementContext context = CreateContext();
        Assert.Equal(1, await context.Notifications.CountAsync(item => item.RecipientId == fixture.MemberIds[0]));
        await AssertInvariantAsync(fixture.SessionIds[0]);
    }

    [Flow2SqlServerFact]
    public async Task SameMember_ConcurrentOverlappingSessions_OnlyOneSucceeds()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        string[] outcomes = await Task.WhenAll(
            BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]),
            BookAsync(fixture.MemberIds[0], fixture.SessionIds[1]));
        Assert.Single(outcomes, value => value == "OK");
        Assert.Single(outcomes, value => value == "MEMBER_SCHEDULE_CONFLICT");
        foreach (int sessionId in fixture.SessionIds) await AssertInvariantAsync(sessionId);
    }

    [Flow2SqlServerFact]
    public async Task BookingAgainstSessionCancellation_NeverLeavesActiveBooking()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        await Task.WhenAll(BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]), AttemptAsync(async () =>
        {
            await using SportsCenterManagementContext context = CreateContext();
            await SessionService(context).CancelSessionAsync(fixture.ManagerId, fixture.SessionIds[0], "Test cancellation");
        }));
        await using SportsCenterManagementContext verify = CreateContext();
        Assert.Equal("CANCELED", (await verify.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[0])).Status);
        Assert.False(await verify.ClassRegistrations.AnyAsync(item => item.SessionId == fixture.SessionIds[0] && item.Status == "BOOKED"));
        await AssertInvariantAsync(fixture.SessionIds[0]);
    }

    [Flow2SqlServerFact]
    public async Task ConcurrentScheduleCreation_PreventsCoachAndRoomCollision()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        async Task CreateAsync()
        {
            await using SportsCenterManagementContext context = CreateContext();
            ClassSession source = await context.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[0]);
            await SessionService(context).CreateSessionAsync(fixture.ManagerId, new CreateClassSessionAPIViewModel
            {
                ClassId = source.ClassId, CoachId = source.CoachId, RoomId = source.RoomId,
                Date = source.SessionDate.AddDays(1).ToString("yyyy-MM-dd"),
                StartTime = "09:00", EndTime = "10:00", Capacity = 1
            });
        }
        string[] outcomes = await Task.WhenAll(AttemptAsync(CreateAsync), AttemptAsync(CreateAsync));
        Assert.Single(outcomes, value => value == "OK");
        Assert.Single(outcomes, value => value == "COACH_SCHEDULE_CONFLICT");
    }

    [Flow2SqlServerFact]
    public async Task ConcurrentCancellation_DecrementsCounterOnce()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();
        Assert.Equal("OK", await BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]));
        await using SportsCenterManagementContext context = CreateContext();
        int registrationId = await context.ClassRegistrations.Where(item => item.SessionId == fixture.SessionIds[0]).Select(item => item.Id).SingleAsync();
        async Task CancelAsync()
        {
            await using SportsCenterManagementContext command = CreateContext();
            await RegistrationService(command).CancelRegistrationAsync(registrationId, null, fixture.MemberIds[0], false);
        }
        string[] outcomes = await Task.WhenAll(AttemptAsync(CancelAsync), AttemptAsync(CancelAsync));
        Assert.Single(outcomes, value => value == "OK");
        Assert.Single(outcomes, value => value == "REGISTRATION_ALREADY_CANCELED");
        await AssertInvariantAsync(fixture.SessionIds[0]);
    }

    [Flow2SqlServerFact]
    public async Task RescheduleSession_MemberHasOverlappingSession_ThrowsMemberScheduleConflictAndPreservesState()
    {
        SqlFixture fixture = await SqlFixture.CreateAsync();

        await using (SportsCenterManagementContext setupContext = CreateContext())
        {
            ClassSession session1 = await setupContext.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[1]);
            session1.StartTime = TimeSpan.FromHours(14);
            session1.EndTime = TimeSpan.FromHours(15);
            await setupContext.SaveChangesAsync();
        }

        Assert.Equal("OK", await BookAsync(fixture.MemberIds[0], fixture.SessionIds[0]));
        Assert.Equal("OK", await BookAsync(fixture.MemberIds[0], fixture.SessionIds[1]));

        await using (SportsCenterManagementContext actContext = CreateContext())
        {
            ClassSession s0 = await actContext.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[0]);
            UpdateClassSessionAPIViewModel request = new UpdateClassSessionAPIViewModel
            {
                ClassId = s0.ClassId,
                CoachId = s0.CoachId,
                RoomId = s0.RoomId,
                Date = s0.SessionDate.ToString("yyyy-MM-dd"),
                StartTime = "14:00",
                EndTime = "15:00",
                Capacity = s0.Capacity
            };

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                SessionService(actContext).UpdateSessionAsync(fixture.ManagerId, fixture.SessionIds[0], request));
            Assert.Equal("MEMBER_SCHEDULE_CONFLICT", ex.Code);
        }

        await using (SportsCenterManagementContext verifyContext = CreateContext())
        {
            ClassSession s0Verify = await verifyContext.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[0]);
            Assert.Equal(TimeSpan.FromHours(9), s0Verify.StartTime);
            Assert.Equal(TimeSpan.FromHours(10), s0Verify.EndTime);
            Assert.Equal(1, s0Verify.BookedCount);
            Assert.Equal("SCHEDULED", s0Verify.Status);

            ClassSession s1Verify = await verifyContext.ClassSessions.SingleAsync(item => item.Id == fixture.SessionIds[1]);
            Assert.Equal(TimeSpan.FromHours(14), s1Verify.StartTime);
            Assert.Equal(TimeSpan.FromHours(15), s1Verify.EndTime);
            Assert.Equal(1, s1Verify.BookedCount);

            bool s0Booked = await verifyContext.ClassRegistrations.AnyAsync(r =>
                r.SessionId == fixture.SessionIds[0] && r.MemberId == fixture.MemberIds[0] && r.Status == "BOOKED");
            bool s1Booked = await verifyContext.ClassRegistrations.AnyAsync(r =>
                r.SessionId == fixture.SessionIds[1] && r.MemberId == fixture.MemberIds[0] && r.Status == "BOOKED");
            Assert.True(s0Booked);
            Assert.True(s1Booked);

            bool rescheduledNotif = await verifyContext.Notifications.AnyAsync(n =>
                n.Type == "SESSION_RESCHEDULED" && n.EntityId == fixture.SessionIds[0].ToString());
            Assert.False(rescheduledNotif);
        }

        await AssertInvariantAsync(fixture.SessionIds[0]);
        await AssertInvariantAsync(fixture.SessionIds[1]);
    }

    private static Task<string> BookAsync(string memberId, int sessionId) => AttemptAsync(async () =>
    {
        await using SportsCenterManagementContext context = CreateContext();
        APIViewModel.ClassRegistration.ClassRegistrationViewModel response =
            await RegistrationService(context).RegisterAsync(memberId, sessionId, "MEMBER", memberId);
        Assert.Equal(1, response.Session.BookedCount);
        Assert.Equal(0, response.Session.RemainingCapacity);
    });

    private static async Task<string> AttemptAsync(Func<Task> action)
    {
        try { await action(); return "OK"; }
        catch (BusinessRuleException exception) { return exception.Code; }
    }

    private static async Task AssertInvariantAsync(int sessionId)
    {
        await using SportsCenterManagementContext context = CreateContext();
        ClassSession session = await context.ClassSessions.SingleAsync(item => item.Id == sessionId);
        Assert.Equal(await context.ClassRegistrations.CountAsync(item => item.SessionId == sessionId && item.Status == "BOOKED"), session.BookedCount);
    }

    private static ClassRegistrationService RegistrationService(SportsCenterManagementContext context) =>
        new ClassRegistrationService(context, new AuditLogService(context), new NotificationService(context));
    private static ClassSessionService SessionService(SportsCenterManagementContext context) =>
        new ClassSessionService(context, new AuditLogService(context), new NotificationService(context));

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

    private sealed class FailingAudit : IAuditLogService
    {
        public Task<APIViewModel.AuditLog.PagedAuditLogAPIViewModel> GetAuditLogsAsync(int page, int pageSize,
            DateTime? from, DateTime? to, string? accountId, string? action, string? entityType,
            string? entityId = null, int? classId = null, int? sessionId = null, string? search = null) =>
            throw new NotSupportedException();

        public Task StageAuditLogAsync(string? accountId, string action, string? entityType,
            string? entityId, string? description) => throw new InvalidOperationException("Simulated audit failure.");
    }

    private sealed record SqlFixture(string ManagerId, string[] MemberIds, int[] SessionIds)
    {
        public static async Task<SqlFixture> CreateAsync()
        {
            await using SportsCenterManagementContext context = CreateContext();
            // The test database is provisioned by the operator; these tests never create/drop databases.
            Role managerRole = await context.Roles.SingleAsync(item => item.Name == "CenterManager");
            Role memberRole = await context.Roles.SingleAsync(item => item.Name == "Member");
            Role coachRole = await context.Roles.SingleAsync(item => item.Name == "Coach");
            Account manager = AccountFor(managerRole);
            Account[] members = [AccountFor(memberRole), AccountFor(memberRole)];
            Account[] coaches = [AccountFor(coachRole), AccountFor(coachRole)];
            context.Accounts.AddRange([manager, .. members, .. coaches]);
            foreach (Account account in members)
                context.Members.Add(new Member { AccountId = account.Id, FullName = "Test Member", MemberCode = "T" + account.Id[..24], CreatedAt = DateTime.UtcNow });
            foreach (Account account in coaches)
                context.Coaches.Add(new Coach { AccountId = account.Id, FullName = "Test Coach", CreatedAt = DateTime.UtcNow });
            Discipline discipline = new Discipline { Name = "Test " + Guid.NewGuid().ToString("N"), IsActive = true, CreatedAt = DateTime.UtcNow };
            MembershipPackage package = new MembershipPackage
            {
                Name = "Test " + Guid.NewGuid().ToString("N"), Benefits = "[]", DurationMonths = 1,
                Price = 1, IsActive = true, CreatedAt = DateTime.UtcNow
            };
            context.Disciplines.Add(discipline);
            context.MembershipPackages.Add(package);
            await context.SaveChangesAsync();
            DateTime date = DateTime.UtcNow.AddHours(7).Date.AddDays(3);
            foreach (Account member in members)
                context.MemberSubscriptions.Add(new MemberSubscription
                {
                    MemberId = member.Id, PackageId = package.Id, PackageName = package.Name,
                    Benefits = "[]", Kind = "REGISTER", Status = "CONFIRMED", DurationMonths = 1,
                    StartDate = DateOnly.FromDateTime(date.AddDays(-1)), EndDate = DateOnly.FromDateTime(date.AddDays(10)),
                    CreatedAt = DateTime.UtcNow
                });
            Class currentClass = new Class
            {
                DisciplineId = discipline.Id, Name = "Test " + Guid.NewGuid().ToString("N"),
                Capacity = 1, Status = "OPEN", CreatedBy = manager.Id, CreatedAt = DateTime.UtcNow
            };
            context.Classes.Add(currentClass);
            Room[] rooms = [RoomFor(), RoomFor()];
            context.Rooms.AddRange(rooms);
            await context.SaveChangesAsync();
            ClassSession[] sessions = Enumerable.Range(0, 2).Select(index => new ClassSession
            {
                ClassId = currentClass.Id, CoachId = coaches[index].Id, RoomId = rooms[index].Id,
                SessionDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10),
                Capacity = 1, Status = "SCHEDULED", CreatedBy = manager.Id, CreatedAt = DateTime.UtcNow
            }).ToArray();
            context.ClassSessions.AddRange(sessions);
            await context.SaveChangesAsync();
            return new SqlFixture(manager.Id, members.Select(item => item.Id).ToArray(), sessions.Select(item => item.Id).ToArray());
        }

        private static Account AccountFor(Role role)
        {
            string id = Guid.NewGuid().ToString("N");
            return new Account { Id = id, Email = id + "@example.com", PasswordHash = Guid.NewGuid().ToString("N"),
                RoleId = role.Id, Status = "Active", CreatedAt = DateTime.UtcNow };
        }
        private static Room RoomFor() => new Room { Name = "Test " + Guid.NewGuid().ToString("N"),
            Capacity = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
    }
}
