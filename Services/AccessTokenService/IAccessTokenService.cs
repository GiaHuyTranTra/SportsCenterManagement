using APIViewModel.Auth;

namespace Services.AccessTokenService;

public interface IAccessTokenService
{
    string GenerateAccessToken(LoginResponseAPIViewModel account);

    GeneratedAccessTokenAPIViewModel GenerateAccessTokenWithMetadata(
        LoginResponseAPIViewModel account);
}
