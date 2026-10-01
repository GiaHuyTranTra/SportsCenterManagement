using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel.AuditLog;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.AuditLogService;

public class AuditLogService : IAuditLogService
{
    private readonly SportsCenterManagementContext _context;

    public AuditLogService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<PagedAuditLogResultAPIViewModel> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? search,
        string? action,
        string? entityType,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0 || pageSize > 50)
        {
            pageSize = 20;
        }

        IQueryable<AuditLog> query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.Account)
                .ThenInclude(acc => acc!.CenterManager)
            .Include(a => a.Account)
                .ThenInclude(acc => acc!.Coach)
            .Include(a => a.Account)
                .ThenInclude(acc => acc!.Receptionist)
            .Include(a => a.Account)
                .ThenInclude(acc => acc!.Member);

        if (!string.IsNullOrWhiteSpace(action))
        {
            string trimmedAction = action.Trim();
            query = query.Where(a => a.Action == trimmedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            string trimmedEntity = entityType.Trim();
            query = query.Where(a => a.EntityType == trimmedEntity);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            DateTime endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.CreatedAt <= endOfDay);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            query = query.Where(a =>
                (a.Description != null && a.Description.Contains(trimmedSearch)) ||
                (a.EntityId != null && a.EntityId.Contains(trimmedSearch)) ||
                a.Action.Contains(trimmedSearch) ||
                (a.Account != null && a.Account.Email.Contains(trimmedSearch)));
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

        List<AuditLogListItemAPIViewModel> items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogListItemAPIViewModel
            {
                Id = a.Id,
                AccountId = a.AccountId,
                AccountName = a.Account != null
                    ? (a.Account.CenterManager != null ? a.Account.CenterManager.FullName
                        : a.Account.Coach != null ? a.Account.Coach.FullName
                        : a.Account.Receptionist != null ? a.Account.Receptionist.FullName
                        : a.Account.Member != null ? a.Account.Member.FullName
                        : a.Account.Email)
                    : "System",
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Description = a.Description,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return new PagedAuditLogResultAPIViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task RecordAsync(
        string? accountId,
        string action,
        string? entityType,
        string? entityId,
        string? description)
    {
        AuditLog entry = new AuditLog
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            Action = action.Trim(),
            EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim(),
            EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();
    }
}
