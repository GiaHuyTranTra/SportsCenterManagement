using APIViewModel.AuditLog;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;

namespace SportsCenterManagement.Tests;

public class AuditLogServiceTests
{
    [Fact]
    public async Task GetAuditLogsAsync_FiltersAndOrdersResults()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account manager = CreateManager();
        context.Accounts.Add(manager);
        context.AuditLogs.AddRange(
            CreateAuditLog("log-1", manager.Id, "UPDATE", "Member", "member-1", new DateTime(2026, 9, 1)),
            CreateAuditLog("log-2", manager.Id, "UPDATE", "Member", "member-2", new DateTime(2026, 9, 2)),
            CreateAuditLog("log-3", manager.Id, "CREATE", "Coach", "coach-1", new DateTime(2026, 9, 3)));
        await context.SaveChangesAsync();
        AuditLogService service = new AuditLogService(context);

        PagedAuditLogAPIViewModel result = await service.GetAuditLogsAsync(
            1,
            20,
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 2, 23, 59, 59),
            manager.Id,
            "UPDATE",
            "Member");

        Assert.Equal(2, result.TotalItems);
        Assert.Equal("log-2", result.Items[0].Id);
        Assert.Equal("Manager One", result.Items[0].ActorFullName);
        Assert.Equal("manager@example.com", result.Items[0].ActorEmail);
        Assert.Equal("log-1", result.Items[1].Id);
    }

    [Fact]
    public async Task StageAuditLogAsync_NormalizesAndStagesWithoutSaving()
    {
        await using SportsCenterManagementContext context = CreateContext();
        AuditLogService service = new AuditLogService(context);

        await service.StageAuditLogAsync(
            null,
            "  CREATE  ",
            "  Coach  ",
            "  coach-1  ",
            "  Created coach  ");

        AuditLog? stagedLog = context.ChangeTracker
            .Entries<AuditLog>()
            .Select(entry => entry.Entity)
            .SingleOrDefault();
        Assert.NotNull(stagedLog);
        Assert.Equal("CREATE", stagedLog.Action);
        Assert.Equal("Coach", stagedLog.EntityType);
        Assert.Equal("coach-1", stagedLog.EntityId);
        Assert.Equal("Created coach", stagedLog.Description);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
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
        Role role = new Role
        {
            Id = 1,
            Name = "CenterManager"
        };
        return new Account
        {
            Id = "manager-1",
            Email = "manager@example.com",
            PasswordHash = "unused",
            RoleId = role.Id,
            Role = role,
            Status = "Active",
            CreatedAt = new DateTime(2026, 9, 1),
            CenterManager = new CenterManager
            {
                AccountId = "manager-1",
                FullName = "Manager One",
                CreatedAt = new DateTime(2026, 9, 1)
            }
        };
    }

    private static AuditLog CreateAuditLog(
        string id,
        string accountId,
        string action,
        string entityType,
        string entityId,
        DateTime createdAt)
    {
        return new AuditLog
        {
            Id = id,
            AccountId = accountId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = createdAt
        };
    }
}
