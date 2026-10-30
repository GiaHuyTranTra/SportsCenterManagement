using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Class;

namespace Services.ClassService;

public interface IClassService
{
    Task<PagedResult<ClassViewModel>> GetClassesPagedAsync(
        int page,
        int pageSize,
        string? search,
        int? sportId,
        string? status,
        bool isMember);

    Task<ClassViewModel?> GetClassByIdAsync(int id, bool isMember);

    Task<ClassViewModel> CreateClassAsync(
        string actorAccountId,
        CreateClassAPIViewModel request);

    Task<ClassViewModel> UpdateClassAsync(
        string actorAccountId,
        int id,
        UpdateClassAPIViewModel request);

    Task<AddClassScheduleResultViewModel> AddScheduleAsync(
        string actorAccountId,
        int classId,
        AddClassScheduleAPIViewModel request);

    Task<ClassViewModel> UpdateClassStatusAsync(
        string actorAccountId,
        int id,
        string status);

    Task<ClassCancelImpactViewModel> GetClassCancelImpactAsync(int classId);

    Task<CancelFutureSessionsResultViewModel> CancelFutureSessionsAsync(
        string actorAccountId,
        int classId,
        string reason);
}
