using System.Globalization;
using APIViewModel;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Services.ScheduleQueryService;

public class ScheduleQueryService : IScheduleQueryService
{
    private readonly SportsCenterManagementContext _context;

    public ScheduleQueryService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ClassSessionViewDto>> GetCenterScheduleAsync(
        DateTime? from,
        DateTime? to,
        int? sportId,
        string? coachId,
        int? roomId,
        string? status,
        int page,
        int pageSize)
    {
        IQueryable<ClassSession> query = BuildSessionQuery(from, to, coachId, roomId, status);
        return await BuildSessionPageAsync(query, page, pageSize);
    }

    public async Task<PagedResult<ClassSessionViewDto>> GetAvailableSessionsAsync(
        DateTime? from,
        DateTime? to,
        int? sportId,
        string? coachId,
        int page,
        int pageSize)
    {
        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;
        IQueryable<ClassSession> query = BuildSessionQuery(from, to, coachId, null, null)
            .Where(session => session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)));

        return await BuildSessionPageAsync(query, page, pageSize);
    }

    public async Task<List<MemberBookedSessionDto>> GetMyClassScheduleAsync(
        string memberAccountId,
        DateTime? from,
        DateTime? to)
    {
        IQueryable<ClassRegistration> query = _context.ClassRegistrations
            .AsNoTracking()
            .Include(registration => registration.Session)
            .ThenInclude(session => session.Room)
            .Where(registration => registration.MemberId == memberAccountId);

        if (from.HasValue)
        {
            query = query.Where(registration => registration.Session.SessionDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(registration => registration.Session.SessionDate <= to.Value.Date);
        }

        List<ClassRegistration> registrations = await query
            .OrderBy(registration => registration.Session.SessionDate)
            .ThenBy(registration => registration.Session.StartTime)
            .ToListAsync();
        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(
            registrations.Select(registration => registration.Session.CoachId));

        List<MemberBookedSessionDto> result = new List<MemberBookedSessionDto>();
        foreach (ClassRegistration registration in registrations)
        {
            ClassSessionViewDto session = MapSession(registration.Session, coachNames);
            result.Add(new MemberBookedSessionDto
            {
                Id = session.Id,
                ClassId = session.ClassId,
                ClassName = session.ClassName,
                SportId = session.SportId,
                SportName = session.SportName,
                CoachId = session.CoachId,
                CoachName = session.CoachName,
                RoomId = session.RoomId,
                RoomName = session.RoomName,
                Date = session.Date,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                StartsAt = session.StartsAt,
                EndsAt = session.EndsAt,
                Capacity = session.Capacity,
                BookedCount = session.BookedCount,
                Status = session.Status,
                Notes = session.Notes,
                RegistrationId = registration.Id,
                RegistrationStatus = registration.Status,
                RegisteredAt = registration.RegisteredAt
            });
        }

        return result;
    }

    public async Task<PagedResult<ClassSessionViewDto>> GetCoachTeachingScheduleAsync(
        string coachAccountId,
        DateTime? from,
        DateTime? to,
        string? status,
        int page,
        int pageSize)
    {
        IQueryable<ClassSession> query = BuildSessionQuery(from, to, coachAccountId, null, status);
        return await BuildSessionPageAsync(query, page, pageSize);
    }

    public async Task<PagedResult<ClassRosterMemberDto>> GetSessionRosterAsync(
        int sessionId,
        string callerAccountId,
        bool isManager,
        bool includeCanceled,
        int page,
        int pageSize)
    {
        ClassSession? session = await _context.ClassSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId);
        if (session is null)
        {
            throw new BusinessRuleException(
                "SESSION_NOT_FOUND",
                "Không tìm thấy buổi học.");
        }

        if (!isManager && session.CoachId != callerAccountId)
        {
            throw new BusinessRuleException(
                "FORBIDDEN",
                "Bạn không được phân công phụ trách buổi học này.");
        }

        IQueryable<ClassRegistration> query = _context.ClassRegistrations
            .AsNoTracking()
            .Where(registration => registration.SessionId == sessionId);
        if (!includeCanceled)
        {
            query = query.Where(registration => registration.Status == "BOOKED");
        }

        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        int totalItems = await query.CountAsync();
        List<ClassRegistration> registrations = await query
            .OrderBy(registration => registration.RegisteredAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();
        Dictionary<string, Member> members = await LoadMembersAsync(
            registrations.Select(registration => registration.MemberId));

        List<ClassRosterMemberDto> items = registrations.Select(registration =>
        {
            Member? member = members.TryGetValue(registration.MemberId, out Member? value) ? value : null;
            return new ClassRosterMemberDto
            {
                Id = registration.Id,
                SessionId = registration.SessionId,
                MemberAccountId = registration.MemberId,
                MemberName = member?.FullName ?? "N/A",
                MemberCode = member?.MemberCode ?? "N/A",
                Status = registration.Status,
                RegisteredVia = registration.RegisteredVia,
                RegisteredAt = registration.RegisteredAt
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

    private IQueryable<ClassSession> BuildSessionQuery(
        DateTime? from,
        DateTime? to,
        string? coachId,
        int? roomId,
        string? status)
    {
        IQueryable<ClassSession> query = _context.ClassSessions
            .AsNoTracking()
            .Include(session => session.Room);

        if (from.HasValue)
        {
            query = query.Where(session => session.SessionDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(session => session.SessionDate <= to.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(coachId))
        {
            query = query.Where(session => session.CoachId == coachId);
        }

        if (roomId.HasValue)
        {
            query = query.Where(session => session.RoomId == roomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(session => session.Status == status);
        }

        return query;
    }

    private async Task<PagedResult<ClassSessionViewDto>> BuildSessionPageAsync(
        IQueryable<ClassSession> query,
        int page,
        int pageSize)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);
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

        return new PagedResult<ClassSessionViewDto>
        {
            Items = sessions.Select(session => MapSession(session, coachNames)).ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    private async Task<Dictionary<string, string>> LoadCoachNamesAsync(IEnumerable<string> coachIds)
    {
        List<string> ids = coachIds.Distinct(StringComparer.Ordinal).ToList();
        return await _context.Coaches
            .AsNoTracking()
            .Where(coach => ids.Contains(coach.AccountId))
            .ToDictionaryAsync(coach => coach.AccountId, coach => coach.FullName);
    }

    private async Task<Dictionary<string, Member>> LoadMembersAsync(IEnumerable<string> memberIds)
    {
        List<string> ids = memberIds.Distinct(StringComparer.Ordinal).ToList();
        return await _context.Members
            .AsNoTracking()
            .Where(member => ids.Contains(member.AccountId))
            .ToDictionaryAsync(member => member.AccountId);
    }

    private static ClassSessionViewDto MapSession(
        ClassSession session,
        IReadOnlyDictionary<string, string> coachNames)
    {
        string date = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string startTime = session.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        string endTime = session.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        coachNames.TryGetValue(session.CoachId, out string? coachName);

        return new ClassSessionViewDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            CoachId = session.CoachId,
            CoachName = coachName ?? string.Empty,
            RoomId = session.RoomId,
            RoomName = session.Room?.Name ?? string.Empty,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            StartsAt = $"{date}T{startTime}:00+07:00",
            EndsAt = $"{date}T{endTime}:00+07:00",
            Capacity = session.Capacity,
            BookedCount = session.BookedCount,
            Status = session.Status,
            Notes = session.Notes
        };
    }
}
