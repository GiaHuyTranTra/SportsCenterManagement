using APIViewModel.Coach;

namespace Services.CoachService;

public enum CreateCoachResult
{
    Success,
    InvalidData,
    DuplicateEmail,
    DuplicatePhone,
    DisciplineNotFoundOrInactive,
    CoachRoleMissing,
    ConcurrencyConflict
}

public enum UpdateCoachResult
{
    Success,
    NotFound,
    InvalidData,
    DuplicatePhone,
    DisciplineNotFoundOrInactive,
    NoChanges,
    ConcurrencyConflict
}

public enum UpdateCoachStatusResult
{
    Success,
    NotFound,
    InvalidStatus,
    NoChanges,
    ConcurrencyConflict
}

public enum DeleteCoachResult
{
    Success,
    NotFound,
    AlreadyDeleted,
    ConcurrencyConflict
}

public interface ICoachService
{
    Task<PagedCoachAPIViewModel> GetCoachesAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        int? disciplineId);

    Task<CoachDetailAPIViewModel?> GetCoachByIdAsync(string accountId);

    Task<(CreateCoachResult Result, CoachDetailAPIViewModel? Data)> CreateCoachAsync(
        string actorAccountId,
        CreateManagedCoachAPIViewModel request);

    Task<(UpdateCoachResult Result, CoachDetailAPIViewModel? Data)> UpdateCoachAsync(
        string actorAccountId,
        string coachAccountId,
        UpdateManagedCoachAPIViewModel request);

    Task<UpdateCoachStatusResult> UpdateCoachStatusAsync(
        string actorAccountId,
        string coachAccountId,
        string status);

    Task<DeleteCoachResult> SoftDeleteCoachAsync(
        string actorAccountId,
        string coachAccountId);
}
