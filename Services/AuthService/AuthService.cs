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

    public Task<LoginResponseAPIViewModel?> LoginMemberAsync(LoginRequestAPIViewModel request)
    {
        return LoginByRoleAsync(request, "Member");
    }

    public Task<LoginResponseAPIViewModel?> LoginReceptionistAsync(LoginRequestAPIViewModel request)
    {
        return LoginByRoleAsync(request, "Receptionist");
    }

    public Task<LoginResponseAPIViewModel?> LoginCoachAsync(LoginRequestAPIViewModel request)
    {
        return LoginByRoleAsync(request, "Coach");
    }

    public Task<LoginResponseAPIViewModel?> LoginCenterManagerAsync(LoginRequestAPIViewModel request)
    {
        return LoginByRoleAsync(request, "CenterManager");
    }

    private async Task<LoginResponseAPIViewModel?> LoginByRoleAsync(
        LoginRequestAPIViewModel request,
        string roleName)
    {
        Role? role = await _context.Roles
            .Where(q => q.Name == roleName)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return null;
        }

        Account? account = await _context.Accounts
            .Where(q =>
                q.Status == "Active" &&
                !q.IsLocked &&
                q.Email == request.Email &&
                q.RoleId == role.Id)
            .FirstOrDefaultAsync();

        if (account == null)
        {
            return null;
        }

        bool isCorrectPassword = _passwordHashService.VerifyPassword(
            request.Password,
            account.PasswordHash);

        if (!isCorrectPassword)
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

        if (account.FailedLoginCount > 0)
        {
            account.FailedLoginCount = 0;
            account.UpdatedAt = DateTime.UtcNow;
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
        }

        LoginResponseAPIViewModel response = new LoginResponseAPIViewModel
        {
            Id = account.Id,
            Email = account.Email,
            Role = role.Name
        };

        return response;
    }
}
