using System;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Room;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.RoomService;
using Services.ScheduleQueryService;
using Xunit;

namespace SportsCenterManagement.Tests;

public class RoomServiceTests
{
    [Fact]
    public async Task CreateRoomAsync_CreatesRoomAndWritesAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        RoomService service = new RoomService(context, new AuditLogService(context));

        CreateRoomAPIViewModel request = new CreateRoomAPIViewModel
        {
            Name = "  Yoga Studio A  ",
            Location = "Floor 2",
            Capacity = 25,
            RoomType = "CLASSROOM"
        };

        RoomViewModel result = await service.CreateRoomAsync("manager-1", request);

        Assert.Equal("Yoga Studio A", result.Name);
        Assert.Equal(25, result.Capacity);
        Assert.True(result.IsActive);

        AuditLog auditLog = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal("ROOM_CREATED", auditLog.Action);
    }

    [Fact]
    public async Task CreateRoomAsync_DuplicateName_ThrowsBusinessRuleException()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Rooms.Add(new Room
        {
            Id = 1,
            Name = "Studio A",
            NormalizedName = Services.ClassScheduling.Flow2Name.Normalize("Studio A"),
            Capacity = 20,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        RoomService service = new RoomService(context, new AuditLogService(context));
        CreateRoomAPIViewModel request = new CreateRoomAPIViewModel
        {
            Name = "  studio a  ",
            Capacity = 30
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateRoomAsync("manager-1", request));

        Assert.Equal("ROOM_NAME_DUPLICATE", exception.Code);
    }

    [Fact]
    public async Task UpdateRoomAsync_CapacityBelowScheduledSession_ThrowsBusinessRuleException()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Room room = new Room
        {
            Id = 1,
            Name = "Studio B",
            Capacity = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Rooms.Add(room);

        DateTime futureDate = DateTime.UtcNow.AddDays(2).Date;
        ClassSession session = new ClassSession
        {
            Id = 10,
            ClassId = 1,
            RoomId = 1,
            CoachId = "coach-1",
            SessionDate = futureDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Capacity = 20,
            BookedCount = 5,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);
        await context.SaveChangesAsync();

        RoomService service = new RoomService(context, new AuditLogService(context));
        UpdateRoomAPIViewModel request = new UpdateRoomAPIViewModel
        {
            Name = "Studio B",
            Capacity = 15 // less than session capacity 20
        };

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateRoomAsync("manager-1", 1, request));

        Assert.Equal("ROOM_CAPACITY_BELOW_SCHEDULE", exception.Code);
    }

    [Fact]
    public async Task UpdateRoomStatusAsync_DeactivatingRoom_PreservesUpcomingSessions()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Room room = new Room
        {
            Id = 2,
            Name = "Studio C",
            Capacity = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Rooms.Add(room);

        DateTime futureDate = DateTime.UtcNow.AddDays(2).Date;
        ClassSession session = new ClassSession
        {
            Id = 11,
            ClassId = 1,
            RoomId = 2,
            CoachId = "coach-1",
            SessionDate = futureDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Capacity = 20,
            BookedCount = 0,
            Status = "SCHEDULED"
        };
        context.ClassSessions.Add(session);
        await context.SaveChangesAsync();

        RoomService service = new RoomService(context, new AuditLogService(context));

        await service.UpdateRoomStatusAsync("manager-1", 2, false);
        Assert.False((await context.Rooms.SingleAsync(item => item.Id == 2)).IsActive);
        Assert.Equal("SCHEDULED", (await context.ClassSessions.SingleAsync()).Status);
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
