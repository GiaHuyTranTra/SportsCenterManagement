using APIViewModel;
using APIViewModel.ClassRegistration;
using APIViewModel.ClassSession;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.ClassRegistrationService;
using Services.ClassScheduling;
using Services.ClassSessionService;
using Services.NotificationService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public class Flow2NameTests
{
    [Theory]
    [InlineData("  Bơi   lội ", "BOI LOI")]
    [InlineData("ĐẤU VẬT", "DAU VAT")]
    [InlineData("Ỹ\tỶ\nỴ", "Y Y Y")]
    [InlineData("Bo\u031bi lo\u0323\u0302i", "BOI LOI")]
    public void Normalize_UsesOneAccentAndWhitespacePolicy(string source, string expected) =>
        Assert.Equal(expected, Flow2Name.Normalize(source));
}

public partial class ClassRegistrationServiceTests
{
    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("PENDING_PAYMENT")]
    [InlineData("CANCELED")]
    public async Task Register_RejectsEveryNonConfirmedSubscription(string status)
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedValidMemberAndSessionAsync(context, DateTime.UtcNow.AddDays(3).Date, 10, 0);
        MemberSubscription subscription = await context.MemberSubscriptions.SingleAsync();
        subscription.Status = status;
        await context.SaveChangesAsync();
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RegistrationService(context).RegisterAsync("member-1", 1, "MEMBER", "member-1"));
        Assert.Equal("MEMBERSHIP_NOT_VALID_FOR_SESSION", error.Code);
        Assert.Empty(await context.ClassRegistrations.ToListAsync());
    }

    [Fact]
    public async Task ConfirmedBooking_StoresSubscription_AndHistoryIsPaged()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedValidMemberAndSessionAsync(context, DateTime.UtcNow.AddDays(3).Date, 10, 0);
        ClassRegistrationService service = RegistrationService(context);
        ClassRegistrationViewModel first = await service.RegisterAsync("member-1", 1, "MEMBER", "member-1");
        Assert.Equal(1, (await context.ClassRegistrations.SingleAsync()).MemberSubscriptionId);
        Assert.Equal(1, first.Session.BookedCount);
        await service.CancelRegistrationAsync(first.Id, null, "member-1", false);
        await service.RegisterAsync("member-1", 1, "MEMBER", "member-1");
        PagedResult<ClassRegistrationViewModel> history = await service.GetMyRegistrationsAsync("member-1", true, 2, 1);
        Assert.Equal(2, history.TotalItems);
        Assert.Equal("CANCELED", Assert.Single(history.Items).Status);
        Assert.Empty((await service.GetMyRegistrationsAsync("another-member", true)).Items);
    }

    [Fact]
    public async Task RoomOnlyUpdate_NotifiesBothMemberAndCoach_AndRetryDoesNotDuplicate()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime date = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, date, 10, 0);
        await RegistrationService(context).RegisterAsync("member-1", 1, "MEMBER", "member-1");
        context.Rooms.Add(new Room { Id = 2, Name = "Second Room", Capacity = 10, IsActive = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        ClassSessionService service = SessionService(context);
        UpdateClassSessionAPIViewModel request = new UpdateClassSessionAPIViewModel
        {
            ClassId = 1, CoachId = "coach-1", RoomId = 2, Date = date.ToString("yyyy-MM-dd"),
            StartTime = "09:00", EndTime = "10:00", Capacity = 10
        };
        await service.UpdateSessionAsync("coach-1", 1, request);
        await service.UpdateSessionAsync("coach-1", 1, request);
        List<Notification> notifications = await context.Notifications.Where(item => item.Type == "SESSION_RESCHEDULED").ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, item => item.RecipientId == "member-1");
        Assert.Contains(notifications, item => item.RecipientId == "coach-1");
        Assert.Single(notifications.Select(item => item.EventId).Distinct());
    }

    [Fact]
    public async Task CoachOnlyUpdates_DifferentChangesAtSameTime_ProduceDifferentEvents()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime date = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, date, 10, 0);
        await RegistrationService(context).RegisterAsync("member-1", 1, "MEMBER", "member-1");
        context.Accounts.Add(new Account { Id = "coach-2", Email = "coach2@example.com", RoleId = 2,
            PasswordHash = Guid.NewGuid().ToString("N"), Status = "Active" });
        context.Coaches.Add(new Coach { AccountId = "coach-2", FullName = "Coach Two" });
        await context.SaveChangesAsync();
        ClassSessionService service = SessionService(context);
        UpdateClassSessionAPIViewModel request = new UpdateClassSessionAPIViewModel
        {
            ClassId = 1, CoachId = "coach-2", RoomId = 1, Date = date.ToString("yyyy-MM-dd"),
            StartTime = "09:00", EndTime = "10:00", Capacity = 10
        };
        await service.UpdateSessionAsync("coach-1", 1, request);
        request.CoachId = "coach-1";
        await service.UpdateSessionAsync("coach-1", 1, request);
        List<Notification> notifications = await context.Notifications.Where(item => item.Type == "SESSION_COACH_CHANGED" && item.RecipientId == "member-1").ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(2, notifications.Select(item => item.EventId).Distinct().Count());
    }

    [Fact]
    public async Task ClassChangeWithoutTimeChange_RechecksPackageEntitlement()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime date = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, date, 10, 0);
        await RegistrationService(context).RegisterAsync("member-1", 1, "MEMBER", "member-1");
        context.Disciplines.Add(new Discipline { Id = 2, Name = "Swimming", IsActive = true });
        context.Classes.Add(new Class { Id = 2, DisciplineId = 2, Name = "Swimming Class", Status = "OPEN", Capacity = 10 });
        context.MembershipPackageSports.Add(new MembershipPackageSport { PackageId = 1, DisciplineId = 1 });
        await context.SaveChangesAsync();
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SessionService(context).UpdateSessionAsync("coach-1", 1, new UpdateClassSessionAPIViewModel
            {
                ClassId = 2, CoachId = "coach-1", RoomId = 1, Date = date.ToString("yyyy-MM-dd"),
                StartTime = "09:00", EndTime = "10:00", Capacity = 10
            }));
        Assert.Equal("PACKAGE_NOT_ELIGIBLE", error.Code);
        Assert.Equal(1, (await context.ClassSessions.SingleAsync()).ClassId);
    }

    [Fact]
    public async Task Notification_IsStagedAndDeduplicatedWithinOperation()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedValidMemberAndSessionAsync(context, DateTime.UtcNow.AddDays(3).Date, 10, 0);
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(context);
        NotificationService notifications = new NotificationService(context);
        await notifications.SendNotificationAsync("member-1", "SESSION_RESCHEDULED", "Change", "Details", "ClassSession", "1", DateTime.UtcNow);
        await notifications.SendNotificationAsync("member-1", "SESSION_RESCHEDULED", "Change", "Details", "ClassSession", "1", DateTime.UtcNow);
        Assert.Empty(await context.Notifications.ToListAsync());
        Assert.Single(context.Notifications.Local);
        await context.SaveChangesAsync();
        await operation.CommitAsync();
        Assert.Single(await context.Notifications.ToListAsync());
    }

    private static ClassRegistrationService RegistrationService(SportsCenterManagementContext context) =>
        new ClassRegistrationService(context, new AuditLogService(context), new NotificationService(context));
    private static ClassSessionService SessionService(SportsCenterManagementContext context) =>
        new ClassSessionService(context, new AuditLogService(context), new NotificationService(context));
}
