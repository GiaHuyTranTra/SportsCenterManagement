using APIViewModel.Auth;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AccessTokenService;
using Services.EmailService;
using Services.PasswordHashService;
using System.Globalization;
using System.Security.Cryptography;

namespace Services.AuthService;

public class AuthService : IAuthService
{
    private const int MaximumFailedLoginAttempts = 5;
    private const int OtpCooldownSeconds = 60;
    private const int OtpExpirationMinutes = 5;
    private const int OtpMaximumAttempts = 3;

    // SQL Server deadlock error number.
    private const int SqlDeadlockErrorNumber = 1205;

    private readonly SportsCenterManagementContext _context;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IEmailService? _emailService;

    // accessTokenService parameter is retained for constructor compatibility
    // with existing test infrastructure that supplies it positionally.
    public AuthService(
        SportsCenterManagementContext context,
        IAccessTokenService accessTokenService,
        IPasswordHashService passwordHashService,
        IEmailService? emailService = null)
    {
        _context = context;
        _passwordHashService = passwordHashService;
        _emailService = emailService;
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

    public async Task<(LoginResult Result, LoginResponseAPIViewModel? Account)> LoginAsync(
        LoginRequestAPIViewModel request)
    {
        (LoginResult result, LoginResponseAPIViewModel? account) = await LoginCoreAsync(
            request, requiredRoleName: null);
        return (result, account);
    }

    public async Task<LoginResponseAPIViewModel?> GetSessionAccountAsync(string accountId)
    {
        Account? account = await _context.Accounts
            .AsNoTracking()
            .Include(candidate => candidate.Role)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == accountId &&
                candidate.Status == "Active" &&
                candidate.DeletedAt == null);

        if (account is null)
        {
            return null;
        }

        return new LoginResponseAPIViewModel
        {
            Id = account.Id,
            Email = account.Email,
            Role = account.Role.Name
        };
    }

