using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewModel.ClassRegistration;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.ClassRegistrationService;
using Services.NotificationService;
using Services.ScheduleQueryService;
using Xunit;

namespace SportsCenterManagement.Tests;

public partial class ClassRegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_ValidConditions_SucceedsAndIncrementsBookedCount()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 0);

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        ClassRegistrationViewModel result = await service.RegisterAsync("member-1", 1, "MEMBER", "member-1");

        Assert.Equal("BOOKED", result.Status);
        Assert.Equal("member-1", result.MemberAccountId);

        ClassSession session = await context.ClassSessions.FirstAsync(s => s.Id == 1);
        Assert.Equal(1, session.BookedCount);

        List<Notification> notifications = await context.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal("CLASS_REGISTERED", notifications[0].Type);

        AuditLog auditLog = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal("CLASS_REGISTERED", auditLog.Action);
    }

    [Fact]
    public async Task RegisterAsync_ClassFull_ThrowsClassFullException()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 5, bookedCount: 5);

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegisterAsync("member-1", 1, "MEMBER", "member-1"));

        Assert.Equal("CLASS_FULL", exception.Code);
    }

    [Fact]
    public async Task RegisterAsync_MemberScheduleConflict_ThrowsMemberScheduleConflict()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 0);

        ClassSession conflictingSession = new ClassSession
        {
            Id = 2,
            ClassId = 1,
            RoomId = 1,
            CoachId = "coach-1",
            SessionDate = futureDate,
            StartTime = new TimeSpan(9, 30, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Capacity = 10,
            BookedCount = 1,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(conflictingSession);

        ClassRegistration existingBooking = new ClassRegistration
        {
            Id = 20,
            SessionId = 2,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(existingBooking);
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegisterAsync("member-1", 1, "MEMBER", "member-1"));

        Assert.Equal("MEMBER_SCHEDULE_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateBooking_ThrowsDuplicateRegistration()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 1);

        ClassRegistration existingBooking = new ClassRegistration
        {
            Id = 10,
            SessionId = 1,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(existingBooking);
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegisterAsync("member-1", 1, "MEMBER", "member-1"));

        Assert.Equal("DUPLICATE_REGISTRATION", exception.Code);
    }

    [Fact]
    public async Task RegisterAsync_NoValidSubscriptionForDate_ThrowsMembershipNotValid()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(30).Date; // past subscription date
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 0);

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegisterAsync("member-1", 1, "MEMBER", "member-1"));

        Assert.Equal("MEMBERSHIP_NOT_VALID_FOR_SESSION", exception.Code);
    }

    [Fact]
    public async Task RegisterAsync_IneligibleSportInPackage_ThrowsPackageNotEligible()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 0);

        // Add restriction for package 1 that only allows discipline 99 (not 1)
        context.MembershipPackageSports.Add(new MembershipPackageSport
        {
            PackageId = 1,
            DisciplineId = 99
        });
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegisterAsync("member-1", 1, "MEMBER", "member-1"));

        Assert.Equal("PACKAGE_NOT_ELIGIBLE", exception.Code);
    }

    [Fact]
    public async Task CancelRegistrationAsync_MemberCancelsOwnBooking_SucceedsAndDecrementsBookedCount()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 1);

        ClassRegistration booking = new ClassRegistration
        {
            Id = 50,
            SessionId = 1,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(booking);
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        ClassRegistrationViewModel result = await service.CancelRegistrationAsync(
            50,
            "Busy schedule",
            "member-1",
            false);

        Assert.Equal("CANCELED", result.Status);

        ClassSession session = await context.ClassSessions.FirstAsync(s => s.Id == 1);
        Assert.Equal(0, session.BookedCount);
    }

    [Fact]
    public async Task CancelRegistrationAsync_AlreadyCanceled_ThrowsRegistrationAlreadyCanceled()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 0);

        ClassRegistration booking = new ClassRegistration
        {
            Id = 51,
            SessionId = 1,
            MemberId = "member-1",
            Status = "CANCELED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1",
            CanceledAt = DateTime.UtcNow
        };
        context.ClassRegistrations.Add(booking);
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CancelRegistrationAsync(51, "Changed mind", "member-1", false));

        Assert.Equal("REGISTRATION_ALREADY_CANCELED", exception.Code);
    }

    [Fact]
    public async Task CancelRegistrationAsync_NotOwner_ThrowsForbidden()
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        await SeedValidMemberAndSessionAsync(context, futureDate, capacity: 10, bookedCount: 1);

        ClassRegistration booking = new ClassRegistration
        {
            Id = 52,
            SessionId = 1,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(booking);
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CancelRegistrationAsync(52, "Reason", "other-member", false));

        Assert.Equal("FORBIDDEN", exception.Code);
    }

    private static async Task SeedValidMemberAndSessionAsync(
        SportsCenterManagementContext context,
        DateTime sessionDate,
        int capacity,
        int bookedCount)
    {
        Discipline discipline = new Discipline { Id = 1, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Disciplines.Add(discipline);

        Class testClass = new Class
        {
            Id = 1,
            DisciplineId = 1,
            Name = "Vinyasa 1",
            Capacity = 20,
            Status = "OPEN",
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(testClass);

        Room room = new Room { Id = 1, Name = "Studio 1", Capacity = 20, IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Rooms.Add(room);

        Role coachRole = new Role { Id = 2, Name = "Coach" };
        Role memberRole = new Role { Id = 3, Name = "Member" };
        context.Roles.AddRange(coachRole, memberRole);

        Account coachAcc = new Account { Id = "coach-1", Email = "c@test.com", PasswordHash = "x", Status = "Active", RoleId = 2, CreatedAt = DateTime.UtcNow };
        Account memberAcc = new Account { Id = "member-1", Email = "m@test.com", PasswordHash = "x", Status = "Active", RoleId = 3, CreatedAt = DateTime.UtcNow };
        context.Accounts.AddRange(coachAcc, memberAcc);

        Coach coach = new Coach { AccountId = "coach-1", FullName = "Coach One", CreatedAt = DateTime.UtcNow };
        Member member = new Member { AccountId = "member-1", FullName = "Member One", MemberCode = "MEM001", CreatedAt = DateTime.UtcNow };
        context.Coaches.Add(coach);
        context.Members.Add(member);

        MembershipPackage package = new MembershipPackage
        {
            Id = 1,
            Name = "All Access",
            Price = 500000,
            DurationMonths = 1,
            Benefits = "All access",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.MembershipPackages.Add(package);

        MemberSubscription subscription = new MemberSubscription
        {
            Id = 1,
            MemberId = "member-1",
            PackageId = 1,
            PackageName = "All Access",
            PackagePrice = 500000,
            DurationMonths = 1,
            Benefits = "All access",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            Kind = "NEW",
            Status = "CONFIRMED",
            IsSuspended = false,
            CreatedAt = DateTime.UtcNow
        };
        context.MemberSubscriptions.Add(subscription);

        ClassSession session = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = sessionDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Capacity = capacity,
            BookedCount = bookedCount,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);

        await context.SaveChangesAsync();
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }
}
