using APIViewModel.Account;
using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;

namespace Services.AccountService;

public enum GetCurrentUserProfileResult
{
    Success,
    AccountNotFound,
    ProfileNotFound,
    UnsupportedRole
}

public enum UpdateCurrentProfileResult
{
    Success,
    AccountNotFound,
    ProfileNotFound,
    UnsupportedRole,
    InvalidData,
    InvalidFieldForRole,
    DuplicatePhone,
    NoChanges,
    ConcurrencyConflict
}

public enum UnlockAccountResult
{
    Success,
    NotFound
}

public enum UpdateAccountStatusResult
{
    Success,
    NotFound,
    InvalidStatus,
    SelfDeactivationNotAllowed,
    LastActiveCenterManager,
    ConcurrencyConflict
}

public interface IAccountService
{
    Task<bool> CreateCenterManagerAsync(CreateCenterManagerAPIViewModel info);

    Task<bool> CreateCoachAsync(CreateCoachAPIViewModel info);

    Task<bool> CreateMemberAsync(CreateMemberAPIViewModel info);

    Task<bool> CreateReceptionistAsync(CreateReceptionistAPIViewModel info);

    Task<bool> IsEmailExistsAsync(string email);

    Task<RegisterMemberResponseAPIViewModel?> RegisterMemberAsync(RegisterMemberRequestAPIViewModel info);

    Task<(GetCurrentUserProfileResult Result, CurrentUserProfileAPIViewModel? Profile)>
        GetCurrentUserProfileAsync(string accountId);

    Task<UpdateCurrentProfileResult> UpdateCurrentProfileAsync(
        string accountId,
        UpdateProfileAPIViewModel request);

    Task<UnlockAccountResult> UnlockAccountAsync(string accountId);

    Task<UpdateAccountStatusResult> UpdateAccountStatusAsync(
        string actorAccountId,
        string targetAccountId,
        string status);
}
