using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Class;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.ClassService;
using Services.NotificationService;
using Services.ScheduleQueryService;
using Xunit;

namespace SportsCenterManagement.Tests;

public class ClassServiceTests
{
    [Fact]
    public async Task CreateClassAsync_ValidRequest_CreatesClassWithSessionsAndWritesAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        SeedScheduleReferences(context);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        // Lớp mới luôn đi kèm lịch: giáo viên đứng lớp, phòng, khoảng ngày, các
        // thứ trong tuần và khung giờ.
        CreateClassAPIViewModel request = new CreateClassAPIViewModel
        {
            SportId = 1,
            Name = "Morning Vinyasa",
            Description = "Flow yoga",
            Capacity = 20,
            Status = "OPEN",
            Schedule = ScheduleFor()
        };

        ClassViewModel result = await service.CreateClassAsync("manager-1", request);

        Assert.Equal("Morning Vinyasa", result.Name);
        Assert.Equal(20, result.Capacity);
        // Lớp vừa tạo đã có buổi nên mở đăng ký được ngay.
        Assert.Equal("OPEN", result.Status);

        List<ClassSession> sessions = await context.ClassSessions.ToListAsync();
        Assert.Equal(result.TotalSessionCount, sessions.Count);
        Assert.All(sessions, session =>
        {
            Assert.Equal("coach-1", session.CoachId);
            Assert.Equal(1, session.RoomId);
            Assert.Equal("SCHEDULED", session.Status);
            // Chỉ sinh buổi vào các thứ đã chọn (thứ hai và thứ tư).
            Assert.Contains(session.SessionDate.DayOfWeek, new[] { DayOfWeek.Monday, DayOfWeek.Wednesday });
        });

        // Lớp mới chỉ có một đợt lịch, chính đợt vừa nhập.
        ClassScheduleBlockViewModel block = Assert.Single(result.ScheduleBlocks);
        Assert.Equal("coach-1", block.CoachId);
        Assert.Equal(1, block.RoomId);
        Assert.Equal("18:00", block.StartTime);
        Assert.Equal("19:30", block.EndTime);
        Assert.Equal(new List<int> { 1, 3 }, block.DaysOfWeek);
        Assert.Equal(sessions.Count, block.SessionCount);

