using System.Data;
using APIViewModel.Coach;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;
using Services.PasswordHashService;

namespace Services.CoachService;

public class CoachService : ICoachService
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 20;

    private readonly SportsCenterManagementContext _context;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IAuditLogService _auditLogService;

    public CoachService(
        SportsCenterManagementContext context,
        IPasswordHashService passwordHashService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _passwordHashService = passwordHashService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedCoachAPIViewModel> GetCoachesAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        int? disciplineId)
    {
        int normalizedPage = page <= 0 ? 1 : page;
        int normalizedPageSize = pageSize <= 0 || pageSize > MaximumPageSize
            ? DefaultPageSize
            : pageSize;
        string? normalizedSearch = NormalizeOptional(search);
        string? normalizedStatus = NormalizeStatus(status);

        IQueryable<Coach> query = BuildCoachQuery(tracking: false)
            .Where(item =>
                item.Account.Role.Name == "Coach" &&
                item.Account.DeletedAt == null);

        if (normalizedSearch is not null)
        {
            query = query.Where(item =>
                item.FullName.Contains(normalizedSearch) ||
                item.Account.Email.Contains(normalizedSearch) ||
                item.Account.Phone != null && item.Account.Phone.Contains(normalizedSearch) ||
                item.CoachDisciplines.Any(link =>
                    link.Discipline.Name.Contains(normalizedSearch)));
        }

        if (normalizedStatus is not null)
        {
            query = query.Where(item => item.Account.Status == normalizedStatus);
        }

        if (disciplineId.HasValue)
        {
            query = query.Where(item => item.CoachDisciplines.Any(link =>
                link.DisciplineId == disciplineId.Value));
        }

        int totalItems = await query.CountAsync();
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / normalizedPageSize);
        List<Coach> coaches = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.AccountId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        return new PagedCoachAPIViewModel
        {
            Items = coaches.Select(MapListItem).ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
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

        Coach? coach = await BuildCoachQuery(tracking: false)
            .FirstOrDefaultAsync(item =>
                item.AccountId == accountId &&
                item.Account.Role.Name == "Coach" &&
                item.Account.DeletedAt == null);
        return coach is null ? null : MapDetail(coach);
    }

    public async Task<(CreateCoachResult Result, CoachDetailAPIViewModel? Data)> CreateCoachAsync(
        string actorAccountId,
        CreateManagedCoachAPIViewModel request)
    {
        string? normalizedEmail = NormalizeEmail(request.Email);
        string? normalizedFullName = NormalizeRequired(request.FullName, 100);
        string? normalizedPhone = NormalizePhone(request.Phone);
        string? normalizedWorkSchedule = NormalizeOptional(request.WorkSchedule);
        List<int>? disciplineIds = NormalizeDisciplineIds(request.DisciplineIds, requireAny: true);
        if (normalizedEmail is null ||
            normalizedFullName is null ||
            normalizedPhone is null ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 8 ||
            disciplineIds is null)
        {
            return (CreateCoachResult.InvalidData, null);
        }

        bool duplicateEmail = await _context.Accounts
            .AnyAsync(item => item.Email == normalizedEmail);
        if (duplicateEmail)
        {
            return (CreateCoachResult.DuplicateEmail, null);
        }

        bool duplicatePhone = await _context.Accounts
            .AnyAsync(item => item.Phone == normalizedPhone);
        if (duplicatePhone)
        {
            return (CreateCoachResult.DuplicatePhone, null);
        }

        Role? coachRole = await _context.Roles
            .FirstOrDefaultAsync(item => item.Name == "Coach");
        if (coachRole is null)
        {
            return (CreateCoachResult.CoachRoleMissing, null);
        }

        List<Discipline> disciplines = await _context.Disciplines
            .Where(item => disciplineIds.Contains(item.Id) && item.IsActive)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync();
        if (disciplines.Count != disciplineIds.Count)
        {
            return (CreateCoachResult.DisciplineNotFoundOrInactive, null);
        }

        DateTime now = DateTime.UtcNow;
        Account account = new Account
        {
            Id = Guid.NewGuid().ToString(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            PasswordHash = _passwordHashService.HashPassword(request.Password),
            RoleId = coachRole.Id,
            Role = coachRole,
            Status = "Active",
            FailedLoginCount = 0,
            IsLocked = false,
            CreatedAt = now
        };
        Coach coach = new Coach
        {
            AccountId = account.Id,
            Account = account,
            FullName = normalizedFullName,
            WorkSchedule = normalizedWorkSchedule,
            Specialization = JoinDisciplineNames(disciplines),
            CreatedAt = now
        };
        account.Coach = coach;

        foreach (Discipline discipline in disciplines)
        {
            coach.CoachDisciplines.Add(new CoachDiscipline
            {
                CoachAccountId = account.Id,
                DisciplineId = discipline.Id,
                CoachAccount = coach,
                Discipline = discipline,
                CreatedAt = now
            });
        }

        await _context.Accounts.AddAsync(account);
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "CREATE",
            "Coach",
            account.Id,
            "Created coach account " + normalizedEmail + ".");

        try
        {
            await _context.SaveChangesAsync();
            return (CreateCoachResult.Success, MapDetail(coach));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return (CreateCoachResult.ConcurrencyConflict, null);
        }
        catch (DbUpdateException)
        {
            return (CreateCoachResult.ConcurrencyConflict, null);
        }
    }

    public async Task<(UpdateCoachResult Result, CoachDetailAPIViewModel? Data)> UpdateCoachAsync(
        string actorAccountId,
        string coachAccountId,
        UpdateManagedCoachAPIViewModel request)
    {
        if (request.FullName is null &&
            request.Phone is null &&
            request.WorkSchedule is null &&
            request.DisciplineIds is null)
        {
            return (UpdateCoachResult.NoChanges, null);
        }

        string? normalizedFullName = request.FullName is null
            ? null
            : NormalizeRequired(request.FullName, 100);
        string? normalizedPhone = request.Phone is null
            ? null
            : NormalizePhone(request.Phone);
        string? normalizedWorkSchedule = request.WorkSchedule is null
            ? null
            : NormalizeOptional(request.WorkSchedule);
        List<int>? disciplineIds = request.DisciplineIds is null
            ? null
            : NormalizeDisciplineIds(request.DisciplineIds, requireAny: false);
        if (request.FullName is not null && normalizedFullName is null ||
            request.Phone is not null && normalizedPhone is null ||
            request.DisciplineIds is not null && disciplineIds is null)
        {
            return (UpdateCoachResult.InvalidData, null);
        }

        List<Discipline>? requestedDisciplines = null;
        if (disciplineIds is not null)
        {
            requestedDisciplines = await _context.Disciplines
                .Where(item => disciplineIds.Contains(item.Id) && item.IsActive)
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .ToListAsync();
            if (requestedDisciplines.Count != disciplineIds.Count)
            {
                return (UpdateCoachResult.DisciplineNotFoundOrInactive, null);
            }
        }

        IDbContextTransaction? transaction = await BeginTransactionAsync();
        try
        {
            Coach? coach = await GetCoachForUpdateAsync(coachAccountId);
            if (coach is null)
            {
                await RollbackAsync(transaction);
                return (UpdateCoachResult.NotFound, null);
            }

            if (normalizedPhone is not null)
            {
                bool duplicatePhone = await _context.Accounts.AnyAsync(item =>
                    item.Id != coachAccountId && item.Phone == normalizedPhone);
                if (duplicatePhone)
                {
                    await RollbackAsync(transaction);
                    return (UpdateCoachResult.DuplicatePhone, null);
                }
            }

            bool changed = false;
            if (normalizedFullName is not null && coach.FullName != normalizedFullName)
            {
                coach.FullName = normalizedFullName;
                changed = true;
            }

            if (request.Phone is not null && coach.Account.Phone != normalizedPhone)
            {
                coach.Account.Phone = normalizedPhone;
                changed = true;
            }

            if (request.WorkSchedule is not null && coach.WorkSchedule != normalizedWorkSchedule)
            {
                coach.WorkSchedule = normalizedWorkSchedule;
                changed = true;
            }

            if (disciplineIds is not null && requestedDisciplines is not null)
            {
                List<CoachDiscipline> activeLinks = coach.CoachDisciplines
                    .Where(link => link.Discipline.IsActive)
                    .ToList();
                List<CoachDiscipline> linksToRemove = activeLinks
                    .Where(link => !disciplineIds.Contains(link.DisciplineId))
                    .ToList();
                List<int> existingIds = coach.CoachDisciplines
                    .Select(link => link.DisciplineId)
                    .ToList();
                List<Discipline> disciplinesToAdd = requestedDisciplines
                    .Where(item => !existingIds.Contains(item.Id))
                    .ToList();

                if (linksToRemove.Count > 0 || disciplinesToAdd.Count > 0)
                {
                    _context.CoachDisciplines.RemoveRange(linksToRemove);
                    foreach (Discipline discipline in disciplinesToAdd)
                    {
                        coach.CoachDisciplines.Add(new CoachDiscipline
                        {
                            CoachAccountId = coach.AccountId,
                            DisciplineId = discipline.Id,
                            CoachAccount = coach,
                            Discipline = discipline,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    List<Discipline> preservedInactive = coach.CoachDisciplines
                        .Where(link =>
                            !link.Discipline.IsActive &&
                            !linksToRemove.Contains(link))
                        .Select(link => link.Discipline)
                        .ToList();
                    List<Discipline> finalDisciplines = preservedInactive
                        .Concat(requestedDisciplines)
                        .GroupBy(item => item.Id)
                        .Select(group => group.First())
                        .OrderBy(item => item.Name)
                        .ThenBy(item => item.Id)
                        .ToList();
                    coach.Specialization = JoinDisciplineNames(finalDisciplines);
                    changed = true;
                }
            }

            if (!changed)
            {
                await RollbackAsync(transaction);
                return (UpdateCoachResult.NoChanges, null);
            }

            coach.Account.UpdatedAt = DateTime.UtcNow;
            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "UPDATE",
                "Coach",
                coach.AccountId,
                "Updated coach profile.");
            await _context.SaveChangesAsync();
            await CommitAsync(transaction);
            return (UpdateCoachResult.Success, MapDetail(coach));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await RollbackAsync(transaction);
            return (UpdateCoachResult.ConcurrencyConflict, null);
        }
        catch (DbUpdateException)
        {
            await RollbackAsync(transaction);
            return (UpdateCoachResult.ConcurrencyConflict, null);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<UpdateCoachStatusResult> UpdateCoachStatusAsync(
        string actorAccountId,
        string coachAccountId,
        string status)
    {
        string? normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus is null)
        {
            return UpdateCoachStatusResult.InvalidStatus;
        }

        Coach? coach = await _context.Coaches
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item =>
                item.AccountId == coachAccountId &&
                item.Account.DeletedAt == null);
        if (coach is null)
        {
            return UpdateCoachStatusResult.NotFound;
        }

        if (coach.Account.Status == normalizedStatus)
        {
            return UpdateCoachStatusResult.NoChanges;
        }

        coach.Account.Status = normalizedStatus;
        coach.Account.UpdatedAt = DateTime.UtcNow;
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "UPDATE_STATUS",
            "Coach",
            coach.AccountId,
            "Changed coach account status to " + normalizedStatus + ".");

        try
        {
            await _context.SaveChangesAsync();
            return UpdateCoachStatusResult.Success;
        }
        catch (DbUpdateException)
        {
            return UpdateCoachStatusResult.ConcurrencyConflict;
        }
    }

    public async Task<DeleteCoachResult> SoftDeleteCoachAsync(
        string actorAccountId,
        string coachAccountId)
    {
        Coach? coach = await _context.Coaches
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.AccountId == coachAccountId);
        if (coach is null)
        {
            return DeleteCoachResult.NotFound;
        }

        if (coach.Account.DeletedAt is not null)
        {
            return DeleteCoachResult.AlreadyDeleted;
        }

        DateTime now = DateTime.UtcNow;
        coach.Account.DeletedAt = now;
        coach.Account.Status = "Inactive";
        coach.Account.UpdatedAt = now;
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "DELETE",
            "Coach",
            coach.AccountId,
            "Soft-deleted coach account.");

        try
        {
            await _context.SaveChangesAsync();
            return DeleteCoachResult.Success;
        }
        catch (DbUpdateException)
        {
            return DeleteCoachResult.ConcurrencyConflict;
        }
    }

    private IQueryable<Coach> BuildCoachQuery(bool tracking)
    {
        IQueryable<Coach> query = _context.Coaches
            .Include(item => item.Account)
                .ThenInclude(account => account.Role)
            .Include(item => item.CoachDisciplines)
                .ThenInclude(link => link.Discipline);
        return tracking ? query : query.AsNoTracking();
    }

    private async Task<Coach?> GetCoachForUpdateAsync(string accountId)
    {
        IQueryable<Coach> query;
        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            query = _context.Coaches.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Coach] WITH (UPDLOCK, HOLDLOCK) WHERE [AccountId] = {accountId}");
        }
        else
        {
            query = _context.Coaches.Where(item => item.AccountId == accountId);
        }

        return await query
            .Include(item => item.Account)
                .ThenInclude(account => account.Role)
            .Include(item => item.CoachDisciplines)
                .ThenInclude(link => link.Discipline)
            .FirstOrDefaultAsync(item =>
                item.AccountId == accountId &&
                item.Account.Role.Name == "Coach" &&
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

    private static CoachListItemAPIViewModel MapListItem(Coach coach)
    {
        return new CoachListItemAPIViewModel
        {
            AccountId = coach.AccountId,
            Email = coach.Account.Email,
            Phone = coach.Account.Phone,
            Status = coach.Account.Status,
            IsLocked = coach.Account.IsLocked,
            FullName = coach.FullName,
            WorkSchedule = coach.WorkSchedule,
            Disciplines = MapDisciplines(coach),
            CreatedAt = coach.CreatedAt
        };
    }

    private static CoachDetailAPIViewModel MapDetail(Coach coach)
    {
        return new CoachDetailAPIViewModel
        {
            AccountId = coach.AccountId,
            Email = coach.Account.Email,
            Phone = coach.Account.Phone,
            Status = coach.Account.Status,
            IsLocked = coach.Account.IsLocked,
            FullName = coach.FullName,
            WorkSchedule = coach.WorkSchedule,
            Disciplines = MapDisciplines(coach),
            CreatedAt = coach.CreatedAt,
            UpdatedAt = coach.Account.UpdatedAt
        };
    }

    private static List<CoachDisciplineAPIViewModel> MapDisciplines(Coach coach)
    {
        return coach.CoachDisciplines
            .OrderBy(link => link.Discipline.Name)
            .ThenBy(link => link.DisciplineId)
            .Select(link => new CoachDisciplineAPIViewModel
            {
                DisciplineId = link.DisciplineId,
                DisciplineName = link.Discipline.Name,
                IsActive = link.Discipline.IsActive
            })
            .ToList();
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

    private static List<int>? NormalizeDisciplineIds(List<int>? values, bool requireAny)
    {
        if (values is null || requireAny && values.Count == 0)
        {
            return null;
        }

        if (values.Any(value => value <= 0) || values.Distinct().Count() != values.Count)
        {
            return null;
        }

        return values.OrderBy(value => value).ToList();
    }

    private static string JoinDisciplineNames(IEnumerable<Discipline> disciplines)
    {
        return string.Join(", ", disciplines
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => item.Name));
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
