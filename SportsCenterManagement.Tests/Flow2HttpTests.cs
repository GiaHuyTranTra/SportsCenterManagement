using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Fact]
    public async Task LegacyAvailableSchedule_RequiresAuthentication()
    {
        await using AuthenticationWebApplicationFactory factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ScheduleQuery/available")).StatusCode);
    }

    [Theory]
    [InlineData("account-1", "member@example.com", "Member", "/api/ClassSession/1/registrations")]
    [InlineData("coach-account", "coach@example.com", "Coach", "/api/ClassRegistration")]
    public async Task Flow2Queries_DenyUnrelatedRoles(string accountId, string email, string role, string route)
    {
        await using AuthenticationWebApplicationFactory factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, CreateToken(accountId, email, role, DateTime.UtcNow.AddMinutes(10)));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Flow2Http_ManagementThroughBookingAndCancellation_UsesConfirmedMembership()
    {
        await using AuthenticationWebApplicationFactory factory = new AuthenticationWebApplicationFactory();
        using HttpClient manager = CreateAuthenticatedClient(factory, CreateToken("manager-account", "manager@example.com", "CenterManager", DateTime.UtcNow.AddMinutes(10)));
        using HttpClient member = CreateAuthenticatedClient(factory, CreateToken("account-1", "member@example.com", "Member", DateTime.UtcNow.AddMinutes(10)));
        DateTime date = DateTime.UtcNow.AddHours(7).Date.AddDays(3);
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            SportsCenterManagementContext context = scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
            MembershipPackage package = new MembershipPackage { Name = "HTTP Package", Price = 1, Benefits = "[]", DurationMonths = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
            context.MemberSubscriptions.Add(new MemberSubscription
            {
                MemberId = "account-1", Package = package, PackageName = package.Name, PackagePrice = 1,
                Benefits = "[]", DurationMonths = 1, Kind = "REGISTER", Status = "CONFIRMED",
                StartDate = DateOnly.FromDateTime(date.AddDays(-1)), EndDate = DateOnly.FromDateTime(date.AddDays(1)),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
        int sportId = await CreatedIdAsync(manager, "/api/Sport", new { name = "Bơi lội", description = "HTTP sport" });
        int roomId = await CreatedIdAsync(manager, "/api/Room", new { name = "HTTP Room", capacity = 2, roomType = "POOL" });
        int classId = await CreatedIdAsync(manager, "/api/Class", new { sportId, name = "HTTP Class", capacity = 2, status = "DRAFT" });
        int sessionId = await CreatedIdAsync(manager, "/api/ClassSession", new
        {
            classId, coachId = "coach-account", roomId, date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00", endTime = "10:00", capacity = 2
        });
        HttpResponseMessage opened = await manager.PatchAsJsonAsync($"/api/Class/{classId}/status", new { status = "OPEN" });
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        int registrationId = await CreatedIdAsync(member, "/api/ClassRegistration/register", new { sessionId });
        HttpResponseMessage schedule = await member.GetAsync("/api/ClassRegistration/me?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        using JsonDocument page = JsonDocument.Parse(await schedule.Content.ReadAsStringAsync());
        Assert.Equal(1, page.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, page.RootElement.GetProperty("items")[0].GetProperty("session").GetProperty("bookedCount").GetInt32());
        HttpResponseMessage canceled = await member.PostAsJsonAsync($"/api/ClassRegistration/{registrationId}/cancel", new { reason = "Changed plan" });
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync($"/api/ClassRegistration/{registrationId}/cancel", new { })).StatusCode);
    }

    private static async Task<int> CreatedIdAsync<T>(HttpClient client, string route, T request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(route, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return result.RootElement.GetProperty("id").GetInt32();
    }
}
