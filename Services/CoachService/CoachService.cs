using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel.Coach;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.CoachService;

public class CoachService : ICoachService
{
    private const string ActiveStatus = "Active";
    private const string InactiveStatus = "Inactive";

    private readonly SportsCenterManagementContext _context;

    public CoachService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        string trimmed = status.Trim();
        if (string.Equals(trimmed, ActiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return ActiveStatus;
        }

        if (string.Equals(trimmed, InactiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return InactiveStatus;
        }

        return null;
    }

    public async Task<PagedCoachResultAPIViewModel> GetCoachesAsync(
        int page,
        int pageSize,
        string? search,
        string? status)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0 || pageSize > 20)
        {
            pageSize = 20;
        }

        IQueryable<Coach> query = _context.Coaches
            .AsNoTracking()
            .Include(c => c.Account)
            .Where(c => c.Account.Role.Name == "Coach");

        if (!string.IsNullOrWhiteSpace(status))
        {
            string? normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus is null)
            {
                throw new ArgumentException("Status filter must be either 'Active' or 'Inactive'.", nameof(status));
            }

            query = query.Where(c => c.Account.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            query = query.Where(c =>
                c.FullName.Contains(trimmedSearch) ||
                c.Account.Email.Contains(trimmedSearch) ||
                (c.Account.Phone != null && c.Account.Phone.Contains(trimmedSearch)) ||
                (c.Specialization != null && c.Specialization.Contains(trimmedSearch)));
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

        List<CoachListItemAPIViewModel> items = await query
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CoachListItemAPIViewModel
            {
                AccountId = c.AccountId,
                Email = c.Account.Email,
                FullName = c.FullName,
                Phone = c.Account.Phone,
                Specialization = c.Specialization,
                WorkSchedule = c.WorkSchedule,
                Status = c.Account.Status,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return new PagedCoachResultAPIViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<CoachDetailAPIViewModel?> GetCoachByIdAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        Coach? coach = await _context.Coaches
            .AsNoTracking()
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.Account.Role.Name == "Coach");

        if (coach is null)
        {
            return null;
        }

        return new CoachDetailAPIViewModel
        {
            AccountId = coach.AccountId,
            Email = coach.Account.Email,
            FullName = coach.FullName,
            Phone = coach.Account.Phone,
            Specialization = coach.Specialization,
            WorkSchedule = coach.WorkSchedule,
            Status = coach.Account.Status,
            CreatedAt = coach.CreatedAt,
            UpdatedAt = coach.Account.UpdatedAt
        };
    }

    public async Task<bool> UpdateCoachAsync(string accountId, UpdateCoachAPIViewModel request)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return false;
        }

        Coach? coach = await _context.Coaches
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.Account.Role.Name == "Coach");

        if (coach is null)
        {
            return false;
        }

        string trimmedPhone = request.Phone.Trim();
        bool phoneExists = await _context.Accounts
            .AnyAsync(a => a.Id != accountId && a.Phone == trimmedPhone);

        if (phoneExists)
        {
            throw new InvalidOperationException("Phone number is already in use by another account.");
        }

        coach.FullName = request.FullName.Trim();
        coach.Specialization = string.IsNullOrWhiteSpace(request.Specialization) ? null : request.Specialization.Trim();
        coach.WorkSchedule = string.IsNullOrWhiteSpace(request.WorkSchedule) ? null : request.WorkSchedule.Trim();
        coach.Account.Phone = trimmedPhone;
        coach.Account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateCoachStatusAsync(string accountId, UpdateCoachStatusAPIViewModel request)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return false;
        }

        string? normalizedStatus = NormalizeStatus(request.Status);
        if (normalizedStatus is null)
        {
            throw new ArgumentException("Status must be either 'Active' or 'Inactive'.", nameof(request));
        }

        Account? account = await _context.Accounts
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.Role.Name == "Coach");

        if (account is null)
        {
            return false;
        }

        account.Status = normalizedStatus;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