    // Shared implementation for unified and role-specific login.
    // requiredRoleName: when non-null, only accounts with that role proceed;
    // mismatched role returns InvalidCredentials to avoid leaking account existence.
    private async Task<(LoginResult Result, LoginResponseAPIViewModel? Account)> LoginCoreAsync(
        LoginRequestAPIViewModel request,
        string? requiredRoleName)
    {
        string normalizedEmail = request.Email.Trim();

        Account? initialAccount = await _context.Accounts
            .AsNoTracking()
            .Include(a => a.Role)
            .Where(a => a.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (initialAccount is null)
        {
            return (LoginResult.InvalidCredentials, null);
        }

        if (requiredRoleName is not null &&
            initialAccount.Role.Name != requiredRoleName)
        {
            return (LoginResult.InvalidCredentials, null);
        }

        if (IsInactive(initialAccount))
        {
            return (LoginResult.AccountInactive, null);
        }

        if (initialAccount.IsLocked)
        {
            return (LoginResult.AccountLocked, null);
        }

        string initialPasswordHash = initialAccount.PasswordHash;
        int initialRoleId = initialAccount.RoleId;

        bool isPasswordValid = _passwordHashService.VerifyPassword(
            request.Password,
            initialPasswordHash);

        // Use transactional locking on relational databases.
        // Skip on in-memory provider (unit tests) where transactions are unsupported.
        if (_context.Database.IsRelational())
        {
            return await LoginCoreRelationalAsync(
                initialAccount.Id,
                initialPasswordHash,
                initialRoleId,
                isPasswordValid);
        }

        return await LoginCoreInMemoryAsync(
            initialAccount.Id,
            initialPasswordHash,
            initialRoleId,
            isPasswordValid);
    }

    // Production path: UPDLOCK + HOLDLOCK + ReadCommitted transaction.
    private async Task<(LoginResult Result, LoginResponseAPIViewModel? Account)>
        LoginCoreRelationalAsync(
            string accountId,
            string initialPasswordHash,
            int initialRoleId,
            bool isPasswordValid)
    {
        try
        {
            using IDbContextTransaction transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

            Account? lockedAccount = await _context.Accounts
                .FromSqlRaw(
                    "SELECT * FROM [dbo].[Account] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {0}",
                    accountId)
                .Include(a => a.Role)
                .FirstOrDefaultAsync();

            if (lockedAccount is null)
            {
                await transaction.RollbackAsync();
                return (LoginResult.InvalidCredentials, null);
            }

            if (IsInactive(lockedAccount))
            {
                await transaction.RollbackAsync();
                return (LoginResult.AccountInactive, null);
            }

            if (lockedAccount.IsLocked)
            {
                await transaction.RollbackAsync();
                return (LoginResult.AccountLocked, null);
            }

            if (!string.Equals(lockedAccount.PasswordHash, initialPasswordHash,
                    StringComparison.Ordinal))
            {
                await transaction.RollbackAsync();
                return (LoginResult.ConcurrentPasswordChange, null);
            }

            if (lockedAccount.RoleId != initialRoleId)
            {
                await transaction.RollbackAsync();
                return (LoginResult.ConcurrentPasswordChange, null);
            }

            if (!isPasswordValid)
            {
                lockedAccount.FailedLoginCount++;
                lockedAccount.UpdatedAt = DateTime.UtcNow;

                if (lockedAccount.FailedLoginCount >= MaximumFailedLoginAttempts)
                {
                    lockedAccount.IsLocked = true;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return (LoginResult.AccountLocked, null);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (LoginResult.InvalidCredentials, null);
            }

            lockedAccount.FailedLoginCount = 0;
            lockedAccount.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            LoginResponseAPIViewModel loginResponse = new LoginResponseAPIViewModel
            {
                Id = lockedAccount.Id,
                Email = lockedAccount.Email,
                Role = lockedAccount.Role.Name
            };

            return (LoginResult.Success, loginResponse);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            return (LoginResult.ConcurrencyConflict, null);
        }
    }

    // Test path: no transaction or raw SQL; direct EF tracking on in-memory store.
    // Performs the same logical checks as the relational path (minus locking).
    private async Task<(LoginResult Result, LoginResponseAPIViewModel? Account)>
        LoginCoreInMemoryAsync(
            string accountId,
            string initialPasswordHash,
            int initialRoleId,
            bool isPasswordValid)
    {
        Account? account = await _context.Accounts
            .Include(a => a.Role)
            .Where(a => a.Id == accountId)
            .FirstOrDefaultAsync();

        if (account is null)
        {
            return (LoginResult.InvalidCredentials, null);
        }

        if (IsInactive(account))
        {
            return (LoginResult.AccountInactive, null);
        }

        if (account.IsLocked)
        {
            return (LoginResult.AccountLocked, null);
        }

        if (!string.Equals(account.PasswordHash, initialPasswordHash,
                StringComparison.Ordinal))
        {
            return (LoginResult.ConcurrentPasswordChange, null);
        }

        if (account.RoleId != initialRoleId)
        {
            return (LoginResult.ConcurrentPasswordChange, null);
        }

        if (!isPasswordValid)
        {
            account.FailedLoginCount++;
            account.UpdatedAt = DateTime.UtcNow;

            if (account.FailedLoginCount >= MaximumFailedLoginAttempts)
            {
                account.IsLocked = true;
                await _context.SaveChangesAsync();
                return (LoginResult.AccountLocked, null);
            }

            await _context.SaveChangesAsync();
            return (LoginResult.InvalidCredentials, null);
        }

        account.FailedLoginCount = 0;
        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        LoginResponseAPIViewModel loginResponse = new LoginResponseAPIViewModel
        {
            Id = account.Id,
            Email = account.Email,
            Role = account.Role.Name
        };

        return (LoginResult.Success, loginResponse);
    }

    public async Task<(RequestPasswordChangeOtpResult Result, int RetryAfterSeconds)>
        RequestChangePasswordOtpAsync(string accountId)
    {
        if (_emailService is null)
        {
            return (RequestPasswordChangeOtpResult.DeliveryFailed, 0);
        }

        string rawOtp = RandomNumberGenerator
            .GetInt32(0, 1000000)
            .ToString("D6", CultureInfo.InvariantCulture);
        string otpHash = _passwordHashService.HashPassword(rawOtp);

        (RequestPasswordChangeOtpResult Result, int RetryAfterSeconds, int OtpId, string? Email)
            creationResult = _context.Database.IsRelational()
                ? await CreatePasswordChangeOtpRelationalAsync(accountId, otpHash)
                : await CreatePasswordChangeOtpInMemoryAsync(accountId, otpHash);

        if (creationResult.Result != RequestPasswordChangeOtpResult.Success ||
            creationResult.Email is null)
        {
            return (creationResult.Result, creationResult.RetryAfterSeconds);
        }

        try
        {
            await _emailService.SendPasswordChangeOtpAsync(
                creationResult.Email,
                rawOtp,
                OtpExpirationMinutes);
        }
        catch (Exception)
        {
            await InvalidatePasswordChangeOtpAsync(creationResult.OtpId);
            return (RequestPasswordChangeOtpResult.DeliveryFailed, 0);
        }

        return (RequestPasswordChangeOtpResult.Success, 0);
    }

    public async Task<ChangePasswordWithOtpResult> ChangePasswordWithOtpAsync(
        string accountId,
        ChangePasswordWithOtpRequestAPIViewModel request)
    {
        Account? initialAccount = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == accountId);

        if (initialAccount is null)
        {
            return ChangePasswordWithOtpResult.AccountNotFound;
        }

        if (IsInactive(initialAccount))
        {
            return ChangePasswordWithOtpResult.AccountInactive;
        }

        if (initialAccount.IsLocked)
        {
            return ChangePasswordWithOtpResult.AccountLocked;
        }

        bool isCurrentPasswordValid = _passwordHashService.VerifyPassword(
            request.CurrentPassword,
            initialAccount.PasswordHash);
        if (!isCurrentPasswordValid)
        {
            return ChangePasswordWithOtpResult.IncorrectCurrentPassword;
        }

        PasswordChangeOtp? initialOtp = await _context.PasswordChangeOtps
            .AsNoTracking()
            .Where(otp => otp.AccountId == accountId && !otp.IsUsed)
            .OrderByDescending(otp => otp.CreatedAt)
            .FirstOrDefaultAsync();

        if (initialOtp is null)
        {
            return ChangePasswordWithOtpResult.OtpNotFound;
        }

        bool isPasswordUnchanged = _passwordHashService.VerifyPassword(
            request.NewPassword,
            initialAccount.PasswordHash);
        if (isPasswordUnchanged)
        {
            return ChangePasswordWithOtpResult.PasswordUnchanged;
        }

        bool isOtpValid = _passwordHashService.VerifyPassword(
            request.Otp,
            initialOtp.OtpHash);
        string newPasswordHash = isOtpValid
            ? _passwordHashService.HashPassword(request.NewPassword)
            : string.Empty;

        if (_context.Database.IsRelational())
        {
            return await ChangePasswordWithOtpRelationalAsync(
                initialAccount,
                initialOtp,
                isOtpValid,
                newPasswordHash);
        }

        return await ChangePasswordWithOtpInMemoryAsync(
            initialAccount,
            initialOtp,
            isOtpValid,
            newPasswordHash);
    }

    private async Task<(
        RequestPasswordChangeOtpResult Result,
        int RetryAfterSeconds,
        int OtpId,
        string? Email)> CreatePasswordChangeOtpRelationalAsync(
            string accountId,
            string otpHash)
    {
        try
        {
            await using IDbContextTransaction transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

            Account? account = await _context.Accounts
                .FromSqlRaw(
                    "SELECT * FROM [dbo].[Account] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {0}",
                    accountId)
                .FirstOrDefaultAsync();

            (RequestPasswordChangeOtpResult Result, int RetryAfterSeconds, int OtpId, string? Email)
                result = await CreatePasswordChangeOtpCoreAsync(account, accountId, otpHash);

            if (result.Result != RequestPasswordChangeOtpResult.Success)
            {
                await transaction.RollbackAsync();
                return result;
            }

            await transaction.CommitAsync();
            return result;
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            return (RequestPasswordChangeOtpResult.ConcurrencyConflict, 0, 0, null);
        }
        catch (Exception exception) when (IsOtpUniqueConstraintViolation(exception))
        {
            return (RequestPasswordChangeOtpResult.ConcurrentRequest, 0, 0, null);
        }
    }

    private async Task<(
        RequestPasswordChangeOtpResult Result,
        int RetryAfterSeconds,
        int OtpId,
        string? Email)> CreatePasswordChangeOtpInMemoryAsync(
            string accountId,
            string otpHash)
    {
        Account? account = await _context.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Id == accountId);
        return await CreatePasswordChangeOtpCoreAsync(account, accountId, otpHash);
    }

