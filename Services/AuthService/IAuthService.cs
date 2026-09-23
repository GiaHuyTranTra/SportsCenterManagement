using APIViewModel.Auth;

namespace Services.AuthService;

public interface IAuthService
{
    Task<LoginResponseAPIViewModel?> LoginCenterManagerAsync(LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> LoginCoachAsync(LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> LoginMemberAsync(LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> LoginReceptionistAsync(LoginRequestAPIViewModel request);
}
