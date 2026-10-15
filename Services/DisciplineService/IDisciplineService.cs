using APIViewModel.Discipline;

namespace Services.DisciplineService;

public enum CreateDisciplineResult
{
    Success,
    InvalidData,
    DuplicateName,
    ConcurrencyConflict
}

public enum UpdateDisciplineResult
{
    Success,
    NotFound,
    InvalidData,
    DuplicateName,
    NoChanges,
    ConcurrencyConflict
}

public enum UpdateDisciplineStatusResult
{
    Success,
    NotFound,
    NoChanges,
    ConcurrencyConflict
}

public interface IDisciplineService
{
    Task<List<DisciplineAPIViewModel>> GetDisciplinesAsync(bool activeOnly);

    Task<APIViewModel.PagedResult<DisciplineAPIViewModel>> GetSportsPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive);

    Task<(CreateDisciplineResult Result, DisciplineAPIViewModel? Data)> CreateDisciplineAsync(
        string actorAccountId,
        CreateDisciplineAPIViewModel request, bool sportContract = false);

    Task<(UpdateDisciplineResult Result, DisciplineAPIViewModel? Data)> UpdateDisciplineAsync(
        string actorAccountId,
        int disciplineId,
        UpdateDisciplineAPIViewModel request, bool sportContract = false);

    Task<UpdateDisciplineStatusResult> UpdateDisciplineStatusAsync(
        string actorAccountId,
        int disciplineId,
        bool isActive, bool sportContract = false);
}
