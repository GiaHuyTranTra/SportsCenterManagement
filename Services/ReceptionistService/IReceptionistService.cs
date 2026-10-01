using APIViewModel.Receptionist;

namespace Services.ReceptionistService;

public enum CreateReceptionistResult
{
    Success,
    InvalidData,
    DuplicateEmail,
    DuplicatePhone,
    ReceptionistRoleMissing,
    ConcurrencyConflict
}

public enum UpdateReceptionistResult
{
    Success,
    NotFound,
    InvalidData,
    DuplicatePhone,
    NoChanges,
    ConcurrencyConflict
}

public enum UpdateReceptionistStatusResult
{
    Success,
    NotFound,
    InvalidStatus,
    NoChanges,
    ConcurrencyConflict
}

public enum DeleteReceptionistResult
{
    Success,
    NotFound,
    AlreadyDeleted,
    ConcurrencyConflict
}

public interface IReceptionistService
{
    Task<PagedReceptionistAPIViewModel> GetReceptionistsAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        string? workShift);

    Task<ReceptionistDetailAPIViewModel?> GetReceptionistByIdAsync(string accountId);

    Task<(CreateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data)>
        CreateReceptionistAsync(
            string actorAccountId,
            CreateReceptionistAPIViewModel request);

    Task<(UpdateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data)>
        UpdateReceptionistAsync(
            string actorAccountId,
            string receptionistAccountId,
            UpdateManagedReceptionistAPIViewModel request);

    Task<UpdateReceptionistStatusResult> UpdateReceptionistStatusAsync(
        string actorAccountId,
        string receptionistAccountId,
        string status);

    Task<DeleteReceptionistResult> SoftDeleteReceptionistAsync(
        string actorAccountId,
        string receptionistAccountId);
}
