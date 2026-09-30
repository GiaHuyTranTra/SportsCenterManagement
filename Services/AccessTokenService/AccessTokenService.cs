using APIViewModel.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Services.Utils;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Services.AccessTokenService;

public class AccessTokenService : IAccessTokenService
{
    private readonly JwtOptions _jwtOptions;

    public AccessTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateAccessToken(LoginResponseAPIViewModel account)
    {
        return GenerateAccessTokenWithMetadata(account).AccessToken;
    }

    public GeneratedAccessTokenAPIViewModel GenerateAccessTokenWithMetadata(
        LoginResponseAPIViewModel account)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey must not be empty.");
        }

        if (_jwtOptions.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:ExpirationMinutes must be greater than zero.");
        }

        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        string jti = Guid.NewGuid().ToString("N");

        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(ClaimTypes.Role, account.Role),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        SymmetricSecurityKey signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        SigningCredentials credentials = new SigningCredentials(
            signingKey, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        string accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new GeneratedAccessTokenAPIViewModel
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc
        };
    }
}
