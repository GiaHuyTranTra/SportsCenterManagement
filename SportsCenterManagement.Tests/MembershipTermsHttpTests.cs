using System.Net;
using System.Net.Http.Json;
using APIViewModel.MembershipTerms;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Fact]
    public async Task MembershipTerms_MemberCanReadAndAcceptCurrentVersion()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken("account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));

        CurrentMembershipTermsAPIViewModel? terms = await client.GetFromJsonAsync<CurrentMembershipTermsAPIViewModel>(
            "/api/MembershipTerms/current");
        Assert.NotNull(terms);
        Assert.False(terms.Accepted);

        using HttpResponseMessage accepted = await client.PostAsJsonAsync(
            "/api/MembershipTerms/current/accept",
            new AcceptMembershipTermsAPIViewModel { Version = terms.Version, ContentHash = terms.ContentHash });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        MemberTermsStatusAPIViewModel? status =
            await accepted.Content.ReadFromJsonAsync<MemberTermsStatusAPIViewModel>();
        Assert.True(status!.Accepted);
        Assert.Equal("account-1", status.MemberId);
    }

    [Theory]
    [InlineData("manager-account", "manager@example.com", "CenterManager")]
    [InlineData("receptionist-account", "receptionist@example.com", "Receptionist")]
    public async Task MembershipTerms_StaffCannotAcceptForMember(string accountId, string email, string role)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, email, role, DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/MembershipTerms/current/accept",
            new AcceptMembershipTermsAPIViewModel { Version = "membership-terms-v1", ContentHash = new string('0', 64) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("manager-account", "manager@example.com", "CenterManager")]
    [InlineData("receptionist-account", "receptionist@example.com", "Receptionist")]
    public async Task MembershipTerms_StaffCanReadAcceptanceSummary(string accountId, string email, string role)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, email, role, DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync("/api/MembershipTerms/members/account-1/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"memberId\":\"account-1\"", body);
        Assert.DoesNotContain("sections", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contentHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MembershipTerms_MemberCannotReadStaffSummaryRoute()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken("account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.GetAsync("/api/MembershipTerms/members/account-1/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MembershipTerms_StaleVersionReturnsStableConflictCode()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken("account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/MembershipTerms/current/accept",
            new AcceptMembershipTermsAPIViewModel { Version = "stale-version", ContentHash = new string('0', 64) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("TERMS_VERSION_MISMATCH", await response.Content.ReadAsStringAsync());
    }
}
