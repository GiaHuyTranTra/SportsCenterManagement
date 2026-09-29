using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using APIViewModel.Auth;
using DataAccess.Entities;

namespace SportsCenterManagement.Tests;

public class AccessTokenServiceTests
{
    [Fact]
    public void GenerateAccessToken_IncludesRequiredClaimsAndExpiration()
    {
        Account account = AuthTestData.CreateAccount();
        LoginResponseAPIViewModel loginResponse = new LoginResponseAPIViewModel
        {
            Id = account.Id,
            Email = account.Email,
            Role = account.Role.Name
        };

        string token = AuthTestData.CreateAccessTokenService(1440).GenerateAccessToken(loginResponse);
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(account.Id, jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(account.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(account.Role.Name, jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(1440).AddSeconds(10));
    }
}