    private async Task<(
        RequestPasswordChangeOtpResult Result,
        int RetryAfterSeconds,
        int OtpId,
        string? Email)> CreatePasswordChangeOtpCoreAsync(
            Account? account,
            string accountId,
            string otpHash)
    {
        if (account is null)
        {
            return (RequestPasswordChangeOtpResult.AccountNotFound, 0, 0, null);
        }

        if (IsInactive(account))
        {
            return (RequestPasswordChangeOtpResult.AccountInactive, 0, 0, null);
        }

        if (account.IsLocked)
        {
            return (RequestPasswordChangeOtpResult.AccountLocked, 0, 0, null);
        }

        DateTime nowUtc = DateTime.UtcNow;
        PasswordChangeOtp? activeOtp = await _context.PasswordChangeOtps
            .Where(otp => otp.AccountId == accountId && !otp.IsUsed)
            .OrderByDescending(otp => otp.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeOtp is not null)
        {
            DateTime cooldownEndsAt = activeOtp.CreatedAt.AddSeconds(OtpCooldownSeconds);
            if (cooldownEndsAt > nowUtc)
            {
                int retryAfterSeconds = Math.Max(
                    1,
                    (int)Math.Ceiling((cooldownEndsAt - nowUtc).TotalSeconds));
                return (
                    RequestPasswordChangeOtpResult.CooldownActive,
                    retryAfterSeconds,
                    0,
                    null);
            }
        }

        List<PasswordChangeOtp> oldOtps = await _context.PasswordChangeOtps
            .Where(otp => otp.AccountId == accountId && !otp.IsUsed)
            .ToListAsync();
        foreach (PasswordChangeOtp oldOtp in oldOtps)
        {
            oldOtp.IsUsed = true;
        }

        PasswordChangeOtp newOtp = new PasswordChangeOtp
        {
            AccountId = accountId,
            OtpHash = otpHash,
            FailedAttempts = 0,
            MaxAttempts = OtpMaximumAttempts,
            ExpiresAt = nowUtc.AddMinutes(OtpExpirationMinutes),
            IsUsed = false,
            CreatedAt = nowUtc
        };
        _context.PasswordChangeOtps.Add(newOtp);
        await _context.SaveChangesAsync();

        return (RequestPasswordChangeOtpResult.Success, 0, newOtp.Id, account.Email);
    }

