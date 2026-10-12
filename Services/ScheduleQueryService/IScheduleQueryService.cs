using APIViewModel;

namespace Services.ScheduleQueryService;

public interface IScheduleQueryService
{
    Task<PagedResult<ClassSessionViewDto>> GetCenterScheduleAsync(
        DateTime? from,
        DateTime? to,
        int? sportId,
        string? coachId,
        int? roomId,
        string? status,
        int page,
        int pageSize);

    Task<PagedResult<ClassSessionViewDto>> GetAvailableSessionsAsync(
        DateTime? from,
        DateTime? to,
        int? sportId,
        string? coachId,
        int page,
        int pageSize);

    Task<List<MemberBookedSessionDto>> GetMyClassScheduleAsync(
        string memberAccountId,
        DateTime? from,
        DateTime? to);

    Task<PagedResult<ClassSessionViewDto>> GetCoachTeachingScheduleAsync(
        string coachAccountId,
        DateTime? from,
        DateTime? to,
        string? status,
        int page,
        int pageSize);

    Task<PagedResult<ClassRosterMemberDto>> GetSessionRosterAsync(
        int sessionId,
        string callerAccountId,
        bool isManager,
        bool includeCanceled,
        int page,
        int pageSize);
}