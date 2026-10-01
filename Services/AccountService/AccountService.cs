using APIViewModel.Account;
using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.PasswordHashService;
using System.Data;

namespace Services.AccountService;

public class AccountService : IAccountService
{
    private const string ActiveStatus = "Active";
    private const string InactiveStatus = "Inactive";

    private readonly SportsCenterManagementContext _context;
    private readonly IPasswordHashService _passwordHashService;

    public AccountService(
        SportsCenterManagementContext context,
        IPasswordHashService passwordHashService)
    {
        _context = context;
        _passwordHashService = passwordHashService;
    }

    private async Task<bool> IsDuplicate(string email, string phone)
    {
        Account? account = await _context.Accounts
            .Where(q => q.Email.ToLower() == email.ToLower() || q.Phone == phone)
            .FirstOrDefaultAsync();

        if (account == null)
            return false;
        else
            return true;
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
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> CreateMemberAsync(CreateMemberAPIViewModel info)
    {
        try
        {
            if (await IsDuplicate(info.Email, info.Phone!) || string.IsNullOrEmpty(info.MemberCode))
            {
                return false;
            }

            bool duplicateMemberCode = await _context.Members
                .AnyAsync(q => q.MemberCode == info.MemberCode);
            if (duplicateMemberCode)
            {
                return false;
            }

            Role? role = await _context.Roles
                .Where(q => q.Name == "Member")
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
            return true;
        }
        catch (Exception)
        {
            return false;
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

    public async Task<(GetCurrentUserProfileResult Result, CurrentUserProfileAPIViewModel? Profile)>
        GetCurrentUserProfileAsync(string accountId)
    {
        Account? account = await _context.Accounts
            .AsNoTracking()
            .Include(item => item.Role)
            .Include(item => item.CenterManager)
            .Include(item => item.Coach)
                .ThenInclude(coach => coach!.CoachDisciplines)
                    .ThenInclude(link => link.Discipline)
            .Include(item => item.Member)
            .Include(item => item.Receptionist)
            .FirstOrDefaultAsync(item => item.Id == accountId);

        if (account is null)
        {
            return (GetCurrentUserProfileResult.AccountNotFound, null);
        }

        CurrentUserProfileAPIViewModel profile = new CurrentUserProfileAPIViewModel
        {
            AccountId = account.Id,
            Email = account.Email,
            Phone = account.Phone,
            Status = account.Status,
            Role = account.Role.Name,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };

        switch (account.Role.Name)
        {
            case "CenterManager" when account.CenterManager is not null:
                profile.FullName = account.CenterManager.FullName;
                break;
            case "Coach" when account.Coach is not null:
                profile.FullName = account.Coach.FullName;
                profile.Specialization = account.Coach.CoachDisciplines.Count > 0
                    ? string.Join(", ", account.Coach.CoachDisciplines
                        .OrderBy(link => link.Discipline.Name)
                        .ThenBy(link => link.DisciplineId)
                        .Select(link => link.Discipline.Name))
                    : account.Coach.Specialization;
                profile.WorkSchedule = account.Coach.WorkSchedule;
                break;
            case "Member" when account.Member is not null:
                profile.FullName = account.Member.FullName;
                profile.MemberCode = account.Member.MemberCode;
                profile.DateOfBirth = account.Member.DateOfBirth;
                profile.AvatarUrl = account.Member.AvatarUrl;
                break;
            case "Receptionist" when account.Receptionist is not null:
                profile.FullName = account.Receptionist.FullName;
                profile.WorkShift = account.Receptionist.WorkShift;
                break;
            case "CenterManager":
            case "Coach":
            case "Member":
            case "Receptionist":
                return (GetCurrentUserProfileResult.ProfileNotFound, null);
            default:
                return (GetCurrentUserProfileResult.UnsupportedRole, null);
        }

        return (GetCurrentUserProfileResult.Success, profile);
    }

    public async Task<UpdateCurrentProfileResult> UpdateCurrentProfileAsync(
        string accountId,
        UpdateProfileAPIViewModel request)
    {
        if (request.FullName is null &&
            request.Phone is null &&
            !request.DateOfBirth.HasValue &&
            request.AvatarUrl is null &&
            request.Specialization is null &&
            request.WorkSchedule is null &&
            request.WorkShift is null)
        {
            return UpdateCurrentProfileResult.NoChanges;
        }

        string? normalizedFullName = null;
        string? normalizedPhone = null;
        string? normalizedAvatarUrl = null;
        string? normalizedWorkSchedule = null;
        string? normalizedWorkShift = null;
        DateOnly? normalizedDateOfBirth = request.DateOfBirth;

        if (request.FullName is not null)
        {
            normalizedFullName = request.FullName.Trim();
            if (string.IsNullOrEmpty(normalizedFullName))
            {
                return UpdateCurrentProfileResult.InvalidData;
            }
        }

        if (request.Phone is not null)
        {
            normalizedPhone = NormalizeOptionalText(request.Phone);
            if (normalizedPhone is not null)
            {
                bool isValidPhone = normalizedPhone.Length == 10 &&
                    normalizedPhone.All(character => character >= '0' && character <= '9');
                if (!isValidPhone)
                {
                    return UpdateCurrentProfileResult.InvalidData;
                }
            }
        }

        if (normalizedDateOfBirth.HasValue &&
            normalizedDateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
        {
            return UpdateCurrentProfileResult.InvalidData;
        }

        if (request.AvatarUrl is not null)
        {
            normalizedAvatarUrl = NormalizeOptionalText(request.AvatarUrl);
        }

        if (request.WorkSchedule is not null)
        {
            normalizedWorkSchedule = NormalizeOptionalText(request.WorkSchedule);
        }

        if (request.WorkShift is not null)
        {
            normalizedWorkShift = NormalizeOptionalText(request.WorkShift);
        }

        Account? account = await _context.Accounts
            .Include(item => item.Role)
            .Include(item => item.CenterManager)
            .Include(item => item.Coach)
                .ThenInclude(coach => coach!.CoachDisciplines)
                    .ThenInclude(link => link.Discipline)
            .Include(item => item.Member)
            .Include(item => item.Receptionist)
            .FirstOrDefaultAsync(item => item.Id == accountId);

        if (account is null)
        {
            return UpdateCurrentProfileResult.AccountNotFound;
        }

        bool hasInvalidField = account.Role.Name switch
        {
            "Member" => request.Specialization is not null ||
                request.WorkSchedule is not null ||
                request.WorkShift is not null,
            "Coach" => request.DateOfBirth.HasValue ||
                request.AvatarUrl is not null ||
                request.Specialization is not null ||
                request.WorkShift is not null,
            "Receptionist" => request.DateOfBirth.HasValue ||
                request.AvatarUrl is not null ||
                request.Specialization is not null ||
                request.WorkSchedule is not null,
            "CenterManager" => request.DateOfBirth.HasValue ||
                request.AvatarUrl is not null ||
                request.Specialization is not null ||
                request.WorkSchedule is not null ||
                request.WorkShift is not null,
            _ => false
        };

        if (hasInvalidField)
        {
            return UpdateCurrentProfileResult.InvalidFieldForRole;
        }

        if (account.Role.Name == "CenterManager" && account.CenterManager is null ||
            account.Role.Name == "Coach" && account.Coach is null ||
            account.Role.Name == "Member" && account.Member is null ||
            account.Role.Name == "Receptionist" && account.Receptionist is null)
        {
            return UpdateCurrentProfileResult.ProfileNotFound;
        }

        if (account.Role.Name != "CenterManager" &&
            account.Role.Name != "Coach" &&
            account.Role.Name != "Member" &&
            account.Role.Name != "Receptionist")
        {
            return UpdateCurrentProfileResult.UnsupportedRole;
        }

        if (request.Phone is not null && normalizedPhone is not null)
        {
            bool duplicatePhone = await _context.Accounts
                .AnyAsync(item => item.Id != accountId && item.Phone == normalizedPhone);
            if (duplicatePhone)
            {
                return UpdateCurrentProfileResult.DuplicatePhone;
            }
        }

        if (request.Phone is not null)
        {
            account.Phone = normalizedPhone;
        }

        switch (account.Role.Name)
        {
            case "CenterManager":
                if (request.FullName is not null)
                {
                    account.CenterManager!.FullName = normalizedFullName!;
                }
                break;
            case "Coach":
                if (request.FullName is not null)
                {
                    account.Coach!.FullName = normalizedFullName!;
                }
                if (request.WorkSchedule is not null)
                {
                    account.Coach!.WorkSchedule = normalizedWorkSchedule;
                }
                break;
            case "Member":
                if (request.FullName is not null)
                {
                    account.Member!.FullName = normalizedFullName;
                }
                if (request.DateOfBirth.HasValue)
                {
                    account.Member!.DateOfBirth = normalizedDateOfBirth;
                }
                if (request.AvatarUrl is not null)
                {
                    account.Member!.AvatarUrl = normalizedAvatarUrl;
                }
                break;
            case "Receptionist":
                if (request.FullName is not null)
                {
                    account.Receptionist!.FullName = normalizedFullName!;
                }
                if (request.WorkShift is not null)
                {
                    account.Receptionist!.WorkShift = normalizedWorkShift;
                }
                break;
        }

        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            return UpdateCurrentProfileResult.Success;
        }
        catch (DbUpdateException exception) when (
            IsSpecificUniqueConstraintViolation(exception, "UQ_Account_Phone"))
        {
            return UpdateCurrentProfileResult.DuplicatePhone;
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            return UpdateCurrentProfileResult.ConcurrencyConflict;
        }
    }

    public async Task<UnlockAccountResult> UnlockAccountAsync(string accountId)
    {
        Account? account = await _context.Accounts
            .FirstOrDefaultAsync(item => item.Id == accountId);
        if (account is null)
        {
            return UnlockAccountResult.NotFound;
        }

        if (!account.IsLocked && account.FailedLoginCount == 0)
        {
            return UnlockAccountResult.Success;
        }

        account.IsLocked = false;
        account.FailedLoginCount = 0;
        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return UnlockAccountResult.Success;
    }

    public async Task<UpdateAccountStatusResult> UpdateAccountStatusAsync(
        string actorAccountId,
        string targetAccountId,
        string status)
    {
        string? normalizedStatus = NormalizeAccountStatus(status);
        if (normalizedStatus is null)
        {
            return UpdateAccountStatusResult.InvalidStatus;
        }

        if (normalizedStatus == InactiveStatus &&
            string.Equals(actorAccountId, targetAccountId, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateAccountStatusResult.SelfDeactivationNotAllowed;
        }

        if (!_context.Database.IsRelational())
        {
            return await UpdateAccountStatusInMemoryAsync(targetAccountId, normalizedStatus);
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            Account? account = await _context.Accounts
                .FromSqlInterpolated($"SELECT * FROM [Account] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {targetAccountId}")
                .Include(item => item.Role)
                .FirstOrDefaultAsync();

            if (account is null)
            {
                await transaction.RollbackAsync();
                return UpdateAccountStatusResult.NotFound;
            }

            if (account.Status == normalizedStatus)
            {
                await transaction.CommitAsync();
                return UpdateAccountStatusResult.Success;
            }

            if (normalizedStatus == InactiveStatus && account.Role.Name == "CenterManager")
            {
                Role? managerRole = await _context.Roles
                    .FromSqlRaw("SELECT * FROM [Role] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = 'CenterManager'")
                    .FirstOrDefaultAsync();

                if (managerRole is null)
                {
                    await transaction.RollbackAsync();
                    return UpdateAccountStatusResult.NotFound;
                }

                int activeManagerCount = await _context.Accounts.CountAsync(item =>
                    item.RoleId == managerRole.Id && item.Status == ActiveStatus);
                if (activeManagerCount <= 1)
                {
                    await transaction.RollbackAsync();
                    return UpdateAccountStatusResult.LastActiveCenterManager;
                }
            }

            account.Status = normalizedStatus;
            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return UpdateAccountStatusResult.Success;
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            await transaction.RollbackAsync();
            return UpdateAccountStatusResult.ConcurrencyConflict;
        }
    }

    private async Task<UpdateAccountStatusResult> UpdateAccountStatusInMemoryAsync(
        string targetAccountId,
        string normalizedStatus)
    {
        Account? account = await _context.Accounts
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == targetAccountId);
        if (account is null)
        {
            return UpdateAccountStatusResult.NotFound;
        }

        if (account.Status == normalizedStatus)
        {
            return UpdateAccountStatusResult.Success;
        }

        if (normalizedStatus == InactiveStatus && account.Role.Name == "CenterManager")
        {
            int activeManagerCount = await _context.Accounts.CountAsync(item =>
                item.RoleId == account.RoleId && item.Status == ActiveStatus);
            if (activeManagerCount <= 1)
            {
                return UpdateAccountStatusResult.LastActiveCenterManager;
            }
        }

        account.Status = normalizedStatus;
        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return UpdateAccountStatusResult.Success;
    }

    private static string? NormalizeAccountStatus(string status)
    {
        string trimmedStatus = status.Trim();
        if (string.Equals(trimmedStatus, ActiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return ActiveStatus;
        }

        if (string.Equals(trimmedStatus, InactiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return InactiveStatus;
        }

        return null;
    }

    private static string? NormalizeOptionalText(string value)
    {
        string normalizedValue = value.Trim();
        return normalizedValue.Length == 0 ? null : normalizedValue;
    }

    private static bool IsSpecificUniqueConstraintViolation(
        Exception exception,
        string indexName)
    {
        Exception? currentException = exception;
        while (currentException is not null)
        {
            if (currentException is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    if ((error.Number == 2601 || error.Number == 2627) &&
                        error.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            currentException = currentException.InnerException;
        }

        return false;
    }

    private static bool IsDeadlock(Exception exception)
    {
        Exception? currentException = exception;
        while (currentException is not null)
        {
            if (currentException is SqlException sqlException && sqlException.Number == 1205)
            {
                return true;
            }

            currentException = currentException.InnerException;
        }

        return false;
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
