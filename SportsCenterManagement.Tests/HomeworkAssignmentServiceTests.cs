using APIViewModel.Homework;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Services.HomeworkAssignmentService;

namespace SportsCenterManagement.Tests;

public sealed class HomeworkAssignmentServiceTests
{
    [Fact]
    public async Task DirectMember_WithTeachingRelationship_CreatesAssignmentNotificationAndAudit()
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        SeedPeople(context);
        context.TrainingPlans.Add(Plan("coach-1", "member-1"));
        await context.SaveChangesAsync();
        HomeworkAssignmentService service = new(context);

        var (result, items) = await service.AssignAsync("coach-1", Request("MEMBER", memberId: "member-1"));

        Assert.Equal(AssignHomeworkResult.Success, result);
        Assert.Single(items);
        Assert.Equal("DIRECT", items[0].SourceType);
        Assert.Single(context.Notifications, item => item.RecipientId == "member-1" && item.Type == "COACH_HOMEWORK_NOTICE");
        AuditLog audit = Assert.Single(context.AuditLogs, item => item.Action == "ASSIGN_HOMEWORK");
        Assert.Contains("MEMBER", audit.Description);
        Assert.DoesNotContain("3 hiệp plank", audit.Description);
    }

    [Fact]
    public async Task ClassTarget_DeduplicatesMemberAcrossFutureSessions()
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        SeedPeople(context);
        SeedClass(context, "coach-1", sessionIds: [11, 12]);
        Register(context, 11, "member-1", "BOOKED");
        Register(context, 12, "member-1", "BOOKED");
        Register(context, 12, "member-2", "BOOKED");
        await context.SaveChangesAsync();
        HomeworkAssignmentService service = new(context);

        var (result, items) = await service.AssignAsync("coach-1", Request("CLASS", classId: 7));

        Assert.Equal(AssignHomeworkResult.Success, result);
        Assert.Equal(["member-1", "member-2"], items.Select(item => item.MemberId).Order().ToArray());
        Assert.Equal(2, await context.Notifications.CountAsync(item => item.Type == "COACH_HOMEWORK_NOTICE"));
    }

    [Fact]
    public async Task SessionTarget_IncludesOnlyExactBookedRegistrations()
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        SeedPeople(context);
        SeedClass(context, "coach-1", sessionIds: [11, 12]);
        Register(context, 11, "member-1", "BOOKED");
        Register(context, 11, "member-2", "CANCELED", canceled: true);
        Register(context, 12, "member-2", "BOOKED");
        await context.SaveChangesAsync();
        HomeworkAssignmentService service = new(context);

        var (result, items) = await service.AssignAsync("coach-1", Request("SESSION", sessionId: 11));

        Assert.Equal(AssignHomeworkResult.Success, result);
        Assert.Equal("member-1", Assert.Single(items).MemberId);
        Assert.All(items, item => Assert.Equal(11, item.SessionId));
    }

    [Fact]
    public async Task PastOrCanceledOnlyTarget_ReturnsNoMembersAndWritesNothing()
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        SeedPeople(context);
        SeedClass(context, "coach-1", sessionIds: [11], date: DateTime.UtcNow.Date.AddDays(-1));
        Register(context, 11, "member-1", "BOOKED");
        await context.SaveChangesAsync();
        HomeworkAssignmentService service = new(context);

        var (result, items) = await service.AssignAsync("coach-1", Request("CLASS", classId: 7));

        Assert.Equal(AssignHomeworkResult.TargetHasNoMembers, result);
        Assert.Empty(items);
        Assert.Empty(context.HomeworkAssignments);
        Assert.Empty(context.Notifications);
    }

    [Fact]
    public async Task UnrelatedCoach_UsesOneNonLeakingForbiddenResult()
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        SeedPeople(context);
        SeedClass(context, "coach-1", sessionIds: [11]);
        Register(context, 11, "member-1", "BOOKED");
        await context.SaveChangesAsync();
        HomeworkAssignmentService service = new(context);

        var existing = await service.AssignAsync("coach-2", Request("CLASS", classId: 7));
        var missing = await service.AssignAsync("coach-2", Request("CLASS", classId: 999));

        Assert.Equal(AssignHomeworkResult.TargetNotFoundOrForbidden, existing.Result);
        Assert.Equal(existing.Result, missing.Result);
        Assert.Empty(context.HomeworkAssignments);
    }

    [Theory]
    [InlineData("MEMBER", null, 7, null)]
    [InlineData("CLASS", "member-1", 7, null)]
    [InlineData("SESSION", null, null, null)]
    [InlineData("UNKNOWN", "member-1", null, null)]
    public async Task InvalidTargetShape_IsRejected(string target, string? memberId, int? classId, int? sessionId)
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        HomeworkAssignmentService service = new(context);

        var (result, items) = await service.AssignAsync("coach-1", Request(target, memberId, classId, sessionId));

        Assert.Equal(AssignHomeworkResult.InvalidRequest, result);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Histories_AreNewestFirstWithDescendingIdTieBreak()
    {
        await using SportsCenterManagementContext context = CreateInMemoryContext();
        SeedPeople(context);
        DateTime sameTime = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);
        context.HomeworkAssignments.AddRange(
            Assignment("coach-1", "member-1", "Old", sameTime.AddDays(-1)),
            Assignment("coach-1", "member-1", "First same time", sameTime),
            Assignment("coach-1", "member-1", "Second same time", sameTime));
        await context.SaveChangesAsync();
        HomeworkAssignmentService service = new(context);

        var memberHistory = await service.GetMemberHistoryAsync("member-1");
        var coachHistory = await service.GetCoachHistoryAsync("coach-1");

        Assert.Equal(["Second same time", "First same time", "Old"], memberHistory.Select(item => item.Title));
        Assert.Equal(memberHistory.Select(item => item.Id), coachHistory.Select(item => item.Id));
    }

    [Fact]
    public async Task NotificationFailure_RollsBackPreviouslySavedAssignments()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>().UseSqlite(connection).Options;
        await using SportsCenterManagementContext context = new(options);
        await context.Database.EnsureCreatedAsync();
        SeedRelationalDirectTarget(context);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_homework_notice BEFORE INSERT ON Notification " +
            "WHEN NEW.Type = 'COACH_HOMEWORK_NOTICE' BEGIN SELECT RAISE(ABORT, 'notification failed'); END;");
        HomeworkAssignmentService service = new(context);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.AssignAsync("coach-1", Request("MEMBER", memberId: "member-1")));
        context.ChangeTracker.Clear();

        Assert.Empty(await context.HomeworkAssignments.ToListAsync());
        Assert.Empty(await context.AuditLogs.Where(item => item.Action == "ASSIGN_HOMEWORK").ToListAsync());
    }

    private static CreateHomeworkAssignmentAPIViewModel Request(
        string targetType,
        string? memberId = null,
        int? classId = null,
        int? sessionId = null) => new()
        {
            TargetType = targetType,
            MemberId = memberId,
            ClassId = classId,
            SessionId = sessionId,
            Title = "Plank cơ bản",
            Instructions = "Thực hiện 3 hiệp plank."
        };

    private static SportsCenterManagementContext CreateInMemoryContext() => new(
        new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SeedPeople(SportsCenterManagementContext context)
    {
        context.Coaches.AddRange(
            new Coach { AccountId = "coach-1", FullName = "HLV Một" },
            new Coach { AccountId = "coach-2", FullName = "HLV Hai" });
        context.Members.AddRange(
            new Member { AccountId = "member-1", MemberCode = "M1", FullName = "Học viên Một" },
            new Member { AccountId = "member-2", MemberCode = "M2", FullName = "Học viên Hai" });
    }

    private static TrainingPlan Plan(string coachId, string memberId) => new()
    {
        CoachId = coachId, PlanType = "INDIVIDUAL", MemberId = memberId,
        Title = "Kế hoạch", Goal = "Khỏe", ExercisesJson = "[]",
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), CreatedAt = DateTime.UtcNow
    };

    private static void SeedClass(
        SportsCenterManagementContext context,
        string coachId,
        int[] sessionIds,
        DateTime? date = null)
    {
        context.Classes.Add(new Class { Id = 7, DisciplineId = 1, Name = "Yoga", Capacity = 10, Status = "OPEN" });
        foreach (int id in sessionIds)
        {
            context.ClassSessions.Add(new ClassSession
            {
                Id = id, ClassId = 7, CoachId = coachId, RoomId = 1,
                SessionDate = date ?? DateTime.UtcNow.Date.AddDays(2),
                StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9),
                Capacity = 10, Status = "SCHEDULED"
            });
        }
    }

    private static void Register(
        SportsCenterManagementContext context,
        int sessionId,
        string memberId,
        string status,
        bool canceled = false) => context.ClassRegistrations.Add(new ClassRegistration
        {
            SessionId = sessionId, MemberId = memberId, Status = status,
            RegisteredAt = DateTime.UtcNow, RegisteredVia = "MEMBER", RegisteredBy = memberId,
            CanceledAt = canceled ? DateTime.UtcNow : null
        });

    private static HomeworkAssignment Assignment(
        string coachId,
        string memberId,
        string title,
        DateTime at) => new()
        {
            CoachId = coachId, MemberId = memberId, Title = title,
            Instructions = "Nội dung", SourceType = "DIRECT", AssignedAtUtc = at
        };

    private static void SeedRelationalDirectTarget(SportsCenterManagementContext context)
    {
        Role coachRole = new() { Id = 1, Name = "Coach" };
        Role memberRole = new() { Id = 2, Name = "Member" };
        Account coachAccount = new()
        {
            Id = "coach-1", Email = "coach@test.local", PasswordHash = "x", Status = "Active",
            Role = coachRole, CreatedAt = DateTime.UtcNow
        };
        Account memberAccount = new()
        {
            Id = "member-1", Email = "member@test.local", PasswordHash = "x", Status = "Active",
            Role = memberRole, CreatedAt = DateTime.UtcNow
        };
        context.AddRange(coachRole, memberRole, coachAccount, memberAccount);
        context.Coaches.Add(new Coach
        {
            AccountId = coachAccount.Id, Account = coachAccount,
            FullName = "HLV Một", CreatedAt = DateTime.UtcNow
        });
        context.Members.Add(new Member
        {
            AccountId = memberAccount.Id, Account = memberAccount,
            MemberCode = "M1", FullName = "Học viên Một", CreatedAt = DateTime.UtcNow
        });
        context.TrainingPlans.Add(Plan(coachAccount.Id, memberAccount.Id));
    }
}
