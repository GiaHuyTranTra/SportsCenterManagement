using System;
using System.Threading.Tasks;
using APIViewModel.Coach;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.CoachService;
using Xunit;

namespace SportsCenterManagement.Tests;

public class CoachServiceTests
{
    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options = new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SportsCenterManagementContext(options);
    }

    private static (Account Account, Coach Coach) CreateCoachData(string accountId, string email, string phone, string status = "Active")
    {
        Role role = new Role { Id = 2, Name = "Coach" };
        Account account = new Account
        {
            Id = accountId,
            Email = email,
            Phone = phone,
            RoleId = role.Id,
            Role = role,
            Status = status,
            PasswordHash = "hash"
        };
        Coach coach = new Coach
        {
            AccountId = accountId,
            FullName = "HLV Test",
            Specialization = "Gym",
            WorkSchedule = "Full-time",
            Account = account
        };
        return (account, coach);
    }

    [Fact]
    public async Task GetCoachesAsync_ReturnsPagedCoaches()
    {
        await using SportsCenterManagementContext context = CreateContext();
        (Account account, Coach coach) = CreateCoachData("c1", "coach1@example.com", "0901234567");
        context.Accounts.Add(account);
        context.Coaches.Add(coach);
        await context.SaveChangesAsync();

        CoachService service = new CoachService(context);
        PagedCoachResultAPIViewModel result = await service.GetCoachesAsync(1, 10, null, null);

        Assert.Single(result.Items);
        Assert.Equal("coach1@example.com", result.Items[0].Email);
        Assert.Equal("HLV Test", result.Items[0].FullName);
    }

    [Fact]
    public async Task UpdateCoachAsync_UpdatesFieldsSuccessfully()
    {
        await using SportsCenterManagementContext context = CreateContext();
        (Account account, Coach coach) = CreateCoachData("c1", "coach1@example.com", "0901234567");
        context.Accounts.Add(account);
        context.Coaches.Add(coach);
        await context.SaveChangesAsync();

        CoachService service = new CoachService(context);
        bool updated = await service.UpdateCoachAsync("c1", new UpdateCoachAPIViewModel
        {
            FullName = "HLV Updated",
            Phone = "0909999999",
            Specialization = "Yoga",
            WorkSchedule = "Part-time"
        });

        Assert.True(updated);
        Coach? updatedCoach = await context.Coaches.Include(c => c.Account).FirstOrDefaultAsync(c => c.AccountId == "c1");
        Assert.NotNull(updatedCoach);
        Assert.Equal("HLV Updated", updatedCoach.FullName);
        Assert.Equal("0909999999", updatedCoach.Account.Phone);
        Assert.Equal("Yoga", updatedCoach.Specialization);
    }

    [Fact]
    public async Task UpdateCoachStatusAsync_ChangesStatus()
    {
        await using SportsCenterManagementContext context = CreateContext();
        (Account account, Coach coach) = CreateCoachData("c1", "coach1@example.com", "0901234567", "Active");
        context.Accounts.Add(account);
        context.Coaches.Add(coach);
        await context.SaveChangesAsync();

        CoachService service = new CoachService(context);
        bool updated = await service.UpdateCoachStatusAsync("c1", new UpdateCoachStatusAPIViewModel { Status = "Inactive" });

        Assert.True(updated);
        Account? updatedAccount = await context.Accounts.FindAsync("c1");
        Assert.NotNull(updatedAccount);
        Assert.Equal("Inactive", updatedAccount.Status);
    }
}
