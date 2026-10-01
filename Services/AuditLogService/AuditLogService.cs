using APIViewModel.AuditLog;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.AuditLogService;

public class AuditLogService : IAuditLogService
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    private readonly SportsCenterManagementContext _context;

    public AuditLogService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<PagedAuditLogAPIViewModel> GetAuditLogsAsync(
        int page,
        int pageSize,
        DateTime? from,
        DateTime? to,
        string? accountId,
        string? action,
        string? entityType)
    {
        int normalizedPage = page <= 0 ? 1 : page;
        int normalizedPageSize = pageSize <= 0 || pageSize > MaximumPageSize
            ? DefaultPageSize
            : pageSize;
        string? normalizedAccountId = NormalizeOptional(accountId);
        string? normalizedAction = NormalizeOptional(action);
        string? normalizedEntityType = NormalizeOptional(entityType);

        IQueryable<AuditLog> query = _context.AuditLogs
            .AsNoTracking()
            .Include(item => item.Account)
                .ThenInclude(account => account!.CenterManager)
            .Include(item => item.Account)
                .ThenInclude(account => account!.Coach)
            .Include(item => item.Account)
                .ThenInclude(account => account!.Member)
            .Include(item => item.Account)
                .ThenInclude(account => account!.Receptionist);

        if (from.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.CreatedAt <= to.Value);
        }

        if (normalizedAccountId is not null)
        {
            query = query.Where(item => item.AccountId == normalizedAccountId);
        }

        if (normalizedAction is not null)
        {
            query = query.Where(item => item.Action == normalizedAction);
        }

        if (normalizedEntityType is not null)
        {
            query = query.Where(item => item.EntityType == normalizedEntityType);
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / normalizedPageSize);

        List<AuditLog> auditLogs = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        List<AuditLogAPIViewModel> items = auditLogs
            .Select(MapAuditLog)
            .ToList();

        return new PagedAuditLogAPIViewModel
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task StageAuditLogAsync(
        string? accountId,
        string action,
        string? entityType,
        string? entityId,
        string? description)
    {
        string normalizedAction = NormalizeRequired(action, 100, nameof(action));
        string? normalizedAccountId = NormalizeBoundedOptional(accountId, 400, nameof(accountId));
        string? normalizedEntityType = NormalizeBoundedOptional(entityType, 100, nameof(entityType));
        string? normalizedEntityId = NormalizeBoundedOptional(entityId, 400, nameof(entityId));
        string? normalizedDescription = NormalizeBoundedOptional(description, 500, nameof(description));

        AuditLog auditLog = new AuditLog
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = normalizedAccountId,
            Action = normalizedAction,
            EntityType = normalizedEntityType,
            EntityId = normalizedEntityId,
            Description = normalizedDescription,
            CreatedAt = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(auditLog);
    }

    private static AuditLogAPIViewModel MapAuditLog(AuditLog auditLog)
    {
        Account? account = auditLog.Account;
        string? actorFullName = account?.CenterManager?.FullName
            ?? account?.Coach?.FullName
            ?? account?.Member?.FullName
            ?? account?.Receptionist?.FullName;

        return new AuditLogAPIViewModel
        {
            Id = auditLog.Id,
            AccountId = auditLog.AccountId,
            ActorEmail = account?.Email,
            ActorFullName = actorFullName,
            Action = auditLog.Action,
            EntityType = auditLog.EntityType,
            EntityId = auditLog.EntityId,
            Description = auditLog.Description,
            CreatedAt = auditLog.CreatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException("The value exceeds the allowed length.", parameterName);
        }

        return normalizedValue;
    }

    private static string? NormalizeBoundedOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        string? normalizedValue = NormalizeOptional(value);
        if (normalizedValue is not null && normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException("The value exceeds the allowed length.", parameterName);
        }

        return normalizedValue;
    }
}
