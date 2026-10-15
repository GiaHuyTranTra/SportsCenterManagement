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
    public async Task CreateClassAsync_ValidRequest_CreatesClassAndWritesAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Disciplines.Add(new Discipline
        {
            Id = 1,
            Name = "Yoga",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        ClassService service = new ClassService(
            context,
            new AuditLogService(context),
            new NotificationService(context));

        CreateClassAPIViewModel request = new CreateClassAPIViewModel
        {
            SportId = 1,
            Name = "Morning Vinyasa",
            Description = "Flow yoga",
            Capacity = 20,
            Status = "DRAFT"
        };

        ClassViewModel result = await service.CreateClassAsync("manager-1", request);

        Assert.Equal("Morning Vinyasa", result.Name);
        Assert.Equal(20, result.Capacity);
        Assert.Equal("DRAFT", result.Status);

        AuditLog auditLog = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal("CLASS_CREATED", auditLog.Action);
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
