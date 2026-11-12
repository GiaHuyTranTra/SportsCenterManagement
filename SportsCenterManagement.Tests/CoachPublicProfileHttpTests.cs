using System.Net;
using System.Net.Http.Json;
using APIViewModel.Coach;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public sealed class CoachPublicProfileHttpTests
{
    [Theory]
    [InlineData("account-1", "member@example.com", "Member")]
    [InlineData("coach-account", "coach@example.com", "Coach")]
    [InlineData("receptionist-account", "receptionist@example.com", "Receptionist")]
    [InlineData("manager-account", "manager@example.com", "CenterManager")]
    public async Task EveryAuthenticatedRole_CanReadActiveCoach(string accountId, string email, string role)
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient client = AuthenticationIntegrationTests.CreateAuthenticatedClient(factory,
            AuthenticationIntegrationTests.CreateToken(accountId, email, role, DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync("/api/coaches/coach-account/public-profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CoachPublicProfileAPIViewModel? profile =
            await response.Content.ReadFromJsonAsync<CoachPublicProfileAPIViewModel>();
        Assert.Equal("coach-account", profile!.AccountId);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isLocked", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnonymousRequest_IsUnauthorized()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/coaches/coach-account/public-profile")).StatusCode);
    }

    [Fact]
    public async Task MissingCoach_ReturnsStableNotFoundCode()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient client = AuthenticationIntegrationTests.CreateAuthenticatedClient(factory,
            AuthenticationIntegrationTests.CreateToken(
                "account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync("/api/coaches/missing/public-profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("COACH_PUBLIC_PROFILE_NOT_FOUND", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MemberStillCannotAccessManagerCoachCrud()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient client = AuthenticationIntegrationTests.CreateAuthenticatedClient(factory,
            AuthenticationIntegrationTests.CreateToken(
                "account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/coach")).StatusCode);
    }
}