        AuditLog auditLog = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal("CLASS_CREATED", auditLog.Action);
    }

    [Fact]
    public async Task CreateClassAsync_WithoutSchedule_ThrowsValidationError()
    {
        await using SportsCenterManagementContext context = CreateContext();
        SeedScheduleReferences(context);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        // Thiếu lịch học thì không tạo được lớp, và không để lại lớp rỗng.
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateClassAsync("manager-1", new CreateClassAPIViewModel
            {
                SportId = 1,
                Name = "No Schedule",
                Capacity = 10,
                Status = "DRAFT"
            }));

        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.False(await context.Classes.AnyAsync());
    }

    [Fact]
    public async Task CreateClassAsync_WithoutRoom_ThrowsRoomNotFound()
    {
        await using SportsCenterManagementContext context = CreateContext();
        SeedScheduleReferences(context);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        ClassScheduleAPIViewModel schedule = ScheduleFor();
        schedule.RoomId = 0;

        // Phòng học là bắt buộc: lớp không có phòng thì không lập được lịch.
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateClassAsync("manager-1", new CreateClassAPIViewModel
            {
                SportId = 1,
                Name = "No Room",
                Capacity = 10,
                Status = "DRAFT",
                Schedule = schedule
            }));

        Assert.Equal("ROOM_NOT_FOUND", error.Code);
        Assert.False(await context.Classes.AnyAsync());
    }

    [Fact]
    public async Task CreateClassAsync_CoachOutsideDiscipline_ThrowsCoachNotQualified()
    {
        await using SportsCenterManagementContext context = CreateContext();
        SeedScheduleReferences(context);
        context.Disciplines.Add(new Discipline { Id = 2, Name = "Boxing", IsActive = true, CreatedAt = DateTime.UtcNow });
        // Hồ sơ đã khai chuyên môn Yoga, nên không nhận lớp Boxing.
        context.CoachDisciplines.Add(new CoachDiscipline
        {
            CoachAccountId = "coach-1",
            DisciplineId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateClassAsync("manager-1", new CreateClassAPIViewModel
            {
                SportId = 2,
                Name = "Boxing Basics",
                Capacity = 10,
                Status = "DRAFT",
                Schedule = ScheduleFor()
            }));

        Assert.Equal("COACH_NOT_QUALIFIED", error.Code);
        Assert.False(await context.Classes.AnyAsync());
    }

    /// <summary>Bộ môn, giáo viên và phòng tối thiểu để lập được một lịch học.</summary>
    private static void SeedScheduleReferences(SportsCenterManagementContext context)
    {
        context.Disciplines.Add(new Discipline
        {
            Id = 1,
            Name = "Yoga",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Accounts.Add(new Account
        {
            Id = "coach-1",
            Email = "coach-1@example.com",
            PasswordHash = "x",
            RoleId = 2,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        context.Coaches.Add(new Coach
        {
            AccountId = "coach-1",
            FullName = "Coach One",
            CreatedAt = DateTime.UtcNow
        });
        context.Rooms.Add(new Room
        {
            Id = 1,
            Name = "Studio A",
            Capacity = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Lịch bốn tuần vào thứ hai và thứ tư, ca tối 18:00–19:30.</summary>
    private static ClassScheduleAPIViewModel ScheduleFor()
    {
        DateTime start = DateTime.UtcNow.AddHours(7).Date.AddDays(1);
        return new ClassScheduleAPIViewModel
        {
            CoachId = "coach-1",
            RoomId = 1,
            StartDate = start.ToString("yyyy-MM-dd"),
            EndDate = start.AddDays(27).ToString("yyyy-MM-dd"),
            DaysOfWeek = new List<int> { 1, 3 },
            StartTime = "18:00",
            EndTime = "19:30"
        };
    }

    [Fact]
    public async Task UpdateClassStatusAsync_OpenWithoutFutureSessions_ThrowsClassHasNoValidSession()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Disciplines.Add(new Discipline { Id = 1, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow });
        Class entity = new Class
        {
            Id = 1,
            DisciplineId = 1,
            Name = "Test Class",
            Capacity = 10,
            Status = "DRAFT",
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(entity);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateClassStatusAsync("manager-1", 1, "OPEN"));

        Assert.Equal("CLASS_HAS_NO_VALID_SESSION", exception.Code);
    }

    [Fact]
    public async Task UpdateClassStatusAsync_InvalidStatus_ThrowsValidationError()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Disciplines.Add(new Discipline { Id = 1, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow });
        Class entity = new Class
        {
            Id = 1,
            DisciplineId = 1,
            Name = "Active Class",
            Capacity = 10,
            Status = "OPEN",
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(entity);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateClassStatusAsync("manager-1", 1, "UNKNOWN_STATUS"));

        Assert.Equal("VALIDATION_ERROR", exception.Code);
    }

    [Fact]
    public async Task UpdateClassAsync_CapacityBelowActiveRegistrations_ThrowsBusinessRuleException()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Disciplines.Add(new Discipline { Id = 1, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow });
        Class entity = new Class
        {
            Id = 2,
            DisciplineId = 1,
            Name = "Hatha Yoga",
            Capacity = 20,
            Status = "OPEN",
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(entity);

        DateTime futureDate = DateTime.UtcNow.AddDays(3).Date;
        ClassSession session = new ClassSession
        {
            Id = 20,
            ClassId = 2,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            Capacity = 20,
            BookedCount = 15,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        UpdateClassAPIViewModel request = new UpdateClassAPIViewModel
        {
            SportId = 1,
            Name = "Hatha Yoga",
            Capacity = 10 // less than booked count 15
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateClassAsync("manager-1", 2, request));

        Assert.Equal("CAPACITY_BELOW_REGISTRATIONS", exception.Code);
    }

    [Fact]
    public async Task CancelFutureSessionsAsync_CancelsSessionsAndNotifiesBookedMembers()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Disciplines.Add(new Discipline { Id = 1, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow });
        Class entity = new Class
        {
            Id = 3,
            DisciplineId = 1,
            Name = "Pilates Class",
            Capacity = 15,
            Status = "OPEN",
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(entity);

        DateTime futureDate = DateTime.UtcNow.AddDays(4).Date;
        ClassSession session = new ClassSession
        {
            Id = 30,
            ClassId = 3,
            CoachId = "coach-1",
            RoomId = 1,
            SessionDate = futureDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Capacity = 15,
            BookedCount = 1,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);

        ClassRegistration registration = new ClassRegistration
        {
            Id = 301,
            SessionId = 30,
            MemberId = "member-1",
            Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow,
            RegisteredVia = "MEMBER",
            RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(registration);
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        CancelFutureSessionsResultViewModel result = await service.CancelFutureSessionsAsync(
            "manager-1",
            3,
            "Instructor on leave");

        Assert.Equal(1, result.CanceledSessionCount);

        ClassSession updatedSession = await context.ClassSessions.FirstAsync(s => s.Id == 30);
        Assert.Equal("CANCELED", updatedSession.Status);

        ClassRegistration updatedRegistration = await context.ClassRegistrations.FirstAsync(r => r.Id == 301);
        Assert.Equal("CANCELED", updatedRegistration.Status);

        List<Notification> notifications = await context.Notifications.ToListAsync();
        Assert.True(notifications.Count >= 2); // 1 for member, 1 for coach
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
