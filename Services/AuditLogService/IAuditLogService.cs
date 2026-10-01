using APIViewModel.AuditLog;

namespace Services.AuditLogService;

public interface IAuditLogService
{
    Task<PagedAuditLogAPIViewModel> GetAuditLogsAsync(
        int page,
        int pageSize,
        DateTime? from,
        DateTime? to,
        string? accountId,
        string? action,
        string? entityType);

    Task StageAuditLogAsync(
        string? accountId,
        string action,
        string? entityType,
        string? entityId,
        string? description);
}
