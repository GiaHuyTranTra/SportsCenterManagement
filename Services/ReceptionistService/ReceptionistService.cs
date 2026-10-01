using System.Data;
using APIViewModel.Receptionist;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;
using Services.PasswordHashService;

namespace Services.ReceptionistService;

public class ReceptionistService : IReceptionistService
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 20;

    private readonly SportsCenterManagementContext _context;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistService(
        SportsCenterManagementContext context,
        IPasswordHashService passwordHashService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHashService = passwordHashService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedReceptionistAPIViewModel> GetReceptionistsAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        string? workShift)
    {
        int normalizedPage = page <= 0 ? 1 : page;
        int normalizedPageSize = pageSize <= 0 || pageSize > MaximumPageSize
            ? DefaultPageSize
            : pageSize;
        string? normalizedSearch = NormalizeOptional(search);
        string? normalizedStatus = NormalizeStatus(status);
        string? normalizedWorkShift = NormalizeOptional(workShift);

        IQueryable<Receptionist> query = BuildReceptionistQuery(tracking: false)
            .Where(item =>
                item.Account.Role.Name == "Receptionist" &&
                item.Account.DeletedAt == null);

        if (normalizedSearch is not null)
        {
            query = query.Where(item =>
                item.FullName.Contains(normalizedSearch) ||
                item.Account.Email.Contains(normalizedSearch) ||
                item.Account.Phone != null && item.Account.Phone.Contains(normalizedSearch));
        }

        if (normalizedStatus is not null)
        {
            query = query.Where(item => item.Account.Status == normalizedStatus);
        }

        if (normalizedWorkShift is not null)
        {
            query = query.Where(item =>
                item.WorkShift != null && item.WorkShift.Contains(normalizedWorkShift));
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / normalizedPageSize);
        List<Receptionist> receptionists = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.AccountId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new PagedReceptionistAPIViewModel
        {
            Items = receptionists.Select(MapListItem).ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
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

        Receptionist? receptionist = await BuildReceptionistQuery(tracking: false)
            .FirstOrDefaultAsync(item =>
                item.AccountId == accountId &&
                item.Account.Role.Name == "Receptionist" &&
                item.Account.DeletedAt == null);
        return receptionist is null ? null : MapDetail(receptionist);
    }

    public async Task<(CreateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data)>
        CreateReceptionistAsync(
            string actorAccountId,
            CreateReceptionistAPIViewModel request)
    {
        string? normalizedEmail = NormalizeEmail(request.Email);
        string? normalizedFullName = NormalizeRequired(request.FullName, 100);
        string? normalizedPhone = NormalizePhone(request.Phone);
        string? normalizedWorkShift = NormalizeOptional(request.WorkShift);
        if (normalizedEmail is null ||
            normalizedFullName is null ||
            normalizedPhone is null ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 8 ||
            normalizedWorkShift is not null && normalizedWorkShift.Length > 100)
        {
            return (CreateReceptionistResult.InvalidData, null);
        }

        if (await _context.Accounts.AnyAsync(item => item.Email == normalizedEmail))
        {
            return (CreateReceptionistResult.DuplicateEmail, null);
        }

        if (await _context.Accounts.AnyAsync(item => item.Phone == normalizedPhone))
        {
            return (CreateReceptionistResult.DuplicatePhone, null);
        }

        Role? role = await _context.Roles.FirstOrDefaultAsync(item => item.Name == "Receptionist");
        if (role is null)
        {
            return (CreateReceptionistResult.ReceptionistRoleMissing, null);
        }

        DateTime now = DateTime.UtcNow;
        Account account = new Account
        {
            Id = Guid.NewGuid().ToString(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            PasswordHash = _passwordHashService.HashPassword(request.Password),
            RoleId = role.Id,
            Role = role,
            Status = "Active",
            FailedLoginCount = 0,
            IsLocked = false,
            CreatedAt = now
        };
        Receptionist receptionist = new Receptionist
        {
            AccountId = account.Id,
            Account = account,
            FullName = normalizedFullName,
            WorkShift = normalizedWorkShift,
            CreatedAt = now
        };
        account.Receptionist = receptionist;

        await _context.Accounts.AddAsync(account);
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "CREATE",
            "Receptionist",
            account.Id,
            "Created receptionist account " + normalizedEmail + ".");

        try
        {
            await _context.SaveChangesAsync();
            return (CreateReceptionistResult.Success, MapDetail(receptionist));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return (CreateReceptionistResult.ConcurrencyConflict, null);
        }
        catch (DbUpdateException)
        {
            return (CreateReceptionistResult.ConcurrencyConflict, null);
        }
    }

    public async Task<(UpdateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data)>
        UpdateReceptionistAsync(
            string actorAccountId,
            string receptionistAccountId,
            UpdateManagedReceptionistAPIViewModel request)
    {
        if (request.FullName is null && request.Phone is null && request.WorkShift is null)
        {
            return (UpdateReceptionistResult.NoChanges, null);
        }

        string? normalizedFullName = request.FullName is null
            ? null
            : NormalizeRequired(request.FullName, 100);
        string? normalizedPhone = request.Phone is null
            ? null
            : NormalizePhone(request.Phone);
        string? normalizedWorkShift = request.WorkShift is null
            ? null
            : NormalizeOptional(request.WorkShift);
        if (request.FullName is not null && normalizedFullName is null ||
            request.Phone is not null && normalizedPhone is null ||
            normalizedWorkShift is not null && normalizedWorkShift.Length > 100)
        {
            return (UpdateReceptionistResult.InvalidData, null);
        }

        IDbContextTransaction? transaction = await BeginTransactionAsync();
        try
        {
            Receptionist? receptionist = await GetReceptionistForUpdateAsync(receptionistAccountId);
            if (receptionist is null)
            {
                await RollbackAsync(transaction);
                return (UpdateReceptionistResult.NotFound, null);
            }

            if (normalizedPhone is not null && await _context.Accounts.AnyAsync(item =>
                item.Id != receptionistAccountId && item.Phone == normalizedPhone))
            {
                await RollbackAsync(transaction);
                return (UpdateReceptionistResult.DuplicatePhone, null);
            }

            bool changed = false;
            if (normalizedFullName is not null && receptionist.FullName != normalizedFullName)
            {
                receptionist.FullName = normalizedFullName;
                changed = true;
            }

            if (normalizedPhone is not null && receptionist.Account.Phone != normalizedPhone)
            {
                receptionist.Account.Phone = normalizedPhone;
                changed = true;
            }

            if (request.WorkShift is not null && receptionist.WorkShift != normalizedWorkShift)
            {
                receptionist.WorkShift = normalizedWorkShift;
                changed = true;
            }

            if (!changed)
            {
                await RollbackAsync(transaction);
                return (UpdateReceptionistResult.NoChanges, null);
            }

            receptionist.Account.UpdatedAt = DateTime.UtcNow;
            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "UPDATE",
                "Receptionist",
                receptionist.AccountId,
                "Updated receptionist profile.");
            await _context.SaveChangesAsync();
            await CommitAsync(transaction);
            return (UpdateReceptionistResult.Success, MapDetail(receptionist));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await RollbackAsync(transaction);
            return (UpdateReceptionistResult.ConcurrencyConflict, null);
        }
        catch (DbUpdateException)
        {
            await RollbackAsync(transaction);
            return (UpdateReceptionistResult.ConcurrencyConflict, null);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<UpdateReceptionistStatusResult> UpdateReceptionistStatusAsync(
        string actorAccountId,
        string receptionistAccountId,
        string status)
    {
        string? normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus is null)
        {
            return UpdateReceptionistStatusResult.InvalidStatus;
        }

        Receptionist? receptionist = await _context.Receptionists
            .Include(item => item.Account)
                .ThenInclude(account => account.Role)
            .FirstOrDefaultAsync(item =>
                item.AccountId == receptionistAccountId &&
                item.Account.Role.Name == "Receptionist" &&
                item.Account.DeletedAt == null);
        if (receptionist is null)
        {
            return UpdateReceptionistStatusResult.NotFound;
        }

        if (receptionist.Account.Status == normalizedStatus)
        {
            return UpdateReceptionistStatusResult.NoChanges;
        }

        receptionist.Account.Status = normalizedStatus;
        receptionist.Account.UpdatedAt = DateTime.UtcNow;
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "UPDATE_STATUS",
            "Receptionist",
            receptionist.AccountId,
            "Changed receptionist account status to " + normalizedStatus + ".");

        try
        {
            await _context.SaveChangesAsync();
            return UpdateReceptionistStatusResult.Success;
        }
        catch (DbUpdateException)
        {
            return UpdateReceptionistStatusResult.ConcurrencyConflict;
        }
    }

    public async Task<DeleteReceptionistResult> SoftDeleteReceptionistAsync(
        string actorAccountId,
        string receptionistAccountId)
    {
        Receptionist? receptionist = await _context.Receptionists
            .Include(item => item.Account)
                .ThenInclude(account => account.Role)
            .FirstOrDefaultAsync(item =>
                item.AccountId == receptionistAccountId &&
                item.Account.Role.Name == "Receptionist");
        if (receptionist is null)
        {
            return DeleteReceptionistResult.NotFound;
        }

        if (receptionist.Account.DeletedAt is not null)
        {
            return DeleteReceptionistResult.AlreadyDeleted;
        }

        DateTime now = DateTime.UtcNow;
        receptionist.Account.DeletedAt = now;
        receptionist.Account.Status = "Inactive";
        receptionist.Account.UpdatedAt = now;
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "DELETE",
            "Receptionist",
            receptionist.AccountId,
            "Soft-deleted receptionist account.");

        try
        {
            await _context.SaveChangesAsync();
            return DeleteReceptionistResult.Success;
        }
        catch (DbUpdateException)
        {
            return DeleteReceptionistResult.ConcurrencyConflict;
        }
    }

    private IQueryable<Receptionist> BuildReceptionistQuery(bool tracking)
    {
        IQueryable<Receptionist> query = _context.Receptionists
            .Include(item => item.Account)
                .ThenInclude(account => account.Role);
        return tracking ? query : query.AsNoTracking();
    }

    private async Task<Receptionist?> GetReceptionistForUpdateAsync(string accountId)
    {
        IQueryable<Receptionist> query;
        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            query = _context.Receptionists.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Receptionist] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {accountId}");
        }
        else
        {
            query = _context.Receptionists.Where(item => item.AccountId == accountId);
        }

        return await query
            .Include(item => item.Account)
                .ThenInclude(account => account.Role)
            .FirstOrDefaultAsync(item =>
                item.AccountId == accountId &&
                item.Account.Role.Name == "Receptionist" &&
                item.Account.DeletedAt == null);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (!_context.Database.IsRelational())
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    }

    private static async Task CommitAsync(IDbContextTransaction? transaction)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
    }

    private static async Task RollbackAsync(IDbContextTransaction? transaction)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync();
        }
    }

    private static ReceptionistListItemAPIViewModel MapListItem(Receptionist receptionist)
    {
        return new ReceptionistListItemAPIViewModel
        {
            AccountId = receptionist.AccountId,
            Email = receptionist.Account.Email,
            Phone = receptionist.Account.Phone,
            Status = receptionist.Account.Status,
            IsLocked = receptionist.Account.IsLocked,
            FullName = receptionist.FullName,
            WorkShift = receptionist.WorkShift,
            CreatedAt = receptionist.CreatedAt
        };
    }

    private static ReceptionistDetailAPIViewModel MapDetail(Receptionist receptionist)
    {
        return new ReceptionistDetailAPIViewModel
        {
            AccountId = receptionist.AccountId,
            Email = receptionist.Account.Email,
            Phone = receptionist.Account.Phone,
            Status = receptionist.Account.Status,
            IsLocked = receptionist.Account.IsLocked,
            FullName = receptionist.FullName,
            WorkShift = receptionist.WorkShift,
            CreatedAt = receptionist.CreatedAt,
            UpdatedAt = receptionist.Account.UpdatedAt
        };
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = value.Trim().ToLowerInvariant();
        return normalizedValue.Length <= 150 && normalizedValue.Contains('@')
            ? normalizedValue
            : null;
    }

    private static string? NormalizeRequired(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = value.Trim();
        return normalizedValue.Length <= maximumLength ? normalizedValue : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizePhone(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string normalizedValue = value.Trim();
        bool valid = normalizedValue.Length == 10 &&
            normalizedValue.All(character => character >= '0' && character <= '9');
        return valid ? normalizedValue : null;
    }

    private static string? NormalizeStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = value.Trim();
        if (string.Equals(normalizedValue, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Active";
        }

        if (string.Equals(normalizedValue, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return "Inactive";
        }

        return null;
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
