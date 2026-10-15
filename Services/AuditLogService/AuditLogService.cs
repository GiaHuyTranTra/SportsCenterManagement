using Services.ClassScheduling;
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
        string? entityType,
        string? entityId = null, int? classId = null, int? sessionId = null, string? search = null)
    {
        int normalizedPage = page <= 0 ? 1 : page;
        int normalizedPageSize = pageSize <= 0 || pageSize > MaximumPageSize
            ? DefaultPageSize
            : pageSize;
        string? normalizedAccountId = NormalizeOptional(accountId);
        string? normalizedAction = NormalizeOptional(action);
        string? normalizedEntityType = NormalizeOptional(entityType);
        string? normalizedEntityId = NormalizeOptional(entityId);

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

        if (normalizedEntityId is not null)
        {
            query = query.Where(item => item.EntityId == normalizedEntityId);
        }

        if (classId.HasValue) query = query.Where(item => item.ClassId == classId.Value);
        if (sessionId.HasValue) query = query.Where(item => item.SessionId == sessionId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            query = query.Where(item => item.Action.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)));
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

        int? relatedSessionId = null;
        if (entityType == "ClassRegistration" && int.TryParse(entityId, out int registrationId))
            relatedSessionId = await _context.ClassRegistrations.Where(item => item.Id == registrationId)
                .Select(item => (int?)item.SessionId).FirstOrDefaultAsync();
        if (entityType == "ClassSession" && int.TryParse(entityId, out int parsedSessionId)) relatedSessionId = parsedSessionId;
        int? relatedClassId = relatedSessionId.HasValue
            ? await _context.ClassSessions.Where(item => item.Id == relatedSessionId.Value).Select(item => (int?)item.ClassId).FirstOrDefaultAsync()
            : null;
        AuditLog auditLog = new AuditLog
        {
            OperationId = Flow2WriteScope.Current(_context)?.OperationId,
            ClassId = entityType == "Class" && int.TryParse(entityId, out int classId) ? classId : relatedClassId,
            SessionId = relatedSessionId,
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
