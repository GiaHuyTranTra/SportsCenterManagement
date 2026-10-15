using APIViewModel.Discipline;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.DisciplineService;

namespace SportsCenterManagement.Tests;

public class DisciplineServiceTests
{
    [Fact]
    public async Task CreateDisciplineAsync_NormalizesAndWritesAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account manager = CreateManager();
        context.Add(manager);
        await context.SaveChangesAsync();
        DisciplineService service = new DisciplineService(context, new AuditLogService(context));

        (CreateDisciplineResult Result, DisciplineAPIViewModel? Data) result =
            await service.CreateDisciplineAsync(
                manager.Id,
                new CreateDisciplineAPIViewModel
                {
                    Name = "  Pilates  ",
                    Description = "  Core training  "
                });

        Assert.Equal(CreateDisciplineResult.Success, result.Result);
        Assert.NotNull(result.Data);
        Assert.Equal("Pilates", result.Data.Name);
        Assert.Equal("Core training", result.Data.Description);
        AuditLog auditLog = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal(result.Data.Id.ToString(), auditLog.EntityId);
    }

    [Fact]
    public async Task CreateDisciplineAsync_WithDuplicateName_ReturnsDuplicateName()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account manager = CreateManager();
        context.Add(manager);
        context.Disciplines.Add(new Discipline
        {
            Id = 1,
            Name = "Fitness",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        DisciplineService service = new DisciplineService(context, new AuditLogService(context));

        (CreateDisciplineResult Result, DisciplineAPIViewModel? Data) result =
            await service.CreateDisciplineAsync(
                manager.Id,
                new CreateDisciplineAPIViewModel { Name = "Fitness" });

        Assert.Equal(CreateDisciplineResult.DuplicateName, result.Result);
        Assert.Null(result.Data);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task GetSportsPagedAsync_FiltersAndPaginatesCorrectly()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Disciplines.AddRange(
            new Discipline { Id = 1, Name = "Yoga", Description = "Gentle yoga", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Discipline { Id = 2, Name = "Pilates", Description = "Mat pilates", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Discipline { Id = 3, Name = "Boxing", Description = "Fitness boxing", IsActive = false, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        DisciplineService service = new DisciplineService(context, new AuditLogService(context));

        APIViewModel.PagedResult<DisciplineAPIViewModel> activeResult = await service.GetSportsPagedAsync(1, 10, null, true);
        Assert.Equal(2, activeResult.TotalItems);
        Assert.Equal(2, activeResult.Items.Count);

        APIViewModel.PagedResult<DisciplineAPIViewModel> searchResult = await service.GetSportsPagedAsync(1, 10, "Box", null);
        Assert.Equal(1, searchResult.TotalItems);
        Assert.Equal("Boxing", searchResult.Items[0].Name);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static Account CreateManager()
    {
        Role role = new Role { Id = 1, Name = "CenterManager" };
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
}
