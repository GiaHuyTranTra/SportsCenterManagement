using System;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassSession;

namespace Services.ClassSessionService;

public interface IClassSessionService
{
    Task<PagedResult<ClassSessionViewDto>> GetSessionsPagedAsync(
        DateTime? from,
        DateTime? to,
        int? classId,
        string? coachId,
        int? roomId,
        int? sportId,
        string? status,
        int page,
        int pageSize,
        bool isMember);

    Task<ClassSessionViewDto?> GetSessionByIdAsync(int id, bool isMember);

    Task<ClassSessionViewDto> CreateSessionAsync(
        string actorAccountId,
        CreateClassSessionAPIViewModel request);

    Task<ClassSessionViewDto> UpdateSessionAsync(
        string actorAccountId,
        int id,
        UpdateClassSessionAPIViewModel request);

    Task<ClassSessionViewDto> UpdateSessionCoachAsync(
        string actorAccountId,
        int id,
        string newCoachId);

    Task<PagedResult<ClassSessionViewDto>> GetCoachScheduleAsync(
        string coachAccountId,
        DateTime? from,
        DateTime? to,
        string? status,
        int page,
        int pageSize);

    Task<SessionCancelImpactViewModel> GetSessionCancelImpactAsync(int sessionId);

    Task<ClassSessionViewDto> CancelSessionAsync(
        string actorAccountId,
        int sessionId,
        string reason);

    Task<PagedResult<ClassRosterMemberDto>> GetSessionRosterAsync(
        int sessionId,
        string callerAccountId,
        bool isManager,
        bool includeCanceled,
        int page,
        int pageSize,
        string? search = null);
}
