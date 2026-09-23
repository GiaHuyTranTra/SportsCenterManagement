using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;

namespace Services.AccountService;

public interface IAccountService
{
    Task<bool> CreateCenterManagerAsync(CreateCenterManagerAPIViewModel info);

    Task<bool> CreateCoachAsync(CreateCoachAPIViewModel info);

    Task<bool> CreateMemberAsync(CreateMemberAPIViewModel info);

    Task<bool> CreateReceptionistAsync(CreateReceptionistAPIViewModel info);
}
