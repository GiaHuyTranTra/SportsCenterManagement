using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Services.Utils;

namespace SportsCenterManagement.Tests;

public class AccessTokenUtilTests
{
    [Fact]
    public void TryReadActiveToken_ExtractsJtiAndRemainingLifetime()
    {
        var token = CreateToken("active-jti", DateTime.UtcNow.AddMinutes(10));

        var success = AccessTokenUtil.TryReadActiveToken(token, out var tokenInfo);

        Assert.True(success);
        Assert.Equal("active-jti", tokenInfo!.Jti);
        Assert.True(tokenInfo.RemainingLifetime > TimeSpan.Zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public void TryReadActiveToken_RejectsMissingOrMalformedToken(string? token)
    {
        Assert.False(AccessTokenUtil.TryReadActiveToken(token, out _));
    }

    [Fact]
    public void TryReadActiveToken_RejectsExpiredToken()
    {
        var token = CreateToken("expired-jti", DateTime.UtcNow.AddMinutes(-1));

        Assert.False(AccessTokenUtil.TryReadActiveToken(token, out _));
    }

    internal static string CreateToken(string jti, DateTime expiresAtUtc)
    {
        var jwt = new JwtSecurityToken(
            claims: new[] { new Claim(JwtRegisteredClaimNames.Jti, jti) },
            expires: expiresAtUtc);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