    private async Task<ChangePasswordWithOtpResult> ChangePasswordWithOtpRelationalAsync(
        Account initialAccount,
        PasswordChangeOtp initialOtp,
        bool isOtpValid,
        string newPasswordHash)
    {
        try
        {
            await using IDbContextTransaction transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

            Account? lockedAccount = await _context.Accounts
                .FromSqlRaw(
                    "SELECT * FROM [dbo].[Account] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {0}",
                    initialAccount.Id)
                .FirstOrDefaultAsync();

            PasswordChangeOtp? lockedOtp = await _context.PasswordChangeOtps
                .FirstOrDefaultAsync(otp =>
                    otp.Id == initialOtp.Id &&
                    otp.AccountId == initialAccount.Id);

            ChangePasswordWithOtpResult result = await ChangePasswordWithOtpCoreAsync(
                lockedAccount,
                lockedOtp,
                initialAccount.PasswordHash,
                initialOtp.OtpHash,
                isOtpValid,
                newPasswordHash);

            await transaction.CommitAsync();
            return result;
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            return ChangePasswordWithOtpResult.ConcurrencyConflict;
        }
    }

    private async Task<ChangePasswordWithOtpResult> ChangePasswordWithOtpInMemoryAsync(
        Account initialAccount,
        PasswordChangeOtp initialOtp,
        bool isOtpValid,
        string newPasswordHash)
    {
        Account? account = await _context.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Id == initialAccount.Id);
        PasswordChangeOtp? otp = await _context.PasswordChangeOtps
            .FirstOrDefaultAsync(candidate => candidate.Id == initialOtp.Id);

