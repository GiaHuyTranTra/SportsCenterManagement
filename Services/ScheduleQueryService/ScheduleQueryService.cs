using APIViewModel;
using APIViewModel.ClassRegistration;
using DataAccess.Entities;
using Services.AuditLogService;
using Services.ClassSessionService;
using Services.ClassRegistrationService;
using Services.NotificationService;

namespace Services.ScheduleQueryService;

public class ScheduleQueryService : IScheduleQueryService
{
    private readonly IClassSessionService _sessions;
    private readonly IClassRegistrationService _registrations;

    public ScheduleQueryService(SportsCenterManagementContext context,
        IClassSessionService? sessions = null, IClassRegistrationService? registrations = null)
    {
        _sessions = sessions ?? new ClassSessionService.ClassSessionService(context,
            new AuditLogService.AuditLogService(context), new NotificationService.NotificationService(context));
        _registrations = registrations ?? new ClassRegistrationService.ClassRegistrationService(context,
            new AuditLogService.AuditLogService(context), new NotificationService.NotificationService(context));
    }

    public Task<PagedResult<ClassSessionViewDto>> GetCenterScheduleAsync(DateTime? from, DateTime? to,
        int? sportId, string? coachId, int? roomId, string? status, int page, int pageSize) =>
        _sessions.GetSessionsPagedAsync(from, to, null, coachId, roomId, sportId, status, page, pageSize, false);

    public Task<PagedResult<ClassSessionViewDto>> GetAvailableSessionsAsync(DateTime? from, DateTime? to,
        int? sportId, string? coachId, int page, int pageSize) =>
        _sessions.GetSessionsPagedAsync(from, to, null, coachId, null, sportId, "SCHEDULED", page, pageSize, true);

    public Task<PagedResult<ClassSessionViewDto>> GetCoachTeachingScheduleAsync(string coachAccountId,
        DateTime? from, DateTime? to, string? status, int page, int pageSize) =>
        _sessions.GetCoachScheduleAsync(coachAccountId, from ?? DateTime.MinValue, to ?? DateTime.MaxValue, status, page, pageSize);

    public Task<PagedResult<ClassRosterMemberDto>> GetSessionRosterAsync(int sessionId,
        string callerAccountId, bool isManager, bool includeCanceled, int page, int pageSize) =>
        _sessions.GetSessionRosterAsync(sessionId, callerAccountId, isManager, includeCanceled, page, pageSize);

    public async Task<List<MemberBookedSessionDto>> GetMyClassScheduleAsync(string memberAccountId,
        DateTime? from, DateTime? to)
    {
        List<MemberBookedSessionDto> result = new List<MemberBookedSessionDto>();
        int page = 1;
        PagedResult<ClassRegistrationViewModel> registrations;
        do
        {
            registrations = await _registrations.GetMyRegistrationsAsync(memberAccountId, true, page++, 100, from, to);
            result.AddRange(registrations.Items.Select(item => new MemberBookedSessionDto
            {
                RegistrationId = item.Id, RegistrationStatus = item.Status, RegisteredAt = item.RegisteredAt,
                Id = item.Session.Id, ClassId = item.Session.ClassId, ClassName = item.Session.ClassName,
                SportId = item.Session.SportId, SportName = item.Session.SportName,
                CoachId = item.Session.CoachId, CoachName = item.Session.CoachName,
                RoomId = item.Session.RoomId, RoomName = item.Session.RoomName,
                Date = item.Session.Date, StartTime = item.Session.StartTime, EndTime = item.Session.EndTime,
                StartsAt = item.Session.StartsAt, EndsAt = item.Session.EndsAt,
                Capacity = item.Session.Capacity, BookedCount = item.Session.BookedCount,
                Status = item.Session.Status, Notes = item.Session.Notes, CancelReason = item.Session.CancelReason,
                CanceledAt = item.Session.CanceledAt, UpdatedAt = item.Session.UpdatedAt
            }));
        } while (page <= registrations.TotalPages);
        return result;
    }
}
