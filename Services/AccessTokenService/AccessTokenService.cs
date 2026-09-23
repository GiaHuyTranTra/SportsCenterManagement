using APIViewModel.Auth;
using DataAccess.Entities;
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
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new(ClaimTypes.Role, account.Role)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
