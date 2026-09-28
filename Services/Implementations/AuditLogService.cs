using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using SportsCenterManagement.DTOs.AuditLog;

namespace Services.Implementations;

public class AuditLogService : IAuditLogService
{
	private readonly SportsCenterManagementContext _context;

	public AuditLogService(SportsCenterManagementContext context) => _context = context;

	public async Task LogAsync(string accountId, string action, string entityType, string entityId, string details, string ipAddress)
	{
		_context.AuditLogs.Add(new DataAccess.Entities.AuditLog
		{
			Id = Guid.NewGuid().ToString(), AccountId = accountId, Action = action,
			EntityType = entityType, EntityId = entityId, Description = $"{details} | IP: {ipAddress}", CreatedAt = DateTime.UtcNow
		});
		await _context.SaveChangesAsync();
	}

	public async Task<object> GetLogsAsync(AuditLogQueryDto query)
	{
		var logsQuery = _context.AuditLogs.AsNoTracking().AsQueryable();
		if (!string.IsNullOrWhiteSpace(query.AccountId)) logsQuery = logsQuery.Where(log => log.AccountId == query.AccountId);
		if (!string.IsNullOrWhiteSpace(query.Action)) logsQuery = logsQuery.Where(log => log.Action == query.Action);
		if (query.FromDate.HasValue) logsQuery = logsQuery.Where(log => log.CreatedAt >= query.FromDate.Value);
		if (query.ToDate.HasValue) logsQuery = logsQuery.Where(log => log.CreatedAt <= query.ToDate.Value);
		var pageIndex = Math.Max(1, query.PageIndex);
		var pageSize = Math.Clamp(query.PageSize, 1, 100);
		var totalItems = await logsQuery.CountAsync();
		var items = await logsQuery.OrderByDescending(log => log.CreatedAt).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
		return new { TotalItems = totalItems, PageIndex = pageIndex, PageSize = pageSize, TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize), Items = items };
	}
}
