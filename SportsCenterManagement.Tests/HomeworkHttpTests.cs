using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APIViewModel.Homework;
using DataAccess.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.Controllers;

namespace SportsCenterManagement.Tests;

public sealed class HomeworkHttpTests
{
    [Fact]
    public async Task CoachCanAssignAndReadHistory_AndMemberReadsOnlySelf()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        await SeedRelationshipAndOtherMemberAsync(factory);
        using HttpClient coach = Client(factory, "coach-account", "coach@example.com", "Coach");

        using HttpResponseMessage post = await coach.PostAsJsonAsync("/api/Homework", new
        {
            targetType = "MEMBER",
            memberId = "account-1",
            title = "Plank cơ bản",
            instructions = "Thực hiện 3 hiệp plank."
        });

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        HomeworkAssignmentAPIViewModel[]? created =
            await post.Content.ReadFromJsonAsync<HomeworkAssignmentAPIViewModel[]>();
        Assert.Equal("coach-account", Assert.Single(created!).CoachId);

        HomeworkAssignmentAPIViewModel[]? coachHistory =
            await coach.GetFromJsonAsync<HomeworkAssignmentAPIViewModel[]>("/api/Homework/coach/me");
        Assert.Contains(coachHistory!, item => item.MemberId == "account-1" && item.Title == "Plank cơ bản");
        Assert.Contains(coachHistory!, item => item.MemberId == "other-member");

