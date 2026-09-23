using APIViewModel.Auth;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AccessTokenService;
using Services.PasswordHashService;

namespace Services.AuthService;

public class AuthService : IAuthService
{
    private const int MaximumFailedLoginAttempts = 5;
    private readonly SportsCenterManagementContext _context;
    private readonly IPasswordHashService _passwordHashService;

    public AuthService(
        SportsCenterManagementContext context,
        IAccessTokenService accessTokenService,
        IPasswordHashService passwordHashService)
    {
        _context = context;
        _passwordHashService = passwordHashService;
    }

    public async Task<LoginResponseAPIViewModel?> LoginMemberAsync(LoginRequestAPIViewModel request)
    {
        var role = await _context.Roles.Where(q => q.Name == "Member").FirstOrDefaultAsync();
        var account = await _context.Accounts.Where(q => q.Email == request.Email && q.RoleId == role.Id).FirstOrDefaultAsync();

        if (account is null || account.IsLocked ||
            !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!_passwordHashService.VerifyPassword(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= MaximumFailedLoginAttempts)
            {
                account.IsLocked = true;
            }

            account.UpdatedAt = DateTime.UtcNow;
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return null;

        }
        return new LoginResponseAPIViewModel()
        {
            Id = account.Id,
            Email = account.Email,
            Role = role.Name
        };


    }
    public async Task<LoginResponseAPIViewModel?> LoginReceptionistAsync(LoginRequestAPIViewModel request)
    {
        var role = await _context.Roles.Where(q => q.Name == "Receptionist").FirstOrDefaultAsync();
        var account = await _context.Accounts.Where(q => q.Email == request.Email && q.RoleId == role.Id).FirstOrDefaultAsync();

        if (account is null || account.IsLocked ||
            !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!_passwordHashService.VerifyPassword(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= MaximumFailedLoginAttempts)
            {
                account.IsLocked = true;
            }

            account.UpdatedAt = DateTime.UtcNow;
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return null;

        }
        return new LoginResponseAPIViewModel()
        {
            Id = account.Id,
            Email = account.Email,
            Role = role.Name
        };


    }
    public async Task<LoginResponseAPIViewModel?> LoginCoachAsync(LoginRequestAPIViewModel request)
    {
        var role = await _context.Roles.Where(q => q.Name == "Coach").FirstOrDefaultAsync();
        var account = await _context.Accounts.Where(q => q.Email == request.Email && q.RoleId == role.Id).FirstOrDefaultAsync();

        if (account is null || account.IsLocked ||
            !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!_passwordHashService.VerifyPassword(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= MaximumFailedLoginAttempts)
            {
                account.IsLocked = true;
            }

            account.UpdatedAt = DateTime.UtcNow;
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return null;

        }
        return new LoginResponseAPIViewModel()
        {
            Id = account.Id,
            Email = account.Email,
            Role = role.Name
        };


    }
    public async Task<LoginResponseAPIViewModel?> LoginAdminAsync(LoginRequestAPIViewModel request)
    {
        var role = await _context.Roles.Where(q => q.Name == "Admin").FirstOrDefaultAsync();
        var account = await _context.Accounts.Where(q => q.Email == request.Email && q.RoleId == role.Id).FirstOrDefaultAsync();

        if (account is null || account.IsLocked ||
            !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!_passwordHashService.VerifyPassword(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= MaximumFailedLoginAttempts)
            {
                account.IsLocked = true;
            }

            account.UpdatedAt = DateTime.UtcNow;
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return null;
           
        }
        return new LoginResponseAPIViewModel()
        {
            Id = account.Id,
            Email = account.Email,
            Role = role.Name
        };


    }

}
