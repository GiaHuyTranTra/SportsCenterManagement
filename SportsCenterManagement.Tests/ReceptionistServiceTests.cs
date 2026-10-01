using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.PasswordHashService;
using Services.ReceptionistService;

namespace SportsCenterManagement.Tests;

public class ReceptionistServiceTests
{
    [Fact]
    public async Task CreateReceptionistAsync_NormalizesHashesAndWritesAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role managerRole = new Role { Id = 1, Name = "CenterManager" };
        Role receptionistRole = new Role { Id = 4, Name = "Receptionist" };
        Account manager = CreateManager(managerRole);
        context.AddRange(managerRole, receptionistRole, manager);
        await context.SaveChangesAsync();
        PasswordHashService passwordHashService = new PasswordHashService();
        ReceptionistService service = new ReceptionistService(
            context,
            passwordHashService,
            new AuditLogService(context));

        (CreateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data) result =
            await service.CreateReceptionistAsync(
                manager.Id,
                new CreateReceptionistAPIViewModel
                {
                    Email = " COUNTER.NEW@EXAMPLE.COM ",
                    Password = "Password@123",
                    FullName = " Counter Staff ",
                    Phone = "0912345678",
                    WorkShift = " Morning "
                });

        Assert.Equal(CreateReceptionistResult.Success, result.Result);
        Assert.NotNull(result.Data);
        Account account = await context.Accounts
            .Include(item => item.Receptionist)
            .SingleAsync(item => item.Email == "counter.new@example.com");
        Assert.True(passwordHashService.VerifyPassword("Password@123", account.PasswordHash));
        Assert.Equal("Counter Staff", account.Receptionist!.FullName);
        Assert.Equal("Morning", account.Receptionist.WorkShift);
        Assert.Single(await context.AuditLogs
            .Where(item => item.EntityId == account.Id && item.Action == "CREATE")
            .ToListAsync());
    }

    [Fact]
    public async Task UpdateReceptionistAsync_UpdatesManagedFieldsAndWritesAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role managerRole = new Role { Id = 1, Name = "CenterManager" };
        Role receptionistRole = new Role { Id = 4, Name = "Receptionist" };
        Account manager = CreateManager(managerRole);
        Account receptionist = CreateReceptionist(receptionistRole);
        context.AddRange(managerRole, receptionistRole, manager, receptionist);
        await context.SaveChangesAsync();
        ReceptionistService service = CreateService(context);

        (UpdateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data) result =
            await service.UpdateReceptionistAsync(
                manager.Id,
                receptionist.Id,
                new UpdateManagedReceptionistAPIViewModel
                {
                    FullName = " Updated Staff ",
                    Phone = "0999999999",
                    WorkShift = " Evening "
                });

        Assert.Equal(UpdateReceptionistResult.Success, result.Result);
        Assert.NotNull(result.Data);
        Assert.Equal("Updated Staff", result.Data.FullName);
        Assert.Equal("0999999999", result.Data.Phone);
        Assert.Equal("Evening", result.Data.WorkShift);
        Assert.Single(await context.AuditLogs
            .Where(item => item.EntityId == receptionist.Id && item.Action == "UPDATE")
            .ToListAsync());
    }

    [Fact]
    public async Task SoftDeleteReceptionistAsync_ExcludesAccountFromList()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role managerRole = new Role { Id = 1, Name = "CenterManager" };
        Role receptionistRole = new Role { Id = 4, Name = "Receptionist" };
        Account manager = CreateManager(managerRole);
        Account receptionist = CreateReceptionist(receptionistRole);
        context.AddRange(managerRole, receptionistRole, manager, receptionist);
        await context.SaveChangesAsync();
        ReceptionistService service = CreateService(context);

        DeleteReceptionistResult deleteResult =
            await service.SoftDeleteReceptionistAsync(manager.Id, receptionist.Id);
        PagedReceptionistAPIViewModel list =
            await service.GetReceptionistsAsync(1, 20, null, null, null);

        Assert.Equal(DeleteReceptionistResult.Success, deleteResult);
        Assert.Empty(list.Items);
        Account persisted = await context.Accounts.SingleAsync(item => item.Id == receptionist.Id);
        Assert.NotNull(persisted.DeletedAt);
        Assert.Equal("Inactive", persisted.Status);
    }

    private static ReceptionistService CreateService(SportsCenterManagementContext context)
    {
        return new ReceptionistService(
            context,
            new PasswordHashService(),
            new AuditLogService(context));
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static Account CreateManager(Role role)
    {
        Account account = new Account
        {
            Id = "manager-1",
            Email = "manager@example.com",
            PasswordHash = "unused",
            Status = "Active",
            RoleId = role.Id,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        account.CenterManager = new CenterManager
        {
            AccountId = account.Id,
            Account = account,
            FullName = "Manager One",
            CreatedAt = DateTime.UtcNow
        };
        return account;
    }

    private static Account CreateReceptionist(Role role)
    {
        Account account = new Account
        {
            Id = "receptionist-1",
            Email = "receptionist.one@example.com",
            Phone = "0900000001",
            PasswordHash = "unused",
            Status = "Active",
            RoleId = role.Id,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        account.Receptionist = new Receptionist
        {
            AccountId = account.Id,
            Account = account,
            FullName = "Receptionist One",
            WorkShift = "Morning",
            CreatedAt = DateTime.UtcNow
        };
        return account;
    }
}
