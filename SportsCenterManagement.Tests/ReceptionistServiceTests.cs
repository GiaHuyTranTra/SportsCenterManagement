using System;
using System.Threading.Tasks;
using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ReceptionistService;
using Xunit;

namespace SportsCenterManagement.Tests;

public class ReceptionistServiceTests
{
    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options = new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SportsCenterManagementContext(options);
    }

    private static (Account Account, Receptionist Receptionist) CreateReceptionistData(string accountId, string email, string phone, string status = "Active")
    {
        Role role = new Role { Id = 4, Name = "Receptionist" };
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
        Receptionist receptionist = new Receptionist
        {
            AccountId = accountId,
            FullName = "Lễ tân Test",
            WorkShift = "Ca sáng",
            Account = account
        };
        return (account, receptionist);
    }

    [Fact]
    public async Task GetReceptionistsAsync_ReturnsPagedReceptionists()
    {
        await using SportsCenterManagementContext context = CreateContext();
        (Account account, Receptionist receptionist) = CreateReceptionistData("r1", "recept1@example.com", "0901112233");
        context.Accounts.Add(account);
        context.Receptionists.Add(receptionist);
        await context.SaveChangesAsync();

        ReceptionistService service = new ReceptionistService(context);
        PagedReceptionistResultAPIViewModel result = await service.GetReceptionistsAsync(1, 10, null, null);

        Assert.Single(result.Items);
        Assert.Equal("recept1@example.com", result.Items[0].Email);
        Assert.Equal("Lễ tân Test", result.Items[0].FullName);
    }

    [Fact]
    public async Task UpdateReceptionistAsync_UpdatesFieldsSuccessfully()
    {
        await using SportsCenterManagementContext context = CreateContext();
        (Account account, Receptionist receptionist) = CreateReceptionistData("r1", "recept1@example.com", "0901112233");
        context.Accounts.Add(account);
        context.Receptionists.Add(receptionist);
        await context.SaveChangesAsync();

        ReceptionistService service = new ReceptionistService(context);
        bool updated = await service.UpdateReceptionistAsync("r1", new UpdateReceptionistAPIViewModel
        {
            FullName = "Lễ tân Updated",
            Phone = "0908887766",
            WorkShift = "Ca chiều"
        });

        Assert.True(updated);
        Receptionist? updatedRecept = await context.Receptionists.Include(r => r.Account).FirstOrDefaultAsync(r => r.AccountId == "r1");
        Assert.NotNull(updatedRecept);
        Assert.Equal("Lễ tân Updated", updatedRecept.FullName);
        Assert.Equal("0908887766", updatedRecept.Account.Phone);
        Assert.Equal("Ca chiều", updatedRecept.WorkShift);
    }

    [Fact]
    public async Task UpdateReceptionistStatusAsync_ChangesStatus()
    {
        await using SportsCenterManagementContext context = CreateContext();
        (Account account, Receptionist receptionist) = CreateReceptionistData("r1", "recept1@example.com", "0901112233", "Active");
        context.Accounts.Add(account);
        context.Receptionists.Add(receptionist);
        await context.SaveChangesAsync();

        ReceptionistService service = new ReceptionistService(context);
        bool updated = await service.UpdateReceptionistStatusAsync("r1", new UpdateReceptionistStatusAPIViewModel { Status = "Inactive" });

        Assert.True(updated);
        Account? updatedAccount = await context.Accounts.FindAsync("r1");
        Assert.NotNull(updatedAccount);
        Assert.Equal("Inactive", updatedAccount.Status);
    }
}
