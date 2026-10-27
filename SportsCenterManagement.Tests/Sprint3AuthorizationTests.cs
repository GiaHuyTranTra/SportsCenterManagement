using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("/api/reports/members")]
    [InlineData("/api/MembershipInvoice/search")]
    public async Task Sprint3_AnonymousRequestIsRejected(string url)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("account-1", "Member", HttpStatusCode.OK)]
    [InlineData("coach-account", "Coach", HttpStatusCode.Forbidden)]
    [InlineData("receptionist-account", "Receptionist", HttpStatusCode.OK)]
    [InlineData("manager-account", "CenterManager", HttpStatusCode.OK)]
    public async Task Sprint3_InvoiceSearchEnforcesRoles(string accountId, string role, HttpStatusCode expected)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, accountId + "@example.com", role, DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync("/api/MembershipInvoice/search");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("account-1", "Member", HttpStatusCode.Forbidden)]
    [InlineData("coach-account", "Coach", HttpStatusCode.Forbidden)]
    [InlineData("receptionist-account", "Receptionist", HttpStatusCode.Forbidden)]
    [InlineData("manager-account", "CenterManager", HttpStatusCode.OK)]
    public async Task MemberReport_RequiresManager(string accountId, string role, HttpStatusCode expected)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, accountId + "@example.com", role, DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync("/api/reports/members?from=2026-10-01&to=2026-10-27");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/MembershipInvoice/search?page=0")]
    [InlineData("/api/MembershipInvoice/search?pageSize=101")]
    [InlineData("/api/MembershipInvoice/search?status=INVALID")]
    [InlineData("/api/MembershipInvoice/search?paymentMethod=INVALID")]
    [InlineData("/api/reports/members?from=2026-10-28&to=2026-10-27")]
    public async Task Sprint3_InvalidFilterReturnsStructuredError(string url)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken("manager-account", "manager@example.com", "CenterManager", DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":false", body);
        Assert.Contains("\"traceId\":", body);
    }
}