        return await ChangePasswordWithOtpCoreAsync(
            account,
            otp,
            initialAccount.PasswordHash,
            initialOtp.OtpHash,
            isOtpValid,
            newPasswordHash);
    }

    private async Task<ChangePasswordWithOtpResult> ChangePasswordWithOtpCoreAsync(
        Account? account,
        PasswordChangeOtp? otp,
        string initialPasswordHash,
        string initialOtpHash,
        bool isOtpValid,
        string newPasswordHash)
    {
        if (account is null)
        {
            return ChangePasswordWithOtpResult.AccountNotFound;
        }

        if (IsInactive(account))
        {
            return ChangePasswordWithOtpResult.AccountInactive;
        }

        if (account.IsLocked)
        {
            return ChangePasswordWithOtpResult.AccountLocked;
        }

        if (!string.Equals(account.PasswordHash, initialPasswordHash, StringComparison.Ordinal))
        {
            return ChangePasswordWithOtpResult.ConcurrentPasswordChange;
        }

        if (otp is null || otp.IsUsed)
        {
            return ChangePasswordWithOtpResult.OtpNotFound;
        }

        if (!string.Equals(otp.OtpHash, initialOtpHash, StringComparison.Ordinal))
        {
            return ChangePasswordWithOtpResult.ConcurrencyConflict;
        }

        if (otp.ExpiresAt <= DateTime.UtcNow)
        {
            otp.IsUsed = true;
            await _context.SaveChangesAsync();
            return ChangePasswordWithOtpResult.OtpExpired;
        }

        if (otp.FailedAttempts >= otp.MaxAttempts)
        {
            otp.IsUsed = true;
            await _context.SaveChangesAsync();
            return ChangePasswordWithOtpResult.AttemptsExceeded;
        }

        if (!isOtpValid)
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= otp.MaxAttempts)
            {
                otp.IsUsed = true;
                await _context.SaveChangesAsync();
                return ChangePasswordWithOtpResult.AttemptsExceeded;
            }

            await _context.SaveChangesAsync();
            return ChangePasswordWithOtpResult.InvalidOtp;
        }

        account.PasswordHash = newPasswordHash;
        account.FailedLoginCount = 0;
        account.UpdatedAt = DateTime.UtcNow;

        List<PasswordChangeOtp> activeOtps = await _context.PasswordChangeOtps
            .Where(candidate => candidate.AccountId == account.Id && !candidate.IsUsed)
            .ToListAsync();
        foreach (PasswordChangeOtp activeOtp in activeOtps)
        {
            activeOtp.IsUsed = true;
        }

        await _context.SaveChangesAsync();
        return ChangePasswordWithOtpResult.Success;
    }

    private async Task InvalidatePasswordChangeOtpAsync(int otpId)
    {
        PasswordChangeOtp? otp = await _context.PasswordChangeOtps
            .FirstOrDefaultAsync(candidate => candidate.Id == otpId);
        if (otp is null || otp.IsUsed)
        {
            return;
        }

        otp.IsUsed = true;
        await _context.SaveChangesAsync();
    }

    // Legacy role-specific login returns null on any failure to preserve
    // existing client compatibility.
    private async Task<LoginResponseAPIViewModel?> LoginByRoleAsync(
        LoginRequestAPIViewModel request,
        string roleName)
    {
        (LoginResult result, LoginResponseAPIViewModel? account) = await LoginCoreAsync(
            request, requiredRoleName: roleName);

        if (result == LoginResult.Success)
        {
            return account;
        }

        return null;
    }

    private static bool IsInactive(Account account)
    {
        return account.Status != "Active" || account.DeletedAt is not null;
    }

    // Walk the full exception chain to detect a SQL Server deadlock (1205) at any depth.
    private static bool IsDeadlock(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current is SqlException sqlEx && sqlEx.Number == SqlDeadlockErrorNumber)
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }

    private static bool IsOtpUniqueConstraintViolation(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627) &&
                sqlException.Message.Contains(
                    "UQ_PasswordChangeOtp_ActiveAccount",
                    StringComparison.Ordinal))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
