using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassSession;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.ClassSessionService;
using Services.NotificationService;
using Services.ScheduleQueryService;
using Xunit;

namespace SportsCenterManagement.Tests;

public partial class ClassSessionServiceTests
{
    [Fact]
    public async Task CreateSessionAsync_OverlappingCoachTime_ThrowsCoachScheduleConflict()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(5).Date;
        ClassSession existingSession = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Capacity = 10,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(existingSession);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        CreateClassSessionAPIViewModel request = new CreateClassSessionAPIViewModel
        {
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 2, // different room, same coach
            Date = futureDate.ToString("yyyy-MM-dd"),
            StartTime = "09:30",
            EndTime = "10:30",
            Capacity = 10
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateSessionAsync("manager-1", request));

        Assert.Equal("COACH_SCHEDULE_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task CreateSessionAsync_AdjacentSessionAtBoundary_SucceedsWithoutConflict()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(5).Date;
        ClassSession existingSession = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Capacity = 10,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(existingSession);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        CreateClassSessionAPIViewModel request = new CreateClassSessionAPIViewModel
        {
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            Date = futureDate.ToString("yyyy-MM-dd"),
            StartTime = "10:00", // Exactly adjacent at 10:00
            EndTime = "11:00",
            Capacity = 10
        };

        ClassSessionViewDto result = await service.CreateSessionAsync("manager-1", request);

        Assert.NotNull(result);
        Assert.Equal("10:00", result.StartTime);
        Assert.Equal("11:00", result.EndTime);
    }

    [Fact]
    public async Task CreateSessionAsync_OverlappingRoomTime_ThrowsRoomScheduleConflict()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(5).Date;
        ClassSession existingSession = new ClassSession
        {
            Id = 1,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 30, 0),
            Capacity = 10,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(existingSession);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        CreateClassSessionAPIViewModel request = new CreateClassSessionAPIViewModel
        {
            ClassId = 1,
            CoachId = "coach-2", // different coach, same room
            RoomId = 1,
            Date = futureDate.ToString("yyyy-MM-dd"),
            StartTime = "15:00",
            EndTime = "16:00",
            Capacity = 10
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateSessionAsync("manager-1", request));

        Assert.Equal("ROOM_SCHEDULE_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task UpdateSessionCoachAsync_ValidChange_UpdatesCoachAndNotifies()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(6).Date;
        ClassSession session = new ClassSession
        {
            Id = 5,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            Capacity = 10,
            BookedCount = 1,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);

        ClassRegistration registration = new ClassRegistration
        {
            Id = 51,
            SessionId = 5,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(registration);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        ClassSessionViewDto result = await service.UpdateSessionCoachAsync("manager-1", 5, "coach-2");

        Assert.Equal("coach-2", result.CoachId);

        List<Notification> notifications = await context.Notifications.ToListAsync();
        Assert.True(notifications.Count >= 3); // old coach + new coach + booked member
    }

    [Fact]
    public async Task CancelSessionAsync_CancelsSessionAndActiveRegistrations()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(7).Date;
        ClassSession session = new ClassSession
        {
            Id = 6,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Capacity = 10,
            BookedCount = 1,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);

        ClassRegistration registration = new ClassRegistration
        {
            Id = 61,
            SessionId = 6,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(registration);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        ClassSessionViewDto result = await service.CancelSessionAsync("manager-1", 6, "Coach unwell");

        Assert.Equal("CANCELED", result.Status);

        ClassRegistration updatedRegistration = await context.ClassRegistrations.FirstAsync(r => r.Id == 61);
        Assert.Equal("CANCELED", updatedRegistration.Status);
    }

    [Fact]
    public async Task UpdateSessionAsync_MemberRegisteredInAnotherSessionAtSameTime_ThrowsMemberScheduleConflictAndRollsBack()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(5).Date;

        Role memberRole = new Role { Id = 3, Name = "Member" };
        context.Roles.Add(memberRole);

        Account memberAcc = new Account
        {
            Id = "member-1",
            Email = "m1@test.com",
            PasswordHash = "x",
            Status = "Active",
            RoleId = 3,
            CreatedAt = DateTime.UtcNow
        };
        context.Accounts.Add(memberAcc);

        Member member = new Member
        {
            AccountId = "member-1",
            FullName = "Member One",
            MemberCode = "MEM001",
            CreatedAt = DateTime.UtcNow
        };
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
            StartDate = DateOnly.FromDateTime(futureDate.AddDays(-5)),
            EndDate = DateOnly.FromDateTime(futureDate.AddDays(15)),
            Kind = "NEW",
            Status = "CONFIRMED",
            IsSuspended = false,
            CreatedAt = DateTime.UtcNow
        };
        context.MemberSubscriptions.Add(subscription);

