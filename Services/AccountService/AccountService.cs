using APIViewModel.CenterManager;
using APIViewModel.AccountProfile;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.EmailService;
using Services.PasswordHashService;

namespace Services.AccountService;

public class AccountService : IAccountService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IEmailService? _emailService;

    public AccountService(
        SportsCenterManagementContext context,
        IPasswordHashService passwordHashService,
        IEmailService? emailService = null)
    {
        _context = context;
        _passwordHashService = passwordHashService;
        _emailService = emailService;
    }

    public async Task<AccountProfileAPIViewModel?> GetProfileAsync(string accountId)
    {
        Account? account = await ProfileQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == accountId);
        return account is null ? null : MapProfile(account);
    }

    public async Task<AccountProfileAPIViewModel?> UpdateProfileAsync(
        string accountId,
        UpdateAccountProfileAPIViewModel request)
    {
        Account? account = await ProfileQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == accountId);
        if (account is null)
        {
            return null;
        }

        string fullName = request.FullName.Trim();
        string? phone = string.IsNullOrWhiteSpace(request.Phone)
            ? null
            : request.Phone.Trim();
        if (phone is not null)
        {
            bool duplicatePhone = await _context.Accounts.AnyAsync(candidate =>
                candidate.Id != accountId && candidate.Phone == phone);
            if (duplicatePhone)
            {
                return null;
            }
        }

        account.Phone = phone;
        account.UpdatedAt = DateTime.UtcNow;
        switch (account.Role.Name)
        {
            case "Member" when account.Member is not null:
                account.Member.FullName = fullName;
                account.Member.DateOfBirth = request.DateOfBirth;
                account.Member.AvatarUrl = NormalizeOptional(request.AvatarUrl);
                break;
            case "Coach" when account.Coach is not null:
                account.Coach.FullName = fullName;
                account.Coach.DateOfBirth = request.DateOfBirth;
                account.Coach.AvatarUrl = NormalizeOptional(request.AvatarUrl);
                account.Coach.Specialization = NormalizeOptional(request.Specialization);
                account.Coach.WorkSchedule = NormalizeOptional(request.WorkSchedule);
                break;
            case "Receptionist" when account.Receptionist is not null:
                account.Receptionist.FullName = fullName;
                account.Receptionist.DateOfBirth = request.DateOfBirth;
                account.Receptionist.AvatarUrl = NormalizeOptional(request.AvatarUrl);
                account.Receptionist.WorkShift = NormalizeOptional(request.WorkSchedule);
                break;
            case "CenterManager" when account.CenterManager is not null:
                account.CenterManager.FullName = fullName;
                account.CenterManager.DateOfBirth = request.DateOfBirth;
                account.CenterManager.AvatarUrl = NormalizeOptional(request.AvatarUrl);
                break;
            default:
                return null;
        }

        await _context.SaveChangesAsync();
        return MapProfile(account);
    }

    private IQueryable<Account> ProfileQuery()
    {
        return _context.Accounts
            .Include(account => account.Role)
            .Include(account => account.Member)
            .Include(account => account.Coach)
            .Include(account => account.Receptionist)
            .Include(account => account.CenterManager);
    }

    private static AccountProfileAPIViewModel MapProfile(Account account)
    {
        string fullName = account.Member?.FullName ??
            account.Coach?.FullName ??
            account.Receptionist?.FullName ??
            account.CenterManager?.FullName ??
            account.Email.Split('@')[0];
        DateOnly? dateOfBirth = account.Member?.DateOfBirth ??
            account.Coach?.DateOfBirth ??
            account.Receptionist?.DateOfBirth ??
            account.CenterManager?.DateOfBirth;
        return new AccountProfileAPIViewModel
        {
            AccountId = account.Id,
            Email = account.Email,
            Role = account.Role.Name,
            FullName = fullName,
            Phone = account.Phone,
            DateOfBirth = dateOfBirth,
            AvatarUrl = account.Member?.AvatarUrl
                ?? account.Coach?.AvatarUrl
                ?? account.Receptionist?.AvatarUrl
                ?? account.CenterManager?.AvatarUrl,
            Specialization = account.Coach?.Specialization,
            WorkSchedule = account.Coach?.WorkSchedule ?? account.Receptionist?.WorkShift,
            MemberCode = account.Member?.MemberCode,
            CreatedAt = account.CreatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<bool> IsDuplicate(string email, string? phone)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        string? normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        return await _context.Accounts.AnyAsync(q =>
            q.Email.ToLower() == normalizedEmail ||
            (normalizedPhone != null && q.Phone == normalizedPhone));
    }

    public async Task<bool> CreateCenterManagerAsync(CreateCenterManagerAPIViewModel info)
    {
        try
        {
            if (await IsDuplicate(info.Email, info.Phone!))
            {
                return false;
            }

            Role? role = await _context.Roles
                .Where(q => q.Name == "CenterManager")
                .FirstOrDefaultAsync();

            if (role is null)
            {
                return false;
            }

            Account newAccount = new Account
            {
                Id = Guid.NewGuid().ToString(),
                Email = info.Email,
                Phone = info.Phone,
                PasswordHash = _passwordHashService.HashPassword(info.Password),
                RoleId = role.Id,
                Status = "Active",
                FailedLoginCount = 0,
                IsLocked = false
            };

            await _context.Accounts.AddAsync(newAccount);

            CenterManager newCenterManager = new CenterManager
            {
                AccountId = newAccount.Id,
                FullName = info.FullName
            };

            await _context.CenterManagers.AddAsync(newCenterManager);
            await _context.SaveChangesAsync();

            if (_emailService is not null)
            {
                await _emailService.SendNewAccountPasswordAsync(
                    newAccount.Email,
                    info.FullName,
                    "Quản lý Trung tâm",
                    info.Password);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> CreateCoachAsync(CreateCoachAPIViewModel info)
    {
        try
        {
            if (await IsDuplicate(info.Email, info.Phone!))
            {
                return false;
            }

            Role? role = await _context.Roles
                .Where(q => q.Name == "Coach")
                .FirstOrDefaultAsync();

            if (role is null)
            {
                return false;
            }

            Account newAccount = new Account
            {
                Id = Guid.NewGuid().ToString(),
                Email = info.Email,
                Phone = info.Phone,
                PasswordHash = _passwordHashService.HashPassword(info.Password),
                RoleId = role.Id,
                Status = "Active",
                FailedLoginCount = 0,
                IsLocked = false
            };

            await _context.Accounts.AddAsync(newAccount);

            Coach newCoach = new Coach
            {
                AccountId = newAccount.Id,
                FullName = info.FullName,
                Specialization = info.Specialization,
                WorkSchedule = info.WorkSchedule
            };

            await _context.Coaches.AddAsync(newCoach);
            await _context.SaveChangesAsync();

            if (_emailService is not null)
            {
                await _emailService.SendNewAccountPasswordAsync(
                    newAccount.Email,
                    info.FullName,
                    "Huấn luyện viên",
                    info.Password);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<RegisterMemberResponseAPIViewModel?> CreateMemberAsync(
        CreateMemberAPIViewModel info)
    {
        try
        {
            if (await IsDuplicate(info.Email, info.Phone!) || string.IsNullOrEmpty(info.MemberCode))
            {
                return null;
            }

            bool duplicateMemberCode = await _context.Members
                .AnyAsync(q => q.MemberCode == info.MemberCode);
            if (duplicateMemberCode)
            {
                return null;
            }

            Role? role = await _context.Roles
                .Where(q => q.Name == "Member")
                .FirstOrDefaultAsync();

            if (role is null)
            {
                return null;
            }

            string normalizedEmail = info.Email.Trim().ToLowerInvariant();

            Account newAccount = new Account
            {
                Id = Guid.NewGuid().ToString(),
                Email = normalizedEmail,
                Phone = info.Phone,
                PasswordHash = _passwordHashService.HashPassword(info.Password),
                RoleId = role.Id,
                Status = "Active",
                FailedLoginCount = 0,
                IsLocked = false
            };

            await _context.Accounts.AddAsync(newAccount);

            Member newMember = new Member
            {
                AccountId = newAccount.Id,
                MemberCode = info.MemberCode,
                FullName = info.FullName,
                DateOfBirth = info.DateOfBirth,
                AvatarUrl = info.AvatarUrl
            };

            await _context.Members.AddAsync(newMember);
            await _context.SaveChangesAsync();

            if (_emailService is not null)
            {
                await _emailService.SendNewAccountPasswordAsync(
                    newAccount.Email,
                    info.FullName,
                    "Hội viên",
                    info.Password);
            }

            return new RegisterMemberResponseAPIViewModel
            {
                AccountId = newAccount.Id,
                Email = newAccount.Email,
                MemberCode = newMember.MemberCode
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> CreateReceptionistAsync(CreateReceptionistAPIViewModel info)
    {
        try
        {
            if (await IsDuplicate(info.Email, info.Phone!))
            {
                return false;
            }

            Role? role = await _context.Roles
                .Where(q => q.Name == "Receptionist")
                .FirstOrDefaultAsync();

            if (role is null)
            {
                return false;
            }

            Account newAccount = new Account
            {
                Id = Guid.NewGuid().ToString(),
                Email = info.Email,
                Phone = info.Phone,
                PasswordHash = _passwordHashService.HashPassword(info.Password),
                RoleId = role.Id,
                Status = "Active",
                FailedLoginCount = 0,
                IsLocked = false
            };

            await _context.Accounts.AddAsync(newAccount);

            Receptionist newReceptionist = new Receptionist
            {
                AccountId = newAccount.Id,
                FullName = info.FullName,
                WorkShift = info.WorkShift
            };

            await _context.Receptionists.AddAsync(newReceptionist);
            await _context.SaveChangesAsync();

            if (_emailService is not null)
            {
                await _emailService.SendNewAccountPasswordAsync(
                    newAccount.Email,
                    info.FullName,
                    "Nhân viên Lễ tân",
                    info.Password);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> IsEmailExistsAsync(string email)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Accounts.AnyAsync(q => q.Email.ToLower() == normalizedEmail);
    }

    public async Task<RegisterMemberResponseAPIViewModel?> RegisterMemberAsync(RegisterMemberRequestAPIViewModel info)
    {
        try
        {
            string normalizedEmail = info.Email.Trim().ToLowerInvariant();
            if (await IsEmailExistsAsync(normalizedEmail))
            {
                return null;
            }

            Role? role = await _context.Roles
                .Where(q => q.Name == "Member")
                .FirstOrDefaultAsync();

            if (role is null)
            {
                return null;
            }

            Account newAccount = new Account
            {
                Id = Guid.NewGuid().ToString(),
                Email = normalizedEmail,
                PasswordHash = _passwordHashService.HashPassword(info.Password),
                RoleId = role.Id,
                Status = "Active",
                FailedLoginCount = 0,
                IsLocked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Accounts.AddAsync(newAccount);

            string memberCode = await GenerateUniqueMemberCodeAsync();
            Member newMember = new Member
            {
                AccountId = newAccount.Id,
                MemberCode = memberCode,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Members.AddAsync(newMember);
            await _context.SaveChangesAsync();

            if (_emailService is not null)
            {
                await _emailService.SendNewAccountPasswordAsync(
                    newAccount.Email,
                    "Hội viên",
                    "Hội viên",
                    info.Password);
            }

            return new RegisterMemberResponseAPIViewModel
            {
                AccountId = newAccount.Id,
                Email = newAccount.Email,
                MemberCode = newMember.MemberCode
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<string> GenerateUniqueMemberCodeAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            string candidateCode = $"MB{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1000, 9999)}";
            bool exists = await _context.Members.AnyAsync(q => q.MemberCode == candidateCode);
            if (!exists)
            {
                return candidateCode;
            }
        }

        return $"MB{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}
