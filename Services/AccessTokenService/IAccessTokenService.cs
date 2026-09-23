using APIViewModel.Auth;
using DataAccess.Entities;

namespace Services.AccessTokenService;

public interface IAccessTokenService
{
    string GenerateAccessToken(LoginResponseAPIViewModel account);
}

