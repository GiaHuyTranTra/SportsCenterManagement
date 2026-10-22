using Services.ClassScheduling;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassSession;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;
using Services.NotificationService;
using Services.ScheduleQueryService;

namespace Services.ClassSessionService;

public class ClassSessionService : IClassSessionService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;

    public ClassSessionService(
        SportsCenterManagementContext context,
        IAuditLogService auditLogService,
        INotificationService notificationService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<PagedResult<ClassSessionViewDto>> GetSessionsPagedAsync(
        DateTime? from,
        DateTime? to,
        int? classId,
        string? coachId,
        int? roomId,
        int? sportId,
        string? status,
        int page,
        int pageSize,
        bool isMember)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<ClassSession> query = _context.ClassSessions.AsNoTracking()
            .Include(session => session.Room)
            .Include(session => session.Class)
                .ThenInclude(c => c.Discipline);

        if (isMember)
        {
            DateTime localNow = DateTime.UtcNow.AddHours(7);
            DateTime today = localNow.Date;
            TimeSpan currentTime = localNow.TimeOfDay;

            query = query.Where(session => session.Status == "SCHEDULED" &&
                session.Class.Status == "OPEN" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            string normalizedStatus = status.Trim().ToUpperInvariant();
            if (normalizedStatus == "CANCELLED")
            {
                normalizedStatus = "CANCELED";
            }
            query = query.Where(session => session.Status == normalizedStatus);
        }

        if (from.HasValue)
        {
            query = query.Where(session => session.SessionDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(session => session.SessionDate <= to.Value.Date);
        }

        if (classId.HasValue)
        {
            query = query.Where(session => session.ClassId == classId.Value);
        }

        if (!string.IsNullOrWhiteSpace(coachId))
        {
            query = query.Where(session => session.CoachId == coachId);
        }

        if (roomId.HasValue)
        {
            query = query.Where(session => session.RoomId == roomId.Value);
        }

        if (sportId.HasValue)
        {
            query = query.Where(session => session.Class.DisciplineId == sportId.Value);
        }

        int totalItems = await query.CountAsync();

        List<ClassSession> sessions = await query
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartTime)
            .ThenBy(session => session.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(
            sessions.Select(session => session.CoachId));

        List<ClassSessionViewDto> items = sessions
            .Select(session => MapSession(session, coachNames))
            .ToList();

        return new PagedResult<ClassSessionViewDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<ClassSessionViewDto?> GetSessionByIdAsync(int id, bool isMember)
    {
        ClassSession? session = await _context.ClassSessions.AsNoTracking()
            .Include(item => item.Room)
            .Include(item => item.Class)
                .ThenInclude(c => c.Discipline)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (session is null)
        {
            return null;
        }

        if (isMember)
        {
            DateTime localNow = DateTime.UtcNow.AddHours(7);
            DateTime today = localNow.Date;
            TimeSpan currentTime = localNow.TimeOfDay;

            bool isAvailable = session.Status == "SCHEDULED" &&
                session.Class.Status == "OPEN" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime));

            if (!isAvailable)
            {
                return null;
            }
        }

        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(new[] { session.CoachId });
        return MapSession(session, coachNames);
    }

    public async Task<ClassSessionViewDto> CreateSessionAsync(
        string actorAccountId,
        CreateClassSessionAPIViewModel request)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        (DateTime sessionDate, TimeSpan startTime, TimeSpan endTime) = ParseSessionTimes(
            request.Date, request.StartTime, request.EndTime);

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (sessionDate < today || (sessionDate == today && startTime <= currentTime))
        {
            throw new BusinessRuleException(
                "SESSION_IN_PAST",
                "Không thể lập buổi học về thời điểm đã qua.");
        }

        Class? currentClass = await _context.Classes
            .Include(c => c.Discipline)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        if (currentClass.Status == "ARCHIVED")
        {
            throw new BusinessRuleException("CLASS_ARCHIVED", "Lớp học đã lưu trữ không thể thêm buổi học.");
        }

        Coach? coach = await _context.Coaches.AsNoTracking()
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.AccountId == request.CoachId);

        if (coach is null)
        {
            throw new BusinessRuleException("COACH_NOT_FOUND", "Không tìm thấy huấn luyện viên.");
        }

        if (coach.Account.DeletedAt != null || coach.Account.Status != "Active" || coach.Account.IsLocked)
        {
            throw new BusinessRuleException("COACH_INACTIVE", "Huấn luyện viên đã ngừng hoạt động.");
        }

        Room? room = await _context.Rooms
            .FirstOrDefaultAsync(r => r.Id == request.RoomId);

        if (room is null)
        {
            throw new BusinessRuleException("ROOM_NOT_FOUND", "Không tìm thấy phòng tập.");
        }

        if (!room.IsActive)
        {
            throw new BusinessRuleException("ROOM_INACTIVE", "Phòng tập đang ngừng hoạt động.");
        }

        if (request.Capacity <= 0 || request.Capacity > currentClass.Capacity || request.Capacity > room.Capacity)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Sức chứa buổi không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    { "capacity", new[] { "Sức chứa buổi phải lớn hơn 0, không vượt quá sức chứa lớp (" + currentClass.Capacity + ") và phòng (" + room.Capacity + ")." } }
                });
        }

        await ScheduleConflict.ValidateAsync(_context, null, request.CoachId, request.RoomId, sessionDate, startTime, endTime);
        await ValidateCoachDisciplineAsync(request.CoachId, currentClass.DisciplineId);

        DateTime now = DateTime.UtcNow;
        ClassSession session = new ClassSession
        {
            ClassId = request.ClassId,
            CoachId = request.CoachId,
            RoomId = request.RoomId,
            SessionDate = sessionDate,
            StartTime = startTime,
            EndTime = endTime,
            Capacity = request.Capacity,
            BookedCount = 0,
            Status = "SCHEDULED",
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            CreatedBy = actorAccountId
        };

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.ClassSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "SESSION_CREATED",
                "ClassSession",
                session.Id.ToString(),
                "Lập lịch buổi học ID " + session.Id + " cho lớp " + currentClass.Name + " vào ngày " + sessionDate.ToString("yyyy-MM-dd") + ".");
            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            session.Class = currentClass;
            session.Room = room;

            Dictionary<string, string> coachNames = new Dictionary<string, string>
            {
                { coach.AccountId, coach.FullName }
            };

            return MapSession(session, coachNames);
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<ClassSessionViewDto> UpdateSessionAsync(
        string actorAccountId,
        int id,
        UpdateClassSessionAPIViewModel request)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        ClassSession? session = await _context.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c.Discipline)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
        {
            throw new BusinessRuleException("SESSION_NOT_FOUND", "Không tìm thấy buổi học.");
        }

        if (session.Status != "SCHEDULED")
        {
            throw new BusinessRuleException("SESSION_CANCELLED", "Buổi học đã bị hủy không thể chỉnh sửa.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (session.SessionDate < today || (session.SessionDate == today && session.StartTime <= currentTime))
        {
            throw new BusinessRuleException(
                "SESSION_ALREADY_STARTED",
                "Không thể chỉnh sửa buổi học đã diễn ra hoặc đang diễn ra.");
        }

        (DateTime sessionDate, TimeSpan startTime, TimeSpan endTime) = ParseSessionTimes(
            request.Date, request.StartTime, request.EndTime);

        if (sessionDate < today || (sessionDate == today && startTime <= currentTime))
        {
            throw new BusinessRuleException(
                "SESSION_IN_PAST",
                "Không thể chuyển buổi học về thời điểm đã qua.");
        }

        Class? currentClass = await _context.Classes
            .FirstOrDefaultAsync(c => c.Id == request.ClassId);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        if (currentClass.Status == "ARCHIVED")
        {
            throw new BusinessRuleException("CLASS_ARCHIVED", "Lớp học đã lưu trữ.");
        }

        if (session.CoachId != request.CoachId)
        {
            Coach? newCoach = await _context.Coaches.AsNoTracking()
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.AccountId == request.CoachId);

            if (newCoach is null)
            {
                throw new BusinessRuleException("COACH_NOT_FOUND", "Không tìm thấy huấn luyện viên.");
            }

            if (newCoach.Account.DeletedAt != null || newCoach.Account.Status != "Active" || newCoach.Account.IsLocked)
            {
                throw new BusinessRuleException("COACH_INACTIVE", "Huấn luyện viên đã ngừng hoạt động.");
            }
        }

        Room? room = await _context.Rooms
            .FirstOrDefaultAsync(r => r.Id == request.RoomId);

        if (room is null)
        {
            throw new BusinessRuleException("ROOM_NOT_FOUND", "Không tìm thấy phòng tập.");
        }

        if (session.RoomId != request.RoomId && !room.IsActive)
        {
            throw new BusinessRuleException("ROOM_INACTIVE", "Phòng tập đang ngừng hoạt động.");
        }

        if (request.Capacity <= 0 || request.Capacity > currentClass.Capacity ||
            request.Capacity > room.Capacity || request.Capacity < session.BookedCount)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Sức chứa buổi không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    { "capacity", new[] { "Sức chứa buổi phải lớn hơn 0, tối thiểu bằng số học viên đã đăng ký (" + session.BookedCount + "), không vượt quá sức chứa lớp (" + currentClass.Capacity + ") và phòng (" + room.Capacity + ")." } }
                });
        }

        await ScheduleConflict.ValidateAsync(_context, id, request.CoachId, request.RoomId, sessionDate, startTime, endTime);
        await ValidateCoachDisciplineAsync(request.CoachId, currentClass.DisciplineId);

        bool timeOrDateChanged = session.SessionDate != sessionDate ||
            session.StartTime != startTime ||
            session.EndTime != endTime;

        bool roomChanged = session.RoomId != request.RoomId;
        bool classChanged = session.ClassId != request.ClassId;
        List<ClassRegistration> bookedRegistrations = await _context.ClassRegistrations
            .Where(r => r.SessionId == id && r.Status == "BOOKED").ToListAsync();
        if (timeOrDateChanged || classChanged)
        {

            if (bookedRegistrations.Count > 0)
            {
                DateOnly targetDate = DateOnly.FromDateTime(sessionDate);
                foreach (ClassRegistration reg in bookedRegistrations)
                {
                    reg.MemberSubscriptionId = await ClassEligibility.ValidateAsync(_context,
                        reg.MemberId, targetDate, currentClass.DisciplineId);

                    bool memberConflict = await _context.ClassRegistrations.AsNoTracking()
                        .Include(r => r.Session)
                        .AnyAsync(r => r.Id != reg.Id &&
                            r.MemberId == reg.MemberId &&
                            r.Status == "BOOKED" &&
                            r.Session.SessionDate == sessionDate &&
                            r.Session.Status == "SCHEDULED" &&
                            r.Session.StartTime < endTime &&
                            r.Session.EndTime > startTime);

                    if (memberConflict)
                    {
                        throw new BusinessRuleException(
                            "MEMBER_SCHEDULE_CONFLICT",
                            "Một hoặc nhiều học viên đã có lịch học trùng với khung giờ mới.");
                    }
                }
            }
        }

        if (!timeOrDateChanged && !roomChanged && !classChanged && session.CoachId == request.CoachId &&
            session.Capacity == request.Capacity && session.Notes == (string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()))
            return MapSession(session, await LoadCoachNamesAsync(new[] { session.CoachId }));
        string oldCoachId = session.CoachId;
        bool coachChanged = session.CoachId != request.CoachId;

        session.Class = currentClass;
        session.Room = room;
        session.ClassId = request.ClassId;
        session.CoachId = request.CoachId;
        session.RoomId = request.RoomId;
        session.SessionDate = sessionDate;
        session.StartTime = startTime;
        session.EndTime = endTime;
        session.Capacity = request.Capacity;
        session.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        session.UpdatedAt = DateTime.UtcNow;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "SESSION_UPDATED",
                "ClassSession",
                session.Id.ToString(),
                "Cập nhật buổi học ID " + session.Id + ".");

            if (timeOrDateChanged || roomChanged || classChanged)
            {
                DateTime sessionStart = sessionDate.Date + startTime;
                foreach (ClassRegistration reg in bookedRegistrations)
                {
                    await _notificationService.SendNotificationAsync(
                        reg.MemberId,
                        "SESSION_RESCHEDULED",
                        "Buổi học của bạn đã được dời lịch",
                        "Buổi học lớp " + session.Class.Name + " được chuyển sang ngày " + sessionDate.ToString("yyyy-MM-dd") + " " + startTime.ToString(@"hh\:mm") + "–" + endTime.ToString(@"hh\:mm") + ".",
                        "ClassSession",
                        session.Id.ToString(),
                        sessionStart);
                }

                await _notificationService.SendNotificationAsync(
                    session.CoachId,
                    "SESSION_RESCHEDULED",
                    "Lịch dạy của bạn đã thay đổi",
                    "Buổi dạy lớp " + session.Class.Name + " được chuyển sang ngày " + sessionDate.ToString("yyyy-MM-dd") + " " + startTime.ToString(@"hh\:mm") + "–" + endTime.ToString(@"hh\:mm") + ".",
                    "ClassSession",
                    session.Id.ToString(),
                    sessionStart);
            }

            if (coachChanged)
            {
                DateTime sessionStart = sessionDate.Date + startTime;
                await _auditLogService.StageAuditLogAsync(
                    actorAccountId,
                    "SESSION_COACH_CHANGED",
                    "ClassSession",
                    session.Id.ToString(),
                    "Đổi huấn luyện viên buổi ID " + session.Id + " sang " + request.CoachId + ".");

                foreach (ClassRegistration reg in bookedRegistrations)
                {
                    await _notificationService.SendNotificationAsync(
                        reg.MemberId,
                        "SESSION_COACH_CHANGED",
                        "Thay đổi huấn luyện viên buổi học",
                        "Buổi học lớp " + session.Class.Name + " ngày " + sessionDate.ToString("yyyy-MM-dd") + " đã đổi huấn luyện viên.",
                        "ClassSession",
                        session.Id.ToString(),
                        sessionStart);
                }

                await _notificationService.SendNotificationAsync(
                    oldCoachId,
                    "SESSION_COACH_CHANGED",
                    "Bạn đã được phân công lại",
                    "Bạn không còn phụ trách buổi dạy lớp " + session.Class.Name + " ngày " + sessionDate.ToString("yyyy-MM-dd") + ".",
                    "ClassSession",
                    session.Id.ToString(),
                    sessionStart);

                await _notificationService.SendNotificationAsync(
                    request.CoachId,
                    "SESSION_COACH_CHANGED",
                    "Bạn có buổi dạy mới",
                    "Bạn được phân công phụ trách buổi dạy lớp " + session.Class.Name + " ngày " + sessionDate.ToString("yyyy-MM-dd") + ".",
                    "ClassSession",
                    session.Id.ToString(),
                    sessionStart);
            }

            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            Dictionary<string, string> coachNames = await LoadCoachNamesAsync(new[] { session.CoachId });
            return MapSession(session, coachNames);
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<ClassSessionViewDto> UpdateSessionCoachAsync(
        string actorAccountId,
        int id,
        string newCoachId)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        ClassSession? session = await _context.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c.Discipline)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
        {
            throw new BusinessRuleException("SESSION_NOT_FOUND", "Không tìm thấy buổi học.");
        }

        if (session.Status != "SCHEDULED")
        {
            throw new BusinessRuleException("SESSION_CANCELLED", "Buổi học đã bị hủy không thể đổi huấn luyện viên.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (session.SessionDate < today || (session.SessionDate == today && session.StartTime <= currentTime))
        {
            throw new BusinessRuleException(
                "SESSION_ALREADY_STARTED",
                "Không thể đổi huấn luyện viên sau khi buổi học đã bắt đầu.");
        }

        if (session.CoachId == newCoachId)
        {
            Dictionary<string, string> currentCoachNames = await LoadCoachNamesAsync(new[] { session.CoachId });
            return MapSession(session, currentCoachNames);
        }

        Coach? newCoach = await _context.Coaches.AsNoTracking()
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.AccountId == newCoachId);

        if (newCoach is null)
        {
            throw new BusinessRuleException("COACH_NOT_FOUND", "Không tìm thấy huấn luyện viên.");
        }

        if (newCoach.Account.DeletedAt != null || newCoach.Account.Status != "Active" || newCoach.Account.IsLocked)
        {
            throw new BusinessRuleException("COACH_INACTIVE", "Huấn luyện viên đã ngừng hoạt động.");
        }

        await ScheduleConflict.ValidateAsync(_context, id, newCoachId, session.RoomId, session.SessionDate, session.StartTime, session.EndTime);
        await ValidateCoachDisciplineAsync(newCoachId, session.Class.DisciplineId);

        if (session.CoachId == newCoachId)
            return MapSession(session, await LoadCoachNamesAsync(new[] { session.CoachId }));
        string oldCoachId = session.CoachId;
        session.CoachId = newCoachId;
        session.UpdatedAt = DateTime.UtcNow;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "SESSION_COACH_CHANGED",
                "ClassSession",
                session.Id.ToString(),
                "Đổi huấn luyện viên buổi ID " + session.Id + " từ " + oldCoachId + " sang " + newCoachId + ".");

            List<ClassRegistration> bookedRegistrations = await _context.ClassRegistrations.AsNoTracking()
                .Where(r => r.SessionId == id && r.Status == "BOOKED")
                .ToListAsync();

            DateTime sessionStart = session.SessionDate.Date + session.StartTime;
            foreach (ClassRegistration reg in bookedRegistrations)
            {
                await _notificationService.SendNotificationAsync(
                    reg.MemberId,
                    "SESSION_COACH_CHANGED",
                    "Thay đổi huấn luyện viên buổi học",
                    "Buổi học lớp " + session.Class.Name + " ngày " + session.SessionDate.ToString("yyyy-MM-dd") + " đã đổi huấn luyện viên.",
                    "ClassSession",
                    session.Id.ToString(),
                    sessionStart);
            }

            await _notificationService.SendNotificationAsync(
                oldCoachId,
                "SESSION_COACH_CHANGED",
                "Bạn đã được phân công lại",
                "Bạn không còn phụ trách buổi dạy lớp " + session.Class.Name + " ngày " + session.SessionDate.ToString("yyyy-MM-dd") + ".",
                "ClassSession",
                session.Id.ToString(),
                sessionStart);

            await _notificationService.SendNotificationAsync(
                newCoachId,
                "SESSION_COACH_CHANGED",
                "Bạn có buổi dạy mới",
                "Bạn được phân công phụ trách buổi dạy lớp " + session.Class.Name + " ngày " + session.SessionDate.ToString("yyyy-MM-dd") + ".",
                "ClassSession",
                session.Id.ToString(),
                sessionStart);

            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            Dictionary<string, string> coachNames = new Dictionary<string, string>
            {
                { newCoach.AccountId, newCoach.FullName }
            };

            return MapSession(session, coachNames);
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<PagedResult<ClassSessionViewDto>> GetCoachScheduleAsync(
        string coachAccountId,
        DateTime? from,
        DateTime? to,
        string? status,
        int page,
        int pageSize)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime defaultFrom = from ?? localNow.Date;
        DateTime defaultTo = to ?? localNow.Date.AddDays(7);

        IQueryable<ClassSession> query = _context.ClassSessions.AsNoTracking()
            .Include(session => session.Room)
            .Include(session => session.Class)
                .ThenInclude(c => c.Discipline)
            .Where(session => session.CoachId == coachAccountId &&
                session.SessionDate >= defaultFrom.Date &&
                session.SessionDate <= defaultTo.Date);

        if (!string.IsNullOrWhiteSpace(status))
        {
            string normalizedStatus = status.Trim().ToUpperInvariant();
            if (normalizedStatus == "CANCELLED")
            {
                normalizedStatus = "CANCELED";
            }
            query = query.Where(session => session.Status == normalizedStatus);
        }

        int totalItems = await query.CountAsync();

        List<ClassSession> sessions = await query
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartTime)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(new[] { coachAccountId });

        List<ClassSessionViewDto> items = sessions
            .Select(session => MapSession(session, coachNames))
            .ToList();

        return new PagedResult<ClassSessionViewDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<SessionCancelImpactViewModel> GetSessionCancelImpactAsync(int sessionId)
    {
        ClassSession? session = await _context.ClassSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
        {
            throw new BusinessRuleException("SESSION_NOT_FOUND", "Không tìm thấy buổi học.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        if (session.Status != "SCHEDULED" || session.SessionDate.Date + session.StartTime <= localNow)
            return new SessionCancelImpactViewModel { Scope = "SESSION", AffectedSessionCount = 0 };

        List<ClassRegistration> bookedRegistrations = await _context.ClassRegistrations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.Status == "BOOKED")
            .ToListAsync();

        int memberCount = bookedRegistrations
            .Select(r => r.MemberId)
            .Distinct()
            .Count();

        DateTime sessionStart = DateTime.SpecifyKind(session.SessionDate.Date + session.StartTime, DateTimeKind.Unspecified);
        string nextStartsAt = new DateTimeOffset(sessionStart, TimeSpan.FromHours(7)).ToString("yyyy-MM-ddTHH:mm:sszzz");

        return new SessionCancelImpactViewModel
        {
            Scope = "SESSION",
            AffectedSessionCount = 1,
            AffectedRegistrationCount = bookedRegistrations.Count,
            AffectedMemberCount = memberCount,
            NextSessionStartsAt = nextStartsAt
        };
    }

    public async Task<ClassSessionViewDto> CancelSessionAsync(
        string actorAccountId,
        int sessionId,
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

        ClassSession? session = await _context.ClassSessions
            .Include(s => s.Class)
                .ThenInclude(c => c.Discipline)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
        {
            throw new BusinessRuleException("SESSION_NOT_FOUND", "Không tìm thấy buổi học.");
        }

        if (session.Status != "SCHEDULED")
        {
            throw new BusinessRuleException("SESSION_CANCELLED", "Buổi học đã bị hủy trước đó.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (session.SessionDate < today || (session.SessionDate == today && session.StartTime <= currentTime))
        {
            throw new BusinessRuleException(
                "SESSION_ALREADY_STARTED",
                "Không thể hủy buổi học đã diễn ra hoặc đang diễn ra.");
        }

        List<ClassRegistration> bookedRegistrations = await _context.ClassRegistrations
            .Where(r => r.SessionId == sessionId && r.Status == "BOOKED")
            .ToListAsync();

        DateTime now = DateTime.UtcNow;
        session.Status = "CANCELED";
        session.CancelReason = normalizedReason;
        session.CanceledAt = now;
        session.CanceledBy = actorAccountId;
        session.BookedCount = 0;
        session.UpdatedAt = now;

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            foreach (ClassRegistration reg in bookedRegistrations)
            {
                reg.Status = "CANCELED";
                reg.CancelReason = normalizedReason;
                reg.CanceledAt = now;
                reg.CanceledBy = actorAccountId;

                await _auditLogService.StageAuditLogAsync(
                    actorAccountId,
                    "CLASS_REGISTRATION_CANCELED",
                    "ClassRegistration",
                    reg.Id.ToString(),
                    "Hủy đăng ký học viên do hủy buổi học.");

                DateTime sessionStart = session.SessionDate.Date + session.StartTime;
                await _notificationService.SendNotificationAsync(
                    reg.MemberId,
                    "SESSION_CANCELED",
                    "Buổi học bạn đã đăng ký bị hủy",
                    "Buổi học lớp " + session.Class.Name + " ngày " + session.SessionDate.ToString("yyyy-MM-dd") + " đã bị hủy. Lý do: " + normalizedReason,
                    "ClassSession",
                    session.Id.ToString(),
                    sessionStart);
            }

            DateTime coachSessionStart = session.SessionDate.Date + session.StartTime;
            await _notificationService.SendNotificationAsync(
                session.CoachId,
                "SESSION_CANCELED",
                "Buổi dạy của bạn đã bị hủy",
                "Buổi dạy lớp " + session.Class.Name + " ngày " + session.SessionDate.ToString("yyyy-MM-dd") + " đã bị hủy. Lý do: " + normalizedReason,
                "ClassSession",
                session.Id.ToString(),
                coachSessionStart);

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "SESSION_CANCELED",
                "ClassSession",
                session.Id.ToString(),
                "Hủy buổi học ID " + session.Id + " lớp " + session.Class.Name + ". Lý do: " + normalizedReason);

            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            Dictionary<string, string> coachNames = await LoadCoachNamesAsync(new[] { session.CoachId });
            return MapSession(session, coachNames);
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<PagedResult<ClassRosterMemberDto>> GetSessionRosterAsync(
        int sessionId,
        string callerAccountId,
        bool isManager,
        bool includeCanceled,
        int page,
        int pageSize,
        string? search = null)
    {
        ClassSession? session = await _context.ClassSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
        {
            throw new BusinessRuleException("SESSION_NOT_FOUND", "Không tìm thấy buổi học.");
        }

        if (!isManager && session.CoachId != callerAccountId)
        {
            throw new BusinessRuleException(
                "FORBIDDEN",
                "Bạn không được phân công phụ trách buổi học này.");
        }

        IQueryable<ClassRegistration> query = _context.ClassRegistrations.AsNoTracking()
            .Where(r => r.SessionId == sessionId);

        if (!includeCanceled)
        {
            query = query.Where(r => r.Status == "BOOKED");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim();
            query = query.Where(item => item.Member.MemberCode.Contains(keyword) ||
                (item.Member.FullName != null && item.Member.FullName.Contains(keyword)));
        }

        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        int totalItems = await query.CountAsync();

        List<ClassRegistration> registrations = await query
            .OrderBy(r => r.RegisteredAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        List<string> memberIds = registrations.Select(r => r.MemberId).Distinct().ToList();

        Dictionary<string, Member> members = await _context.Members.AsNoTracking()
            .Where(m => memberIds.Contains(m.AccountId))
            .ToDictionaryAsync(m => m.AccountId);

        List<ClassRosterMemberDto> items = registrations.Select(r =>
        {
            Member? member = members.TryGetValue(r.MemberId, out Member? m) ? m : null;
            return new ClassRosterMemberDto
            {
                Id = r.Id,
                SessionId = r.SessionId,
                MemberAccountId = r.MemberId,
                MemberName = member?.FullName ?? "N/A",
                MemberCode = member?.MemberCode ?? "N/A",
                Status = r.Status,
                RegisteredVia = r.RegisteredVia,
                RegisteredAt = r.RegisteredAt
            };
        }).ToList();

        return new PagedResult<ClassRosterMemberDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    private static (DateTime sessionDate, TimeSpan startTime, TimeSpan endTime) ParseSessionTimes(
        string dateStr,
        string startTimeStr,
        string endTimeStr)
    {
        if (!DateTime.TryParseExact(dateStr.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime sessionDate))
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Ngày diễn ra không hợp lệ (định dạng YYYY-MM-DD).",
                new Dictionary<string, string[]> { { "date", new[] { "Định dạng ngày là YYYY-MM-DD." } } });
        }

        if (!TimeSpan.TryParseExact(startTimeStr.Trim(), new[] { @"hh\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out TimeSpan startTime))
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Giờ bắt đầu không hợp lệ (định dạng HH:mm).",
                new Dictionary<string, string[]> { { "startTime", new[] { "Định dạng giờ là HH:mm." } } });
        }

        if (!TimeSpan.TryParseExact(endTimeStr.Trim(), new[] { @"hh\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out TimeSpan endTime))
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Giờ kết thúc không hợp lệ (định dạng HH:mm).",
                new Dictionary<string, string[]> { { "endTime", new[] { "Định dạng giờ là HH:mm." } } });
        }

        if (startTime < TimeSpan.Zero || endTime >= TimeSpan.FromDays(1) || startTime >= endTime)
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Giờ kết thúc phải sau giờ bắt đầu.",
                new Dictionary<string, string[]> { { "endTime", new[] { "Giờ kết thúc phải sau giờ bắt đầu." } } });
        }

        return (sessionDate.Date, startTime, endTime);
    }

    private async Task ValidateCoachDisciplineAsync(string coachId, int disciplineId)
    {
        bool hasConfiguredDisciplines = await _context.CoachDisciplines.AsNoTracking()
            .AnyAsync(cd => cd.CoachAccountId == coachId);

        if (!hasConfiguredDisciplines)
        {
            return;
        }

        bool teachesDiscipline = await _context.CoachDisciplines.AsNoTracking()
            .AnyAsync(cd => cd.CoachAccountId == coachId && cd.DisciplineId == disciplineId);

        if (!teachesDiscipline)
        {
            throw new BusinessRuleException(
                "COACH_DISCIPLINE_MISMATCH",
                "Huấn luyện viên không phụ trách bộ môn của lớp học này.",
                new Dictionary<string, string[]>
                {
                    { "coachId", new[] { "Huấn luyện viên không phụ trách bộ môn của lớp học này." } }
                });
        }
    }

    private async Task<Dictionary<string, string>> LoadCoachNamesAsync(IEnumerable<string> coachIds)
    {
        List<string> distinct = coachIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _context.Coaches.AsNoTracking()
            .Where(coach => distinct.Contains(coach.AccountId))
            .ToDictionaryAsync(coach => coach.AccountId, coach => coach.FullName);
    }

    public static ClassSessionViewDto MapSession(
        ClassSession session,
        Dictionary<string, string> coachNames)
    {
        string coachName = coachNames.TryGetValue(session.CoachId, out string? name)
            ? name
            : string.Empty;

        DateTime startDateTime = DateTime.SpecifyKind(session.SessionDate.Date + session.StartTime, DateTimeKind.Unspecified);
        DateTime endDateTime = DateTime.SpecifyKind(session.SessionDate.Date + session.EndTime, DateTimeKind.Unspecified);
        TimeSpan offset = TimeSpan.FromHours(7);

        return new ClassSessionViewDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            ClassName = session.Class?.Name ?? string.Empty,
            SportId = session.Class?.DisciplineId ?? 0,
            SportName = session.Class?.Discipline?.Name ?? string.Empty,
            CoachId = session.CoachId,
            CoachName = coachName,
            RoomId = session.RoomId,
            RoomName = session.Room?.Name ?? string.Empty,
            Date = session.SessionDate.ToString("yyyy-MM-dd"),
            StartTime = session.StartTime.ToString(@"hh\:mm"),
            EndTime = session.EndTime.ToString(@"hh\:mm"),
            StartsAt = new DateTimeOffset(startDateTime, offset).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            EndsAt = new DateTimeOffset(endDateTime, offset).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Capacity = session.Capacity,
            BookedCount = session.BookedCount,
            Status = session.Status,
            CancelReason = session.CancelReason,
            CanceledAt = session.CanceledAt,
            UpdatedAt = session.UpdatedAt,
            Notes = session.Notes
        };
    }
}
