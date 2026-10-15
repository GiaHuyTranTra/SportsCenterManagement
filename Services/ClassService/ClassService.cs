using Services.ClassScheduling;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Class;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;
using Services.NotificationService;
using Services.ScheduleQueryService;

namespace Services.ClassService;

public class ClassService : IClassService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;

    public ClassService(
        SportsCenterManagementContext context,
        IAuditLogService auditLogService,
        INotificationService notificationService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<PagedResult<ClassViewModel>> GetClassesPagedAsync(
        int page,
        int pageSize,
        string? search,
        int? sportId,
        string? status,
        bool isMember)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Class> query = _context.Classes.AsNoTracking()
            .Include(item => item.Discipline)
            .Include(item => item.ClassSessions);

        if (isMember)
        {
            query = query.Where(item => item.Status == "OPEN");
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status.Trim().ToUpperInvariant());
        }

        if (sportId.HasValue)
        {
            query = query.Where(item => item.DisciplineId == sportId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            query = query.Where(item => item.Name.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)));
        }

        int totalItems = await query.CountAsync();

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        List<Class> classes = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        List<ClassViewModel> items = new List<ClassViewModel>();
        foreach (Class currentClass in classes)
        {
            int upcomingCount = currentClass.ClassSessions.Count(session =>
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));

            int totalCount = currentClass.ClassSessions.Count;

            int maxActiveRegistrations = currentClass.ClassSessions
                .Where(session => session.Status == "SCHEDULED" &&
                    (session.SessionDate > today ||
                     (session.SessionDate == today && session.StartTime > currentTime)))
                .Select(session => session.BookedCount)
                .DefaultIfEmpty(0)
                .Max();

            items.Add(new ClassViewModel
            {
                Id = currentClass.Id,
                SportId = currentClass.DisciplineId,
                SportName = currentClass.Discipline?.Name ?? string.Empty,
                Name = currentClass.Name,
                Description = currentClass.Description,
                Capacity = currentClass.Capacity,
                Status = currentClass.Status,
                UpcomingSessionCount = upcomingCount,
                TotalSessionCount = totalCount,
                ActiveRegistrationCount = maxActiveRegistrations,
                CreatedAt = currentClass.CreatedAt,
                UpdatedAt = currentClass.UpdatedAt
            });
        }

        return new PagedResult<ClassViewModel>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<ClassViewModel?> GetClassByIdAsync(int id, bool isMember)
    {
        Class? currentClass = await _context.Classes.AsNoTracking()
            .Include(item => item.Discipline)
            .Include(item => item.ClassSessions)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (currentClass is null)
        {
            return null;
        }

        if (isMember && currentClass.Status != "OPEN")
        {
            return null;
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        int upcomingCount = currentClass.ClassSessions.Count(session =>
            session.Status == "SCHEDULED" &&
            (session.SessionDate > today ||
             (session.SessionDate == today && session.StartTime > currentTime)));

        int totalCount = currentClass.ClassSessions.Count;

        int maxActiveRegistrations = currentClass.ClassSessions
            .Where(session => session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)))
            .Select(session => session.BookedCount)
            .DefaultIfEmpty(0)
            .Max();

        return new ClassViewModel
        {
            Id = currentClass.Id,
            SportId = currentClass.DisciplineId,
            SportName = currentClass.Discipline?.Name ?? string.Empty,
            Name = currentClass.Name,
            Description = currentClass.Description,
            Capacity = currentClass.Capacity,
            Status = currentClass.Status,
            UpcomingSessionCount = upcomingCount,
            TotalSessionCount = totalCount,
            ActiveRegistrationCount = maxActiveRegistrations,
            CreatedAt = currentClass.CreatedAt,
            UpdatedAt = currentClass.UpdatedAt
        };
    }

    public async Task<ClassViewModel> CreateClassAsync(
        string actorAccountId,
        CreateClassAPIViewModel request)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        string normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Tên lớp không hợp lệ.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên lớp từ 1 đến 100 ký tự." } } });
        }

        if (request.Capacity <= 0)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Sức chứa lớp phải là số nguyên dương.",
                new Dictionary<string, string[]> { { "capacity", new[] { "Sức chứa phải lớn hơn 0." } } });
        }

        Discipline? sport = await _context.Disciplines.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.SportId);

        if (sport is null)
        {
            throw new BusinessRuleException("SPORT_NOT_FOUND", "Không tìm thấy bộ môn.");
        }

        if (!sport.IsActive)
        {
            throw new BusinessRuleException("SPORT_INACTIVE", "Bộ môn đã ngừng hoạt động không thể tạo lớp mới.");
        }

        bool duplicateName = await _context.Classes
            .AnyAsync(item => item.Status != "ARCHIVED" && (item.NormalizedName == Flow2Name.Normalize(normalizedName) || item.Name == normalizedName));

        if (duplicateName)
        {
            throw new BusinessRuleException(
                "CLASS_NAME_DUPLICATE",
                "Tên lớp học đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên lớp học đã tồn tại trong hệ thống." } } });
        }

        string initialStatus = string.IsNullOrWhiteSpace(request.Status)
            ? "DRAFT"
            : request.Status.Trim().ToUpperInvariant();

        if (initialStatus == "OPEN")
        {
            throw new BusinessRuleException(
                "CLASS_HAS_NO_VALID_SESSION",
                "Lớp học chỉ có thể mở khi có ít nhất một buổi học được lên lịch trong tương lai.");
        }

        DateTime now = DateTime.UtcNow;
        Class newClass = new Class
        {
            DisciplineId = request.SportId,
            NormalizedName = Flow2Name.Normalize(normalizedName),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Capacity = request.Capacity,
            Status = initialStatus,
            CreatedAt = now,
            CreatedBy = actorAccountId
        };

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.Classes.AddAsync(newClass);
            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CLASS_CREATED",
                "Class",
                newClass.Id.ToString(),
                "Tạo mới lớp học " + normalizedName + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            return new ClassViewModel
            {
                Id = newClass.Id,
                SportId = newClass.DisciplineId,
                SportName = sport.Name,
                Name = newClass.Name,
                Description = newClass.Description,
                Capacity = newClass.Capacity,
                Status = newClass.Status,
                UpcomingSessionCount = 0,
                TotalSessionCount = 0,
                ActiveRegistrationCount = 0,
                CreatedAt = newClass.CreatedAt,
                UpdatedAt = newClass.UpdatedAt
            };
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            throw new BusinessRuleException(
                "CLASS_NAME_DUPLICATE",
                "Tên lớp học đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên lớp học đã tồn tại trong hệ thống." } } });
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<ClassViewModel> UpdateClassAsync(
        string actorAccountId,
        int id,
        UpdateClassAPIViewModel request)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        string normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Tên lớp không hợp lệ.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên lớp từ 1 đến 100 ký tự." } } });
        }

        if (request.Capacity <= 0)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Sức chứa lớp phải là số nguyên dương.",
                new Dictionary<string, string[]> { { "capacity", new[] { "Sức chứa phải lớn hơn 0." } } });
        }

        Class? currentClass = await _context.Classes
            .Include(item => item.Discipline)
            .Include(item => item.ClassSessions)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        if (currentClass.Status == "ARCHIVED")
        {
            throw new BusinessRuleException("CLASS_ARCHIVED", "Lớp học đã lưu trữ không thể chỉnh sửa.");
        }

        Discipline? sport = await _context.Disciplines.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.SportId);

        if (sport is null)
        {
            throw new BusinessRuleException("SPORT_NOT_FOUND", "Không tìm thấy bộ môn.");
        }

        if (currentClass.DisciplineId != request.SportId && !sport.IsActive)
        {
            throw new BusinessRuleException("SPORT_INACTIVE", "Bộ môn đã ngừng hoạt động không thể gán cho lớp học.");
        }

        bool duplicateName = await _context.Classes
            .AnyAsync(item => item.Id != id && item.Status != "ARCHIVED" && (item.NormalizedName == Flow2Name.Normalize(normalizedName) || item.Name == normalizedName));

        if (duplicateName)
        {
            throw new BusinessRuleException(
                "CLASS_NAME_DUPLICATE",
                "Tên lớp học đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên lớp học đã tồn tại trong hệ thống." } } });
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        int maxBooked = currentClass.ClassSessions
            .Where(session => session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)))
            .Select(session => session.BookedCount)
            .DefaultIfEmpty(0)
            .Max();

        int maxSessionCapacity = currentClass.ClassSessions
            .Where(session => session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)))
            .Select(session => session.Capacity)
            .DefaultIfEmpty(0)
            .Max();

        if (request.Capacity < maxBooked || request.Capacity < maxSessionCapacity)
        {
            throw new BusinessRuleException(
                "CAPACITY_BELOW_REGISTRATIONS",
                "Sức chứa lớp không thể nhỏ hơn số người đã đăng ký hoặc sức chứa của các buổi học đã lập.",
                new Dictionary<string, string[]> { { "capacity", new[] { "Sức chứa phải lớn hơn hoặc bằng số lượng đăng ký hiện tại." } } });
        }

        string requestedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? currentClass.Status
            : request.Status.Trim().ToUpperInvariant();

        if (requestedStatus == "OPEN" && currentClass.Status != "OPEN")
        {
            bool hasUpcomingSession = currentClass.ClassSessions.Any(session =>
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));

            if (!hasUpcomingSession)
            {
                throw new BusinessRuleException(
                    "CLASS_HAS_NO_VALID_SESSION",
                    "Lớp học chỉ có thể mở khi có ít nhất một buổi học được lên lịch trong tương lai.");
            }
        }

        DateTime now = DateTime.UtcNow;
        currentClass.DisciplineId = request.SportId;
        currentClass.NormalizedName = Flow2Name.Normalize(normalizedName);
        currentClass.Name = normalizedName;
        currentClass.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        currentClass.Capacity = request.Capacity;
        currentClass.Status = requestedStatus;
        currentClass.UpdatedAt = now;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CLASS_UPDATED",
                "Class",
                currentClass.Id.ToString(),
                "Cập nhật thông tin lớp học " + normalizedName + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            int upcomingCount = currentClass.ClassSessions.Count(session =>
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));

            return new ClassViewModel
            {
                Id = currentClass.Id,
                SportId = currentClass.DisciplineId,
                SportName = sport.Name,
                Name = currentClass.Name,
                Description = currentClass.Description,
                Capacity = currentClass.Capacity,
                Status = currentClass.Status,
                UpcomingSessionCount = upcomingCount,
                TotalSessionCount = currentClass.ClassSessions.Count,
                ActiveRegistrationCount = maxBooked,
                CreatedAt = currentClass.CreatedAt,
                UpdatedAt = currentClass.UpdatedAt
            };
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            throw new BusinessRuleException(
                "CLASS_NAME_DUPLICATE",
                "Tên lớp học đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên lớp học đã tồn tại trong hệ thống." } } });
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<ClassViewModel> UpdateClassStatusAsync(
        string actorAccountId,
        int id,
        string status)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        Class? currentClass = await _context.Classes
            .Include(item => item.Discipline)
            .Include(item => item.ClassSessions)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        string normalizedStatus = status.Trim().ToUpperInvariant();
        if (normalizedStatus != "DRAFT" && normalizedStatus != "OPEN" &&
            normalizedStatus != "CLOSED" && normalizedStatus != "ARCHIVED")
        {
            throw new BusinessRuleException("VALIDATION_ERROR", "Trạng thái lớp không hợp lệ.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (normalizedStatus == "OPEN")
        {
            bool hasUpcomingSession = currentClass.ClassSessions.Any(session =>
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));

            if (!hasUpcomingSession)
            {
                throw new BusinessRuleException(
                    "CLASS_HAS_NO_VALID_SESSION",
                    "Lớp học chỉ có thể mở khi có ít nhất một buổi học được lên lịch trong tương lai.");
            }
        }

        currentClass.Status = normalizedStatus;
        currentClass.UpdatedAt = DateTime.UtcNow;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CLASS_STATUS_CHANGED",
                "Class",
                currentClass.Id.ToString(),
                "Cập nhật trạng thái lớp học thành " + normalizedStatus + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            int upcomingCount = currentClass.ClassSessions.Count(session =>
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));

            int maxBooked = currentClass.ClassSessions
                .Where(session => session.Status == "SCHEDULED" &&
                    (session.SessionDate > today ||
                     (session.SessionDate == today && session.StartTime > currentTime)))
                .Select(session => session.BookedCount)
                .DefaultIfEmpty(0)
                .Max();

            return new ClassViewModel
            {
                Id = currentClass.Id,
                SportId = currentClass.DisciplineId,
                SportName = currentClass.Discipline?.Name ?? string.Empty,
                Name = currentClass.Name,
                Description = currentClass.Description,
                Capacity = currentClass.Capacity,
                Status = currentClass.Status,
                UpcomingSessionCount = upcomingCount,
                TotalSessionCount = currentClass.ClassSessions.Count,
                ActiveRegistrationCount = maxBooked,
                CreatedAt = currentClass.CreatedAt,
                UpdatedAt = currentClass.UpdatedAt
            };
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<ClassCancelImpactViewModel> GetClassCancelImpactAsync(int classId)
    {
        Class? currentClass = await _context.Classes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == classId);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        List<ClassSession> upcomingSessions = await _context.ClassSessions.AsNoTracking()
            .Where(session => session.ClassId == classId &&
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)))
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartTime)
            .ToListAsync();

        List<int> sessionIds = upcomingSessions.Select(session => session.Id).ToList();

        List<ClassRegistration> bookedRegistrations = await _context.ClassRegistrations.AsNoTracking()
            .Where(registration => sessionIds.Contains(registration.SessionId) && registration.Status == "BOOKED")
            .ToListAsync();

        int affectedMemberCount = bookedRegistrations
            .Select(registration => registration.MemberId)
            .Distinct()
            .Count();

        string? nextStartsAt = null;
        if (upcomingSessions.Count > 0)
        {
            ClassSession earliest = upcomingSessions[0];
            DateTime sessionStart = DateTime.SpecifyKind(earliest.SessionDate.Date + earliest.StartTime, DateTimeKind.Unspecified);
            nextStartsAt = new DateTimeOffset(sessionStart, TimeSpan.FromHours(7)).ToString("yyyy-MM-ddTHH:mm:sszzz");
        }

        return new ClassCancelImpactViewModel
        {
            Scope = "CLASS_FUTURE_SESSIONS",
            AffectedSessionCount = upcomingSessions.Count,
            AffectedRegistrationCount = bookedRegistrations.Count,
            AffectedMemberCount = affectedMemberCount,
            NextSessionStartsAt = nextStartsAt
        };
    }

    public async Task<CancelFutureSessionsResultViewModel> CancelFutureSessionsAsync(
        string actorAccountId,
        int classId,
        string reason)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        string normalizedReason = reason.Trim();
        if (normalizedReason.Length < 3 || normalizedReason.Length > 500)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Lý do hủy từ 3 đến 500 ký tự.",
                new Dictionary<string, string[]> { { "reason", new[] { "Lý do hủy phải từ 3 đến 500 ký tự." } } });
        }

        Class? currentClass = await _context.Classes
            .FirstOrDefaultAsync(item => item.Id == classId);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        List<ClassSession> sessionsToCancel = await _context.ClassSessions
            .Where(session => session.ClassId == classId &&
                session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)))
            .ToListAsync();

        if (sessionsToCancel.Count == 0)
        {
            throw new BusinessRuleException(
                "NO_CANCELABLE_SESSION",
                "Lớp học không còn buổi học nào trong tương lai để hủy.");
        }

        List<int> sessionIds = sessionsToCancel.Select(session => session.Id).ToList();

        List<ClassRegistration> registrationsToCancel = await _context.ClassRegistrations
            .Where(registration => sessionIds.Contains(registration.SessionId) && registration.Status == "BOOKED")
            .ToListAsync();

        DateTime now = DateTime.UtcNow;
        IDbContextTransaction? transaction = operation.Transaction;

        try
        {

            foreach (ClassSession session in sessionsToCancel)
            {
                session.Status = "CANCELED";
                session.CancelReason = normalizedReason;
                session.CanceledAt = now;
                session.CanceledBy = actorAccountId;
                session.BookedCount = 0;
                session.UpdatedAt = now;

                await _auditLogService.StageAuditLogAsync(
                    actorAccountId,
                    "SESSION_CANCELED",
                    "ClassSession",
                    session.Id.ToString(),
                    "Hủy buổi học ID " + session.Id + " thuộc lớp " + currentClass.Name + ". Lý do: " + normalizedReason);

                DateTime sessionStart = session.SessionDate.Date + session.StartTime;
                await _notificationService.SendNotificationAsync(
                    session.CoachId,
                    "SESSION_CANCELED",
                    "Buổi dạy của bạn đã bị hủy",
                    "Buổi dạy lớp " + currentClass.Name + " ngày " + session.SessionDate.ToString("yyyy-MM-dd") + " đã bị hủy. Lý do: " + normalizedReason,
                    "ClassSession",
                    session.Id.ToString(),
                    sessionStart);
            }

            foreach (ClassRegistration registration in registrationsToCancel)
            {
                registration.Status = "CANCELED";
                registration.CancelReason = normalizedReason;
                registration.CanceledAt = now;
                registration.CanceledBy = actorAccountId;

                await _auditLogService.StageAuditLogAsync(
                    actorAccountId,
                    "CLASS_REGISTRATION_CANCELED",
                    "ClassRegistration",
                    registration.Id.ToString(),
                    "Hủy đăng ký học viên ID " + registration.MemberId + " do hủy lịch lớp học.");

                ClassSession? relatedSession = sessionsToCancel.FirstOrDefault(item => item.Id == registration.SessionId);
                DateTime sessionStart = relatedSession != null
                    ? relatedSession.SessionDate.Date + relatedSession.StartTime
                    : now;

                await _notificationService.SendNotificationAsync(
                    registration.MemberId,
                    "SESSION_CANCELED",
                    "Buổi học bạn đã đăng ký bị hủy",
                    "Buổi học lớp " + currentClass.Name + " ngày " + (relatedSession?.SessionDate.ToString("yyyy-MM-dd") ?? "") + " đã bị hủy. Lý do: " + normalizedReason,
                    "ClassSession",
                    registration.SessionId.ToString(),
                    sessionStart);
            }

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CLASS_SESSIONS_CANCELED",
                "Class",
                currentClass.Id.ToString(),
                "Hủy " + sessionsToCancel.Count + " buổi học tương lai của lớp " + currentClass.Name + ". Lý do: " + normalizedReason);

            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            return new CancelFutureSessionsResultViewModel
            {
                CanceledSessionCount = sessionsToCancel.Count
            };
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is SqlException sqlException && (sqlException.Number == 2627 || sqlException.Number == 2601))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }
}
