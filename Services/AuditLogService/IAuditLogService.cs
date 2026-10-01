using System;
using System.Threading.Tasks;
using APIViewModel.AuditLog;

namespace Services.AuditLogService;

public interface IAuditLogService
{
    Task<PagedAuditLogResultAPIViewModel> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? search,
        string? action,
        string? entityType,
        DateTime? fromDate,
        DateTime? toDate);

    Task RecordAsync(
        string? accountId,
        string action,
        string? entityType,
        string? entityId,
        string? description);
}
