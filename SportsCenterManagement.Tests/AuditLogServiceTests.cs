using System;
using System.Threading.Tasks;
using APIViewModel.AuditLog;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Xunit;

namespace SportsCenterManagement.Tests;

public class AuditLogServiceTests
{
    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options = new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SportsCenterManagementContext(options);
    }

    [Fact]
    public async Task RecordAsync_CreatesAuditLogSuccessfully()
    {
        await using SportsCenterManagementContext context = CreateContext();
        AuditLogService service = new AuditLogService(context);

        await service.RecordAsync("acc-123", "CREATE", "Member", "mem-456", "Tạo mới thành viên");

        AuditLog? log = await context.AuditLogs.FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal("acc-123", log.AccountId);
        Assert.Equal("CREATE", log.Action);
        Assert.Equal("Member", log.EntityType);
        Assert.Equal("mem-456", log.EntityId);
        Assert.Equal("Tạo mới thành viên", log.Description);
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsPagedFilteredLogs()
    {
        await using SportsCenterManagementContext context = CreateContext();
        AuditLogService service = new AuditLogService(context);

        await service.RecordAsync("acc-1", "CREATE", "Coach", "c-1", "Tạo HLV 1");
        await service.RecordAsync("acc-2", "UPDATE", "Coach", "c-1", "Cập nhật HLV 1");
        await service.RecordAsync("acc-3", "DELETE", "Package", "p-1", "Xóa gói tập");

        PagedAuditLogResultAPIViewModel result = await service.GetAuditLogsAsync(1, 10, null, "CREATE", null, null, null);

        Assert.Single(result.Items);
        Assert.Equal("CREATE", result.Items[0].Action);
        Assert.Equal("Coach", result.Items[0].EntityType);
    }
}
