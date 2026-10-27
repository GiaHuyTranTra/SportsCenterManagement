using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("revenue", "account-1", "Member", HttpStatusCode.Forbidden)]
    [InlineData("revenue", "coach-account", "Coach", HttpStatusCode.Forbidden)]
    [InlineData("revenue", "receptionist-account", "Receptionist", HttpStatusCode.Forbidden)]
    [InlineData("revenue", "manager-account", "CenterManager", HttpStatusCode.OK)]
    [InlineData("members", "account-1", "Member", HttpStatusCode.Forbidden)]
    [InlineData("members", "coach-account", "Coach", HttpStatusCode.Forbidden)]
    [InlineData("members", "receptionist-account", "Receptionist", HttpStatusCode.Forbidden)]
    [InlineData("members", "manager-account", "CenterManager", HttpStatusCode.OK)]
    [InlineData("classes", "account-1", "Member", HttpStatusCode.Forbidden)]
    [InlineData("classes", "coach-account", "Coach", HttpStatusCode.Forbidden)]
    [InlineData("classes", "receptionist-account", "Receptionist", HttpStatusCode.Forbidden)]
    [InlineData("classes", "manager-account", "CenterManager", HttpStatusCode.OK)]
    public async Task ReportExport_RequiresManagerAndReturnsCsv(string report, string accountId, string role, HttpStatusCode expected)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, accountId + "@example.com", role, DateTime.UtcNow.AddMinutes(10)));
        using HttpResponseMessage response = await client.GetAsync($"/api/reports/{report}/export?from=2026-10-01&to=2026-10-27");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            Assert.DoesNotContain("password", Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("/api/reports/revenue/export?paymentMethod=CARD", "UNSUPPORTED_REPORT_PAYMENT_METHOD")]
    [InlineData("/api/reports/classes/export?registrationStatus=ATTENDED", "INVALID_CLASS_REPORT_FILTER")]
    [InlineData("/api/reports/members/export?from=2026-10-28&to=2026-10-27", "INVALID_REPORT_DATE_RANGE")]
    public async Task ReportExport_RetainsJsonValidationErrors(string url, string errorCode)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken("manager-account", "manager@example.com", "CenterManager", DateTime.UtcNow.AddMinutes(10)));
        using HttpResponseMessage response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errorCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("revenue")]
    [InlineData("members")]
    [InlineData("classes")]
    public async Task ReportExport_AnonymousDenied(string report)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync($"/api/reports/{report}/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
