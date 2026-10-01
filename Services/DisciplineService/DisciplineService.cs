using System.Data;
using APIViewModel.Discipline;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;

namespace Services.DisciplineService;

public class DisciplineService : IDisciplineService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IAuditLogService _auditLogService;

    public DisciplineService(
        SportsCenterManagementContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<List<DisciplineAPIViewModel>> GetDisciplinesAsync(bool activeOnly)
    {
        IQueryable<Discipline> query = _context.Disciplines.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new DisciplineAPIViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<(CreateDisciplineResult Result, DisciplineAPIViewModel? Data)>
        CreateDisciplineAsync(
            string actorAccountId,
            CreateDisciplineAPIViewModel request)
    {
        string? normalizedName = NormalizeRequiredName(request.Name);
        if (normalizedName is null)
        {
            return (CreateDisciplineResult.InvalidData, null);
        }

        string? normalizedDescription = NormalizeOptional(request.Description);
        bool duplicateName = await _context.Disciplines
            .AnyAsync(item => item.Name == normalizedName);
        if (duplicateName)
        {
            return (CreateDisciplineResult.DuplicateName, null);
        }

        DateTime now = DateTime.UtcNow;
        Discipline discipline = new Discipline
        {
            Name = normalizedName,
            Description = normalizedDescription,
            IsActive = true,
            CreatedAt = now
        };
        IDbContextTransaction? transaction = null;

        try
        {
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted);
            }

            await _context.Disciplines.AddAsync(discipline);
            await _context.SaveChangesAsync();
            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CREATE",
                "Discipline",
                discipline.Id.ToString(),
                "Created discipline " + normalizedName + ".");
            await _context.SaveChangesAsync();
            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }
            return (CreateDisciplineResult.Success, MapDiscipline(discipline));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            return (CreateDisciplineResult.DuplicateName, null);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            return (CreateDisciplineResult.ConcurrencyConflict, null);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<(UpdateDisciplineResult Result, DisciplineAPIViewModel? Data)>
        UpdateDisciplineAsync(
            string actorAccountId,
            int disciplineId,
            UpdateDisciplineAPIViewModel request)
    {
        if (disciplineId <= 0 || request.Name is null && request.Description is null)
        {
            return (UpdateDisciplineResult.InvalidData, null);
        }

        string? normalizedName = request.Name is null
            ? null
            : NormalizeRequiredName(request.Name);
        if (request.Name is not null && normalizedName is null)
        {
            return (UpdateDisciplineResult.InvalidData, null);
        }

        string? normalizedDescription = request.Description is null
            ? null
            : NormalizeOptional(request.Description);
        Discipline? discipline = await _context.Disciplines
            .FirstOrDefaultAsync(item => item.Id == disciplineId);
        if (discipline is null)
        {
            return (UpdateDisciplineResult.NotFound, null);
        }

        if (normalizedName is not null)
        {
            bool duplicateName = await _context.Disciplines.AnyAsync(item =>
                item.Id != disciplineId && item.Name == normalizedName);
            if (duplicateName)
            {
                return (UpdateDisciplineResult.DuplicateName, null);
            }
        }

        bool changed = false;
        if (normalizedName is not null && discipline.Name != normalizedName)
        {
            discipline.Name = normalizedName;
            changed = true;
        }

        if (request.Description is not null && discipline.Description != normalizedDescription)
        {
            discipline.Description = normalizedDescription;
            changed = true;
        }

        if (!changed)
        {
            return (UpdateDisciplineResult.NoChanges, null);
        }

        discipline.UpdatedAt = DateTime.UtcNow;
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "UPDATE",
            "Discipline",
            discipline.Id.ToString(),
            "Updated discipline " + discipline.Name + ".");

        try
        {
            await _context.SaveChangesAsync();
            return (UpdateDisciplineResult.Success, MapDiscipline(discipline));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return (UpdateDisciplineResult.DuplicateName, null);
        }
        catch (DbUpdateException)
        {
            return (UpdateDisciplineResult.ConcurrencyConflict, null);
        }
    }

    public async Task<UpdateDisciplineStatusResult> UpdateDisciplineStatusAsync(
        string actorAccountId,
        int disciplineId,
        bool isActive)
    {
        if (disciplineId <= 0)
        {
            return UpdateDisciplineStatusResult.NotFound;
        }

        Discipline? discipline = await _context.Disciplines
            .FirstOrDefaultAsync(item => item.Id == disciplineId);
        if (discipline is null)
        {
            return UpdateDisciplineStatusResult.NotFound;
        }

        if (discipline.IsActive == isActive)
        {
            return UpdateDisciplineStatusResult.NoChanges;
        }

        discipline.IsActive = isActive;
        discipline.UpdatedAt = DateTime.UtcNow;
        await _auditLogService.StageAuditLogAsync(
            actorAccountId,
            "UPDATE_STATUS",
            "Discipline",
            discipline.Id.ToString(),
            "Changed discipline status to " + (isActive ? "Active." : "Inactive."));

        try
        {
            await _context.SaveChangesAsync();
            return UpdateDisciplineStatusResult.Success;
        }
        catch (DbUpdateException)
        {
            return UpdateDisciplineStatusResult.ConcurrencyConflict;
        }
    }

    private static DisciplineAPIViewModel MapDiscipline(Discipline discipline)
    {
        return new DisciplineAPIViewModel
        {
            Id = discipline.Id,
            Name = discipline.Name,
            Description = discipline.Description,
            IsActive = discipline.IsActive,
            CreatedAt = discipline.CreatedAt,
            UpdatedAt = discipline.UpdatedAt
        };
    }

    private static string? NormalizeRequiredName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = value.Trim();
        return normalizedValue.Length is >= 2 and <= 80 ? normalizedValue : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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