        using HttpClient member = Client(factory, "account-1", "member@example.com", "Member");
        HomeworkAssignmentAPIViewModel[]? memberHistory =
            await member.GetFromJsonAsync<HomeworkAssignmentAPIViewModel[]>(
                "/api/Homework/member/me?memberId=other-member");
        Assert.All(memberHistory!, item => Assert.Equal("account-1", item.MemberId));
    }

    [Fact]
    public async Task RoleBoundaries_BlockMemberWritesAndCoachMemberHistory()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient member = Client(factory, "account-1", "member@example.com", "Member");
        using HttpClient coach = Client(factory, "coach-account", "coach@example.com", "Coach");

        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/Homework", new
        {
            targetType = "MEMBER", memberId = "account-1", title = "Title", instructions = "Instructions"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await coach.GetAsync("/api/Homework/member/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.GetAsync("/api/Homework/coach/me")).StatusCode);
    }

    [Fact]
    public async Task InvalidAndMissingTargets_ReturnStableErrorCodes()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient coach = Client(factory, "coach-account", "coach@example.com", "Coach");

        using HttpResponseMessage invalid = await coach.PostAsJsonAsync("/api/Homework", new
        {
            targetType = "MEMBER", memberId = "account-1", classId = 7,
            title = "Title", instructions = "Instructions"
        });
        using HttpResponseMessage missing = await coach.PostAsJsonAsync("/api/Homework", new
        {
            targetType = "MEMBER", memberId = "missing",
            title = "Title", instructions = "Instructions"
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("INVALID_HOMEWORK_TARGET", await invalid.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("HOMEWORK_TARGET_NOT_FOUND", await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LegacyNoticeRoute_IsGone()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        using HttpClient coach = Client(factory, "coach-account", "coach@example.com", "Coach");

        HttpResponseMessage response = await coach.PostAsJsonAsync(
            "/api/TrainingAndOperations/coach/homework-notice",
            new { memberId = "account-1", title = "Old", body = "Old path" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task TrainingContractsAndWorkspaceDoNotExposeLegacyHomeworkFields()
    {
        Assert.Null(typeof(CreateTrainingPlanRequest).GetProperty("Homework"));
        Assert.Null(typeof(CreateTrainingResultRequest).GetProperty("HomeworkAssigned"));

        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        await SeedLegacyTrainingRowsAsync(factory);
        using HttpClient member = Client(factory, "account-1", "member@example.com", "Member");

        string json = await member.GetStringAsync("/api/TrainingAndOperations/member/workspace");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement plan = document.RootElement.GetProperty("plans")[0];
        JsonElement result = document.RootElement.GetProperty("results")[0];
        Assert.False(plan.TryGetProperty("homework", out _));
        Assert.False(result.TryGetProperty("homeworkAssigned", out _));
    }

    [Fact]
    public async Task LegacyHomeworkInput_IsIgnoredAndNeverWrittenByPlanEndpoint()
    {
        await using WebApplicationFactory<Program> factory =
            new AuthenticationIntegrationTests.AuthenticationWebApplicationFactory();
        await SeedRelationshipAndOtherMemberAsync(factory);
        using HttpClient coach = Client(factory, "coach-account", "coach@example.com", "Coach");

        using HttpResponseMessage response = await coach.PostAsJsonAsync(
            "/api/TrainingAndOperations/coach/plans",
            new
            {
                planType = "INDIVIDUAL",
                memberId = "account-1",
                title = "Kế hoạch mới",
                goal = "Cải thiện sức bền",
                exercises = new[] { new { name = "Plank", sets = 3, reps = "30 giây", durationMinutes = 2 } },
                homework = "Trường cũ phải bị bỏ qua",
                startDate = "2026-11-12",
                endDate = "2026-11-19"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using IServiceScope scope = factory.Services.CreateScope();
        SportsCenterManagementContext context =
            scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
        TrainingPlan newest = await context.TrainingPlans.OrderByDescending(item => item.Id).FirstAsync();
        Assert.Null(newest.Homework);
    }

    private static HttpClient Client(
        WebApplicationFactory<Program> factory,
        string id,
        string email,
        string role) => AuthenticationIntegrationTests.CreateAuthenticatedClient(
            factory,
            AuthenticationIntegrationTests.CreateToken(
                id, email, role, DateTime.UtcNow.AddMinutes(10)));

    private static async Task SeedRelationshipAndOtherMemberAsync(WebApplicationFactory<Program> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SportsCenterManagementContext context =
            scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
        context.TrainingPlans.Add(Plan("account-1", homework: null));

        Role memberRole = await context.Roles.SingleAsync(item => item.Name == "Member");
        Account otherAccount = new()
        {
            Id = "other-member", Email = "other@example.com", PasswordHash = "x",
            Status = "Active", RoleId = memberRole.Id, CreatedAt = DateTime.UtcNow
        };
        context.Accounts.Add(otherAccount);
        context.Members.Add(new Member
        {
            AccountId = otherAccount.Id, MemberCode = "MEM002",
            FullName = "Other Member", CreatedAt = DateTime.UtcNow
        });
        context.HomeworkAssignments.Add(new HomeworkAssignment
        {
            CoachId = "coach-account", MemberId = "other-member", Title = "Private other",
            Instructions = "Must not leak", SourceType = "DIRECT", AssignedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedLegacyTrainingRowsAsync(WebApplicationFactory<Program> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SportsCenterManagementContext context =
            scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
        TrainingPlan plan = Plan("account-1", "legacy plan homework");
        context.TrainingPlans.Add(plan);
        await context.SaveChangesAsync();
        context.TrainingResults.Add(new TrainingResult
        {
            CoachId = "coach-account", MemberId = "account-1", PlanId = plan.Id,
            RecordedDate = new DateOnly(2026, 11, 12), CompletedExercises = "Plank",
            CompletionRate = 100, ProgressRating = "ON_TRACK", ResultSummary = "Good",
            CoachFeedback = "Keep going", HomeworkAssigned = "legacy result homework",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static TrainingPlan Plan(string memberId, string? homework) => new()
    {
        CoachId = "coach-account", PlanType = "INDIVIDUAL", MemberId = memberId,
        Title = "Kế hoạch test", Goal = "Cải thiện thể lực", ExercisesJson = "[]",
        Homework = homework, StartDate = new DateOnly(2026, 11, 12),
        EndDate = new DateOnly(2026, 11, 19), CreatedAt = DateTime.UtcNow
    };
}
