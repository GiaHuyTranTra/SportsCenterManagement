using DataAccess;
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
    [Theory]
    [InlineData(6, false, false)]
    [InlineData(8, false, true)]
    [InlineData(6, true, true)]
    public async Task CancelRegistrationAsync_EnforcesMemberDeadlineOnly(int hoursUntilStart, bool isStaff, bool canCancel)
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime startsAt = DateTime.UtcNow.AddHours(7 + hoursUntilStart);
        await SeedValidMemberAndSessionAsync(context, startsAt.Date, 10, 1);
        ClassSession session = await context.ClassSessions.SingleAsync();
        session.StartTime = startsAt.TimeOfDay;
        session.EndTime = startsAt.AddHours(1).TimeOfDay;
        ClassRegistration booking = new ClassRegistration
        {
            Id = 70, SessionId = 1, MemberId = "member-1", Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow, RegisteredVia = "MEMBER", RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(booking);
        await context.SaveChangesAsync();
        ClassRegistrationService service = new ClassRegistrationService(
            context, new AuditLogService(context), new NotificationService(context));

        if (canCancel)
        {
            await service.CancelRegistrationAsync(70, "Schedule conflict", "member-1", isStaff);
            Assert.Equal("CANCELED", booking.Status);
            Assert.Equal(0, session.BookedCount);
        }
        else
        {
            BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.CancelRegistrationAsync(70, null, "member-1", isStaff));
            Assert.Equal("REGISTRATION_CANCELLATION_TOO_LATE", exception.Code);
            Assert.Equal("BOOKED", booking.Status);
            Assert.Equal(1, session.BookedCount);
            Assert.Empty(await context.AuditLogs.ToListAsync());
        }
    }

    // Hủy lớp bám giờ khai giảng, không bám buổi kế tiếp: lớp đã mở thì dù còn
    // buổi ở xa trong tương lai cũng không hủy được nữa.
    [Theory]
    [InlineData(10, true)]
    [InlineData(6, false)]
    [InlineData(-24, false)]
    public async Task CancelClassRegistrationAsync_UsesClassOpeningTimeForDeadline(
        int hoursUntilClassOpens,
        bool canCancel)
    {
        await using SportsCenterManagementContext context = CreateContext();
        DateTime bookedSessionDate = DateTime.UtcNow.AddHours(7).AddDays(5).Date;
        await SeedValidMemberAndSessionAsync(context, bookedSessionDate, 10, 1);

        ClassSession bookedSession = await context.ClassSessions.SingleAsync();
        bookedSession.StartTime = new TimeSpan(18, 0, 0);
        bookedSession.EndTime = new TimeSpan(19, 0, 0);

        DateTime opensAt = DateTime.UtcNow.AddHours(7 + hoursUntilClassOpens);
        ClassSession openingSession = new ClassSession
        {
            Id = 2,
            ClassId = 1,
            RoomId = 1,
            CoachId = "coach-1",
            SessionDate = opensAt.Date,
            StartTime = opensAt.TimeOfDay,
            EndTime = opensAt.AddHours(1).TimeOfDay,
            Capacity = 10,
            BookedCount = 0,
            Status = "SCHEDULED",
            CreatedAt = DateTime.UtcNow
        };
        context.ClassSessions.Add(openingSession);

        ClassRegistration booking = new ClassRegistration
        {
            Id = 71, SessionId = 1, MemberId = "member-1", Status = "BOOKED",
            RegisteredAt = DateTime.UtcNow, RegisteredVia = "MEMBER", RegisteredBy = "member-1"
        };
        context.ClassRegistrations.Add(booking);
        await context.SaveChangesAsync();

        ClassRegistrationService service = new ClassRegistrationService(
            context, new AuditLogService(context), new NotificationService(context));

        if (canCancel)
        {
            await service.CancelClassRegistrationAsync("member-1", 1, "Bận việc", "member-1", false);
            Assert.Equal("CANCELED", booking.Status);
        }
        else
        {
            BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.CancelClassRegistrationAsync("member-1", 1, null, "member-1", false));
            Assert.Equal("REGISTRATION_CANCELLATION_TOO_LATE", exception.Code);
            Assert.Equal("BOOKED", booking.Status);
        }
    }
}
