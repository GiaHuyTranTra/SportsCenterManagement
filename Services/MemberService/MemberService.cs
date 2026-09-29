using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel.Member;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.MemberService;

public class MemberService : IMemberService
{
    private const string ActiveStatus = "Active";
    private const string InactiveStatus = "Inactive";

    private readonly SportsCenterManagementContext _context;

    public MemberService(SportsCenterManagementContext context)
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

    private IQueryable<Member> BuildMemberQuery()
    {
        return _context.Members
            .AsNoTracking()
            .Where(m => m.Account.Role.Name == "Member");
    }

    private IQueryable<Member> ApplyKeywordFilter(
        IQueryable<Member> query,
        string? keyword,
        bool includeMemberCode)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        string trimmedKeyword = keyword.Trim();

        if (includeMemberCode)
        {
            return query.Where(m =>
                (m.FullName != null && m.FullName.Contains(trimmedKeyword)) ||
                m.MemberCode.Contains(trimmedKeyword) ||
                m.Account.Email.Contains(trimmedKeyword) ||
                (m.Account.Phone != null && m.Account.Phone.Contains(trimmedKeyword)));
        }

        return query.Where(m =>
            (m.FullName != null && m.FullName.Contains(trimmedKeyword)) ||
            m.Account.Email.Contains(trimmedKeyword) ||
            (m.Account.Phone != null && m.Account.Phone.Contains(trimmedKeyword)));
    }

    public async Task<PagedMemberResultAPIViewModel> GetMembersAsync(
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

        IQueryable<Member> query = BuildMemberQuery();

        if (!string.IsNullOrWhiteSpace(status))
        {
            string? normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus is null)
            {
                throw new ArgumentException("Status filter must be either 'Active' or 'Inactive'.", nameof(status));
            }

            query = query.Where(m => m.Account.Status == normalizedStatus);
        }

        query = ApplyKeywordFilter(query, search, includeMemberCode: false);

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

        List<MemberListItemAPIViewModel> items = await query
            .OrderBy(m => m.MemberCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MemberListItemAPIViewModel
            {
                AccountId = m.AccountId,
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                Email = m.Account.Email,
                Phone = m.Account.Phone,
                Status = m.Account.Status,
                DateOfBirth = m.DateOfBirth,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        PagedMemberResultAPIViewModel result = new PagedMemberResultAPIViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };

        return result;
    }

    public async Task<MemberDetailAPIViewModel?> GetMemberByIdAsync(string accountId)
    {
        Member? member = await _context.Members
            .AsNoTracking()
            .Include(m => m.Account)
            .ThenInclude(a => a.Role)
            .Where(m => m.AccountId == accountId && m.Account.Role.Name == "Member")
            .FirstOrDefaultAsync();

        if (member is null)
        {
            return null;
        }

        MemberDetailAPIViewModel detail = new MemberDetailAPIViewModel
        {
            AccountId = member.AccountId,
            MemberCode = member.MemberCode,
            FullName = member.FullName,
            DateOfBirth = member.DateOfBirth,
            AvatarUrl = member.AvatarUrl,
            Email = member.Account.Email,
            Phone = member.Account.Phone,
            Status = member.Account.Status,
            CreatedAt = member.CreatedAt,
            UpdatedAt = member.Account.UpdatedAt
        };

        return detail;
    }

    public async Task<UpdateMemberResult> UpdateMemberAsync(
        string accountId,
        UpdateMemberAPIViewModel request)
    {
        if (request.FullName is null &&
            !request.DateOfBirth.HasValue &&
            request.AvatarUrl is null &&
            request.Phone is null)
        {
            return UpdateMemberResult.NoChanges;
        }

        string? normalizedFullName = null;
        DateOnly? normalizedDateOfBirth = request.DateOfBirth;
        string? normalizedAvatarUrl = null;
        string? normalizedPhone = null;
        bool updateAvatarUrl = request.AvatarUrl is not null;
        bool updatePhone = request.Phone is not null;

        if (request.FullName is not null)
        {
            normalizedFullName = request.FullName.Trim();
            if (string.IsNullOrEmpty(normalizedFullName))
            {
                return UpdateMemberResult.InvalidData;
            }
        }

        if (normalizedDateOfBirth.HasValue &&
            normalizedDateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
        {
            return UpdateMemberResult.InvalidData;
        }

        if (request.AvatarUrl is not null)
        {
            normalizedAvatarUrl = request.AvatarUrl.Trim();
            if (string.IsNullOrEmpty(normalizedAvatarUrl))
            {
                normalizedAvatarUrl = null;
            }
        }

        if (request.Phone is not null)
        {
            normalizedPhone = request.Phone.Trim();
            if (string.IsNullOrEmpty(normalizedPhone))
            {
                normalizedPhone = null;
            }
            else
            {
                bool isValidPhone = normalizedPhone.Length == 10 &&
                    normalizedPhone.All(character => character >= '0' && character <= '9');

                if (!isValidPhone)
                {
                    return UpdateMemberResult.InvalidData;
                }
            }
        }

        Member? member = await _context.Members
            .Include(m => m.Account)
            .FirstOrDefaultAsync(m => m.AccountId == accountId && m.Account.Role.Name == "Member");

        if (member is null)
        {
            return UpdateMemberResult.NotFound;
        }

        if (updatePhone && normalizedPhone is not null)
        {
            bool isPhoneDuplicate = await _context.Accounts
                .AnyAsync(a => a.Phone == normalizedPhone && a.Id != accountId);

            if (isPhoneDuplicate)
            {
                return UpdateMemberResult.DuplicatePhone;
            }
        }

        if (request.FullName is not null)
        {
            member.FullName = normalizedFullName;
        }

        if (request.DateOfBirth.HasValue)
        {
            member.DateOfBirth = normalizedDateOfBirth;
        }

        if (updateAvatarUrl)
        {
            member.AvatarUrl = normalizedAvatarUrl;
        }

        if (updatePhone)
        {
            member.Account.Phone = normalizedPhone;
        }

        member.Account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return UpdateMemberResult.Success;
    }

    public async Task<UpdateMemberStatusResult> UpdateMemberStatusAsync(
        string accountId,
        string status)
    {
        string? normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus is null)
        {
            return UpdateMemberStatusResult.InvalidStatus;
        }

        Account? account = await _context.Accounts
            .Include(a => a.Role)
            .Include(a => a.Member)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.Role.Name == "Member" && a.Member != null);

        if (account is null)
        {
            return UpdateMemberStatusResult.NotFound;
        }

        // Only update Account.Status. IsLocked is a separate authentication lock flag and must remain untouched.
        account.Status = normalizedStatus;
        account.UpdatedAt = DateTime.UtcNow;

        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();

        return UpdateMemberStatusResult.Success;
    }

    public async Task<List<MemberSearchAPIViewModel>> QuickSearchMembersAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new List<MemberSearchAPIViewModel>();
        }

        string trimmedKeyword = keyword.Trim();

        IQueryable<Member> query = BuildMemberQuery();
        query = ApplyKeywordFilter(query, trimmedKeyword, includeMemberCode: true);

        List<MemberSearchAPIViewModel> results = await query
            .OrderBy(m => m.MemberCode)
            .Take(20)
            .Select(m => new MemberSearchAPIViewModel
            {
                AccountId = m.AccountId,
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                Email = m.Account.Email,
                Phone = m.Account.Phone,
                Status = m.Account.Status
            })
            .ToListAsync();

        return results;
    }
}
