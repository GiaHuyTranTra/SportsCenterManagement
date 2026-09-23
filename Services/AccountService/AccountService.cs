using APIViewModel.CenterManager;
using APIViewModel.Coach;
using APIViewModel.Member;
using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.PasswordHashService;

namespace Services.AccountService;

public class AccountService : IAccountService
{
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
        Account account = await _context.Accounts.Where(q => q.Email.ToLower().Equals(email.ToLower()) || q.Phone.Equals(phone)).FirstOrDefaultAsync();

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

            Role role = await _context.Roles
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

            Role role = await _context.Roles
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

            Role role = await _context.Roles
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

            Role role = await _context.Roles
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
}
