using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            CreateToken("account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));

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
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            CreateToken(
                "manager-account",
                "manager@example.com",
                "CenterManager",
                DateTime.UtcNow.AddMinutes(10)));

        HttpResponseMessage logoutResponse = await client.PostAsync("/api/auth/logout", null);
        HttpResponseMessage memberListResponse = await client.GetAsync("/api/member");

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, memberListResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            CreateToken("account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(-1)));

        HttpResponseMessage response = await client.PostAsync("/api/auth/check-token", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewApiProcess_DoesNotRetainInMemoryBlacklist()
    {
        string token = CreateToken(
            "account-1",
            "member@example.com",
            "Member",
            DateTime.UtcNow.AddMinutes(10));

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

    [Fact]
    public async Task DeletedAccount_WithExistingToken_ReturnsForbidden()
    {
        await using AuthenticationWebApplicationFactory factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            CreateToken(
                "manager-account",
                "manager@example.com",
                "CenterManager",
                DateTime.UtcNow.AddMinutes(10)));
        await factory.MarkDeletedAsync("manager-account");

        HttpResponseMessage response = await client.GetAsync("/api/member");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentOrigin_PreflightRequest_IsAllowed()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/api/member");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(
            "http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task CenterManager_CanCallEveryMemberManagementRoute()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            CreateToken(
                "manager-account",
                "manager@example.com",
                "CenterManager",
                DateTime.UtcNow.AddMinutes(10)));

        HttpResponseMessage list = await client.GetAsync("/api/member");
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/member", new
        {
            fullName = "Created Member",
            email = "created.member@example.com",
            phone = "0911111111",
            dateOfBirth = "2000-01-02",
            isActive = true
        });
        HttpResponseMessage update = await client.PatchAsJsonAsync("/api/member/account-1", new
        {
            fullName = "Updated Member",
            email = "updated.member@example.com",
            phone = "0922222222",
            dateOfBirth = "2000-01-02",
            isActive = true
        });
        HttpResponseMessage delete = await client.DeleteAsync("/api/member/account-1");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Theory]
    [InlineData("coach-account", "coach@example.com", "Coach")]
    [InlineData("account-1", "member@example.com", "Member")]
    [InlineData("receptionist-account", "receptionist@example.com", "Receptionist")]
    public async Task NonManagers_CannotCallMemberManagementRoutes(
        string accountId,
        string email,
        string role)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(
            factory,
            CreateToken(accountId, email, role, DateTime.UtcNow.AddMinutes(10)));

        HttpResponseMessage list = await client.GetAsync("/api/member");
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/member", new { });
        HttpResponseMessage update = await client.PatchAsJsonAsync("/api/member/account-1", new { });
        HttpResponseMessage delete = await client.DeleteAsync("/api/member/account-1");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task ActiveMembershipPackages_RemainAnonymous()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/membershippackage/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private static string CreateToken(
        string accountId,
        string email,
        string role,
        DateTime expiresAtUtc)
    {
        Claim[] claims = new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
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
        private readonly string _databaseName = "AuthenticationIntegrationTests-" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((WebHostBuilderContext context, IConfigurationBuilder configurationBuilder) =>
            {
                Dictionary<string, string?> settings = new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = AuthTestData.SigningKey,
                    ["Jwt:ExpirationMinutes"] = "1440"
                };

                configurationBuilder.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices((IServiceCollection services) =>
            {
                services.RemoveAll<SportsCenterManagementContext>();
                services.RemoveAll<DbContextOptions<SportsCenterManagementContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SportsCenterManagementContext>>();
                services.AddDbContext<SportsCenterManagementContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));

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

        protected override IHost CreateHost(IHostBuilder builder)
        {
            IHost host = base.CreateHost(builder);
            using IServiceScope scope = host.Services.CreateScope();
            SportsCenterManagementContext context =
                scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
            context.Database.EnsureCreated();
            SeedAuthenticationData(context);
            return host;
        }

        public async Task MarkDeletedAsync(string accountId)
        {
            using IServiceScope scope = Services.CreateScope();
            SportsCenterManagementContext context =
                scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
            Account account = await context.Accounts.SingleAsync(candidate => candidate.Id == accountId);
            account.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        private static void SeedAuthenticationData(SportsCenterManagementContext context)
        {
            DateTime createdAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
            Role managerRole = new Role { Id = 1, Name = "CenterManager" };
            Role coachRole = new Role { Id = 2, Name = "Coach" };
            Role memberRole = new Role { Id = 3, Name = "Member" };
            Role receptionistRole = new Role { Id = 4, Name = "Receptionist" };

            Account manager = new Account
            {
                Id = "manager-account",
                Email = "manager@example.com",
                PasswordHash = "unused",
                Status = "Active",
                CreatedAt = createdAt,
                RoleId = managerRole.Id,
                Role = managerRole,
                CenterManager = new CenterManager
                {
                    AccountId = "manager-account",
                    FullName = "Long Manager",
                    CreatedAt = createdAt
                }
            };
            Account coach = new Account
            {
                Id = "coach-account",
                Email = "coach@example.com",
                PasswordHash = "unused",
                Status = "Active",
                CreatedAt = createdAt,
                RoleId = coachRole.Id,
                Role = coachRole,
                Coach = new Coach
                {
                    AccountId = "coach-account",
                    FullName = "Long Coach",
                    CreatedAt = createdAt
                }
            };
            Account member = new Account
            {
                Id = "account-1",
                Email = "member@example.com",
                PasswordHash = "unused",
                Status = "Active",
                CreatedAt = createdAt,
                RoleId = memberRole.Id,
                Role = memberRole,
                Member = new Member
                {
                    AccountId = "account-1",
                    MemberCode = "MEM001",
                    FullName = "Long Member",
                    CreatedAt = createdAt
                }
            };
            Account receptionist = new Account
            {
                Id = "receptionist-account",
                Email = "receptionist@example.com",
                PasswordHash = "unused",
                Status = "Active",
                CreatedAt = createdAt,
                RoleId = receptionistRole.Id,
                Role = receptionistRole,
                Receptionist = new Receptionist
                {
                    AccountId = "receptionist-account",
                    FullName = "Long Receptionist",
                    CreatedAt = createdAt
                }
            };

            context.Accounts.AddRange(manager, coach, member, receptionist);
            context.SaveChanges();
        }
    }
}
