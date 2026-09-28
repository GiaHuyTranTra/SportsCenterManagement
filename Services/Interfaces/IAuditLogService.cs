using SportsCenterManagement.DTOs.AuditLog;

namespace Services.Interfaces;

public interface IAuditLogService
{
	Task LogAsync(string accountId, string action, string entityType, string entityId, string details, string ipAddress);
	Task<object> GetLogsAsync(AuditLogQueryDto query);
}
