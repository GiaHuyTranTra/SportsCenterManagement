using APIViewModel.AccountProfile;
using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;

namespace Services.AccountService;

public interface IAccountService
{
    Task<AccountProfileAPIViewModel?> GetProfileAsync(string accountId);

    Task<AccountProfileAPIViewModel?> UpdateProfileAsync(
        string accountId,
        UpdateAccountProfileAPIViewModel request);

    Task<bool> CreateCenterManagerAsync(CreateCenterManagerAPIViewModel info);

    Task<bool> CreateCoachAsync(CreateCoachAPIViewModel info);

    Task<RegisterMemberResponseAPIViewModel?> CreateMemberAsync(CreateMemberAPIViewModel info);

    Task<bool> CreateReceptionistAsync(CreateReceptionistAPIViewModel info);

    Task<bool> IsEmailExistsAsync(string email);

    Task<RegisterMemberResponseAPIViewModel?> RegisterMemberAsync(RegisterMemberRequestAPIViewModel info);
}
