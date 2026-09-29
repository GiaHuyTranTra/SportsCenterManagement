using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SportsCenterManagement.Tests;

public class AuthenticationIntegrationTests
{
    private const string Issuer = "SportsCenterManagement";
    private const string Audience = "SportsCenterManagement";

    [Fact]
    public async Task ValidToken_CanAccessCheckToken()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, CreateToken(DateTime.UtcNow.AddMinutes(10)));

        HttpResponseMessage response = await client.PostAsync("/api/auth/check-token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("account-1", body);
        Assert.Contains("member@example.com", body);
        Assert.Contains("Member", body);
    }

    [Fact]
    public async Task Logout_RevokesTokenForEveryProtectedEndpoint()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, CreateToken(DateTime.UtcNow.AddMinutes(10)));

        HttpResponseMessage logoutResponse = await client.PostAsync("/api/auth/logout", null);
        HttpResponseMessage checkTokenResponse = await client.PostAsync("/api/auth/check-token", null);
        HttpResponseMessage secondLogoutResponse = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, checkTokenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondLogoutResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, CreateToken(DateTime.UtcNow.AddMinutes(-1)));

        HttpResponseMessage response = await client.PostAsync("/api/auth/check-token", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewApiProcess_DoesNotRetainInMemoryBlacklist()
    {
        string token = CreateToken(DateTime.UtcNow.AddMinutes(10));

        await using (WebApplicationFactory<Program> firstFactory = new AuthenticationWebApplicationFactory())
        using (HttpClient firstClient = CreateAuthenticatedClient(firstFactory, token))
        {
            Assert.Equal(HttpStatusCode.OK, (await firstClient.PostAsync("/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await firstClient.PostAsync("/api/auth/check-token", null)).StatusCode);
        }

        await using WebApplicationFactory<Program> restartedFactory = new AuthenticationWebApplicationFactory();
        using HttpClient restartedClient = CreateAuthenticatedClient(restartedFactory, token);

        Assert.Equal(HttpStatusCode.OK, (await restartedClient.PostAsync("/api/auth/check-token", null)).StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string token)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CreateToken(DateTime expiresAtUtc)
    {
        Claim[] claims = new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "account-1"),
            new Claim(JwtRegisteredClaimNames.Email, "member@example.com"),
            new Claim(ClaimTypes.Role, "Member"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        SigningCredentials credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthTestData.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        JwtSecurityToken jwt = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class AuthenticationWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((WebHostBuilderContext context, IConfigurationBuilder configurationBuilder) =>
            {
                Dictionary<string, string?> settings = new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = AuthTestData.SigningKey,
                    ["Jwt:ExpirationMinutes"] = "1440",
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=SportsCenterManagementTests;Trusted_Connection=True;"
                };

                configurationBuilder.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices((IServiceCollection services) =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Issuer,
                        ValidAudience = Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(AuthTestData.SigningKey)),
                        RoleClaimType = ClaimTypes.Role,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            });

            builder.ConfigureLogging((ILoggingBuilder loggingBuilder) =>
            {
                loggingBuilder.ClearProviders();
            });
        }
    }
}
