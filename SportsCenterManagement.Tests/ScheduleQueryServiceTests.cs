using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ScheduleQueryService;
using APIViewModel;

namespace SportsCenterManagement.Tests;

public class ScheduleQueryServiceTests
{
    [Fact]
    public async Task CoachSchedule_OnlyReturnsAssignedSessions()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.ClassSessions.AddRange(CreateSession(1, "coach-1"), CreateSession(2, "coach-2"));
        await context.SaveChangesAsync();
        ScheduleQueryService service = new(context);

        PagedResult<ClassSessionViewDto> result = await service.GetCoachTeachingScheduleAsync(
            "coach-1", null, null, null, 1, 20);

        Assert.Equal(1, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Roster_RejectsCoachAssignedToAnotherSession()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.ClassSessions.Add(CreateSession(1, "coach-1"));
        await context.SaveChangesAsync();
        ScheduleQueryService service = new(context);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.GetSessionRosterAsync(1, "coach-2", false, false, 1, 20));

        Assert.Equal("FORBIDDEN", exception.Code);
    }

    [Fact]
    public async Task MemberSchedule_OnlyReturnsOwnRegistrationsIncludingCanceled()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.ClassSessions.Add(CreateSession(1, "coach-1"));
        context.ClassRegistrations.AddRange(
            new ClassRegistration
            {
                Id = 1, SessionId = 1, MemberId = "member-1", Status = "CANCELED",
                RegisteredVia = "MEMBER", RegisteredBy = "member-1"
            },
            new ClassRegistration
            {
                Id = 2, SessionId = 1, MemberId = "member-2", Status = "BOOKED",
                RegisteredVia = "MEMBER", RegisteredBy = "member-2"
            });
        await context.SaveChangesAsync();
        ScheduleQueryService service = new(context);

        List<MemberBookedSessionDto> result = await service.GetMyClassScheduleAsync(
            "member-1", null, null);

        MemberBookedSessionDto registration = Assert.Single(result);
        Assert.Equal(1, registration.RegistrationId);
        Assert.Equal("CANCELED", registration.RegistrationStatus);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static ClassSession CreateSession(int id, string coachId)
    {
        return new ClassSession
        {
            Id = id, ClassId = 1, CoachId = coachId, RoomId = id,
            SessionDate = new DateTime(2030, 1, 1), StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10), Capacity = 10, Status = "SCHEDULED",
            Room = new Room { Id = id, Name = $"Room {id}", Capacity = 10, IsActive = true }
        };
    }
}
