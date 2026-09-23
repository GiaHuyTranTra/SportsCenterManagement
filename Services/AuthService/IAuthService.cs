using APIViewModel.Auth;

namespace Services.AuthService;

public interface IAuthService
{
    Task<LoginResponseAPIViewModel?> LoginAdminAsync(LoginRequestAPIViewModel request);
}
