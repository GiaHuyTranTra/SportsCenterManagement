using System.IdentityModel.Tokens.Jwt;

namespace Services.Utils;

public static class AccessTokenUtil
{
    public static bool TryReadActiveToken(string? accessToken, out AccessTokenInfo? tokenInfo)
    {
        tokenInfo = null;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var jti = token.Id;
            var expiresAtUtc = token.ValidTo;

            if (string.IsNullOrWhiteSpace(jti) || expiresAtUtc <= DateTime.UtcNow)
            {
                return false;
            }

            tokenInfo = new AccessTokenInfo(jti, expiresAtUtc, expiresAtUtc - DateTime.UtcNow);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed record AccessTokenInfo(string Jti, DateTime ExpiresAtUtc, TimeSpan RemainingLifetime);