        ClassSession session1 = new ClassSession
        {
            Id = 10,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Capacity = 10,
            BookedCount = 1,
            Status = "SCHEDULED"
        };

        ClassSession session2 = new ClassSession
        {
            Id = 20,
            ClassId = 1,
            CoachId = "coach-2",
            RoomId = 2,
            SessionDate = futureDate,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            Capacity = 10,
            BookedCount = 1,
            Status = "SCHEDULED"
        };
        context.ClassSessions.AddRange(session1, session2);

        ClassRegistration reg1 = new ClassRegistration
        {
            Id = 101,
            SessionId = 10,
            MemberId = "member-1",
            MemberSubscriptionId = 1,
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        ClassRegistration reg2 = new ClassRegistration
        {
            Id = 201,
            SessionId = 20,
            MemberId = "member-1",
            MemberSubscriptionId = 1,
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.AddRange(reg1, reg2);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        UpdateClassSessionAPIViewModel request = new UpdateClassSessionAPIViewModel
        {
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            Date = futureDate.ToString("yyyy-MM-dd"),
            StartTime = "14:00",
            EndTime = "15:00",
            Capacity = 10
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateSessionAsync("manager-1", 10, request));

        Assert.Equal("MEMBER_SCHEDULE_CONFLICT", exception.Code);

        ClassSession reloaded1 = await context.ClassSessions.FirstAsync(s => s.Id == 10);
        Assert.Equal(new TimeSpan(9, 0, 0), reloaded1.StartTime);
        Assert.Equal(new TimeSpan(10, 0, 0), reloaded1.EndTime);
        Assert.Equal(futureDate, reloaded1.SessionDate);
        Assert.Equal(1, reloaded1.BookedCount);
        Assert.Equal("SCHEDULED", reloaded1.Status);
        Assert.Equal(1, reloaded1.RoomId);
        Assert.Equal("coach-1", reloaded1.CoachId);

        ClassSession reloaded2 = await context.ClassSessions.FirstAsync(s => s.Id == 20);
        Assert.Equal(new TimeSpan(14, 0, 0), reloaded2.StartTime);
        Assert.Equal(new TimeSpan(15, 0, 0), reloaded2.EndTime);
        Assert.Equal(1, reloaded2.BookedCount);

        ClassRegistration reloadedReg1 = await context.ClassRegistrations.FirstAsync(r => r.Id == 101);
        Assert.Equal("BOOKED", reloadedReg1.Status);

        ClassRegistration reloadedReg2 = await context.ClassRegistrations.FirstAsync(r => r.Id == 201);
        Assert.Equal("BOOKED", reloadedReg2.Status);

        List<Notification> notifications = await context.Notifications.ToListAsync();
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task GetCoachScheduleAsync_OnlyReturnsAssignedSessionsForCallingCoach()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        DateTime futureDate = DateTime.UtcNow.AddDays(2).Date;

        ClassSession sessionCoach1 = new ClassSession
        {
            Id = 301,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Capacity = 10,
            BookedCount = 0,
            Status = "SCHEDULED"
        };
        ClassSession sessionCoach2 = new ClassSession
        {
            Id = 302,
            ClassId = 1,
            CoachId = "coach-2",
            RoomId = 2,
            SessionDate = futureDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Capacity = 10,
            BookedCount = 0,
            Status = "SCHEDULED"
        };
        context.ClassSessions.AddRange(sessionCoach1, sessionCoach2);
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        PagedResult<ClassSessionViewDto> coach1Schedule = await service.GetCoachScheduleAsync(
            "coach-1", futureDate.AddDays(-1), futureDate.AddDays(1), null, 1, 10);

        Assert.Equal(1, coach1Schedule.TotalItems);
        Assert.Equal(301, Assert.Single(coach1Schedule.Items).Id);

        PagedResult<ClassSessionViewDto> coach2Schedule = await service.GetCoachScheduleAsync(
            "coach-2", futureDate.AddDays(-1), futureDate.AddDays(1), null, 1, 10);

        Assert.Equal(1, coach2Schedule.TotalItems);
        Assert.Equal(302, Assert.Single(coach2Schedule.Items).Id);
    }

    [Fact]
    public async Task CreateSessionAsync_CoachDoesNotTeachClassDiscipline_ThrowsCoachDisciplineMismatch()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        context.Disciplines.Add(new Discipline { Id = 2, Name = "Boxing", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.CoachDisciplines.Add(new CoachDiscipline { CoachAccountId = "coach-2", DisciplineId = 2, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        DateTime futureDate = DateTime.UtcNow.AddDays(5).Date;
        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        CreateClassSessionAPIViewModel request = new CreateClassSessionAPIViewModel
        {
            ClassId = 1,
            CoachId = "coach-2",
            RoomId = 1,
            Date = futureDate.ToString("yyyy-MM-dd"),
            StartTime = "08:00",
            EndTime = "09:00",
            Capacity = 10
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateSessionAsync("manager-1", request));

        Assert.Equal("COACH_DISCIPLINE_MISMATCH", exception.Code);
    }

    [Fact]
    public async Task UpdateSessionAsync_CoachDoesNotTeachClassDiscipline_ThrowsCoachDisciplineMismatch()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        context.Disciplines.Add(new Discipline { Id = 2, Name = "Boxing", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.CoachDisciplines.Add(new CoachDiscipline { CoachAccountId = "coach-2", DisciplineId = 2, CreatedAt = DateTime.UtcNow });

        DateTime futureDate = DateTime.UtcNow.AddDays(5).Date;
        context.ClassSessions.Add(new ClassSession
        {
            Id = 401,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            Capacity = 10,
            Status = "SCHEDULED"
        });
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        UpdateClassSessionAPIViewModel request = new UpdateClassSessionAPIViewModel
        {
            ClassId = 1,
            CoachId = "coach-2",
            RoomId = 1,
            Date = futureDate.ToString("yyyy-MM-dd"),
            StartTime = "08:00",
            EndTime = "09:00",
            Capacity = 10
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateSessionAsync("manager-1", 401, request));

        Assert.Equal("COACH_DISCIPLINE_MISMATCH", exception.Code);
    }

    [Fact]
    public async Task UpdateSessionCoachAsync_CoachDoesNotTeachClassDiscipline_ThrowsCoachDisciplineMismatch()
    {
        await using SportsCenterManagementContext context = CreateContext();
        await SeedDependenciesAsync(context);

        context.Disciplines.Add(new Discipline { Id = 2, Name = "Boxing", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.CoachDisciplines.Add(new CoachDiscipline { CoachAccountId = "coach-2", DisciplineId = 2, CreatedAt = DateTime.UtcNow });

        DateTime futureDate = DateTime.UtcNow.AddDays(6).Date;
        context.ClassSessions.Add(new ClassSession
        {
            Id = 402,
            ClassId = 1,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            Capacity = 10,
            Status = "SCHEDULED"
        });
        await context.SaveChangesAsync();

        ClassSessionService service = new ClassSessionService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateSessionCoachAsync("manager-1", 402, "coach-2"));

        Assert.Equal("COACH_DISCIPLINE_MISMATCH", exception.Code);
    }

    private static async Task SeedDependenciesAsync(SportsCenterManagementContext context)
    {
        context.Disciplines.Add(new Discipline { Id = 1, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Classes.Add(new Class { Id = 1, DisciplineId = 1, Name = "Yoga 101", Capacity = 20, Status = "OPEN", CreatedAt = DateTime.UtcNow });
        context.Rooms.Add(new Room { Id = 1, Name = "Studio 1", Capacity = 25, IsActive = true, CreatedAt = DateTime.UtcNow });
        context.Rooms.Add(new Room { Id = 2, Name = "Studio 2", Capacity = 25, IsActive = true, CreatedAt = DateTime.UtcNow });

        Role coachRole = new Role { Id = 2, Name = "Coach" };
        context.Roles.Add(coachRole);

        Account coachAcc1 = new Account { Id = "coach-1", Email = "c1@test.com", PasswordHash = "x", Status = "Active", RoleId = 2, CreatedAt = DateTime.UtcNow };
        Account coachAcc2 = new Account { Id = "coach-2", Email = "c2@test.com", PasswordHash = "x", Status = "Active", RoleId = 2, CreatedAt = DateTime.UtcNow };
        context.Accounts.AddRange(coachAcc1, coachAcc2);

        Coach coach1 = new Coach { AccountId = "coach-1", FullName = "Coach One", CreatedAt = DateTime.UtcNow };
        Coach coach2 = new Coach { AccountId = "coach-2", FullName = "Coach Two", CreatedAt = DateTime.UtcNow };
        context.Coaches.AddRange(coach1, coach2);

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
