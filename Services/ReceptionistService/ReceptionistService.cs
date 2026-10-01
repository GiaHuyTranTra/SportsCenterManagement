using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.ReceptionistService;

public class ReceptionistService : IReceptionistService
{
    private const string ActiveStatus = "Active";
    private const string InactiveStatus = "Inactive";

    private readonly SportsCenterManagementContext _context;

    public ReceptionistService(SportsCenterManagementContext context)
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

    public async Task<PagedReceptionistResultAPIViewModel> GetReceptionistsAsync(
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

        IQueryable<Receptionist> query = _context.Receptionists
            .AsNoTracking()
            .Include(r => r.Account)
            .Where(r => r.Account.Role.Name == "Receptionist");

        if (!string.IsNullOrWhiteSpace(status))
        {
            string? normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus is null)
            {
                throw new ArgumentException("Status filter must be either 'Active' or 'Inactive'.", nameof(status));
            }

            query = query.Where(r => r.Account.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            query = query.Where(r =>
                r.FullName.Contains(trimmedSearch) ||
                r.Account.Email.Contains(trimmedSearch) ||
                (r.Account.Phone != null && r.Account.Phone.Contains(trimmedSearch)) ||
                (r.WorkShift != null && r.WorkShift.Contains(trimmedSearch)));
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

        List<ReceptionistListItemAPIViewModel> items = await query
            .OrderBy(r => r.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReceptionistListItemAPIViewModel
            {
                AccountId = r.AccountId,
                Email = r.Account.Email,
                FullName = r.FullName,
                Phone = r.Account.Phone,
                WorkShift = r.WorkShift,
                Status = r.Account.Status,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return new PagedReceptionistResultAPIViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<ReceptionistDetailAPIViewModel?> GetReceptionistByIdAsync(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        Receptionist? receptionist = await _context.Receptionists
            .AsNoTracking()
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.AccountId == accountId && r.Account.Role.Name == "Receptionist");

        if (receptionist is null)
        {
            return null;
        }

        return new ReceptionistDetailAPIViewModel
        {
            AccountId = receptionist.AccountId,
            Email = receptionist.Account.Email,
            FullName = receptionist.FullName,
            Phone = receptionist.Account.Phone,
            WorkShift = receptionist.WorkShift,
            Status = receptionist.Account.Status,
            CreatedAt = receptionist.CreatedAt,
            UpdatedAt = receptionist.Account.UpdatedAt
        };
    }

    public async Task<bool> UpdateReceptionistAsync(string accountId, UpdateReceptionistAPIViewModel request)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return false;
        }

        Receptionist? receptionist = await _context.Receptionists
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.AccountId == accountId && r.Account.Role.Name == "Receptionist");

        if (receptionist is null)
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

        receptionist.FullName = request.FullName.Trim();
        receptionist.WorkShift = string.IsNullOrWhiteSpace(request.WorkShift) ? null : request.WorkShift.Trim();
        receptionist.Account.Phone = trimmedPhone;
        receptionist.Account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateReceptionistStatusAsync(string accountId, UpdateReceptionistStatusAPIViewModel request)
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
            .FirstOrDefaultAsync(a => a.Id == accountId && a.Role.Name == "Receptionist");

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
