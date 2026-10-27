using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("account-1", "Member", HttpStatusCode.Forbidden)]
    [InlineData("coach-account", "Coach", HttpStatusCode.Forbidden)]
    [InlineData("receptionist-account", "Receptionist", HttpStatusCode.Forbidden)]
    [InlineData("manager-account", "CenterManager", HttpStatusCode.OK)]
    public async Task ClassReport_RequiresManager(string accountId, string role, HttpStatusCode expected)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, accountId + "@example.com", role, DateTime.UtcNow.AddMinutes(10)));
        using HttpResponseMessage response = await client.GetAsync("/api/reports/classes?from=2026-10-01&to=2026-10-27");
        Assert.Equal(expected, response.StatusCode);
    }
}
