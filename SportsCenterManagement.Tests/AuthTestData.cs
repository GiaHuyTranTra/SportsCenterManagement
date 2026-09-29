using DataAccess.Entities;
using Microsoft.Extensions.Options;
using Services.AccessTokenService;
using Services.PasswordHashService;
using Services.Utils;

namespace SportsCenterManagement.Tests;

internal static class AuthTestData
{
    internal const string SigningKey = "test-signing-key-with-at-least-thirty-two-characters";

    internal static Account CreateAccount(string password = "CorrectPassword123!")
    {
        Role role = new Role
        {
            Id = 3,
            Name = "Member"
        };

        return new Account
        {
            Id = "account-1",
            RoleId = role.Id,
            Email = "member@example.com",
            PasswordHash = new PasswordHashService().HashPassword(password),
            Status = "Active",
            Role = role
        };
    }

    internal static AccessTokenService CreateAccessTokenService(int expirationMinutes = 60)
    {
        return new AccessTokenService(Options.Create(new JwtOptions
        {
            Issuer = "SportsCenterManagement",
            Audience = "SportsCenterManagement",
            SigningKey = SigningKey,
            ExpirationMinutes = expirationMinutes
        }));
    }
}
