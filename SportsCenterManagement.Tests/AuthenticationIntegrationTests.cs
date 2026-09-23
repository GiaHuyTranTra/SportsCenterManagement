using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace SportsCenterManagement.Tests;

public class AuthenticationIntegrationTests
{
    private const string Issuer = "SportsCenterManagement";
    private const string Audience = "SportsCenterManagement";
    private const string SigningKey = "sports-center-management-development-secret-key-change-me";

    [Fact]
    public async Task ValidToken_CanAccessCheckToken()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = CreateAuthenticatedClient(factory, CreateToken(DateTime.UtcNow.AddMinutes(10)));

        var response = await client.PostAsync("/api/auth/check-token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("account-1", body);
        Assert.Contains("member@example.com", body);
        Assert.Contains("Member", body);
    }

    [Fact]
    public async Task Logout_RevokesTokenForEveryProtectedEndpoint()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = CreateAuthenticatedClient(factory, CreateToken(DateTime.UtcNow.AddMinutes(10)));

        var logoutResponse = await client.PostAsync("/api/auth/logout", null);
        var checkTokenResponse = await client.PostAsync("/api/auth/check-token", null);
        var secondLogoutResponse = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, checkTokenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondLogoutResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = CreateAuthenticatedClient(factory, CreateToken(DateTime.UtcNow.AddMinutes(-1)));

        var response = await client.PostAsync("/api/auth/check-token", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewApiProcess_DoesNotRetainInMemoryBlacklist()
    {
        var token = CreateToken(DateTime.UtcNow.AddMinutes(10));

        await using (var firstFactory = new WebApplicationFactory<Program>())
        using (var firstClient = CreateAuthenticatedClient(firstFactory, token))
        {
            Assert.Equal(HttpStatusCode.OK, (await firstClient.PostAsync("/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await firstClient.PostAsync("/api/auth/check-token", null)).StatusCode);
        }

        await using var restartedFactory = new WebApplicationFactory<Program>();
        using var restartedClient = CreateAuthenticatedClient(restartedFactory, token);

        Assert.Equal(HttpStatusCode.OK, (await restartedClient.PostAsync("/api/auth/check-token", null)).StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CreateToken(DateTime expiresAtUtc)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "account-1"),
            new Claim(JwtRegisteredClaimNames.Email, "member@example.com"),
            new Claim(ClaimTypes.Role, "Member"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
