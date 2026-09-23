using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SportsCenterManagement.Tests;

public class AccessTokenServiceTests
{
    [Fact]
    public void GenerateAccessToken_IncludesRequiredClaimsAndExpiration()
    {
        var account = AuthTestData.CreateAccount();
        var generatedToken = AuthTestData.CreateAccessTokenService().GenerateAccessToken(account);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(generatedToken.Value);

        Assert.Equal(account.Id, jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(account.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(account.Role.Name, jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.Equal(jwt.ValidTo, generatedToken.ExpiresAtUtc, TimeSpan.FromSeconds(1));
    }
}
