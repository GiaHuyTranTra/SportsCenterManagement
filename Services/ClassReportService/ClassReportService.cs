using APIViewModel.Report;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.ScheduleQueryService;

namespace Services.ClassReportService;

public class ClassReportService : IClassReportService
{
    private readonly SportsCenterManagementContext _context;
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _clock;

    public ClassReportService(SportsCenterManagementContext context,
        IConfiguration configuration, TimeProvider? clock = null)
    {
        _context = context;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration["BusinessSettings:TimeZoneId"]
            ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured."));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ClassReportAPIViewModel> GetClassReportAsync(DateOnly? from, DateOnly? to,
        int? classId, int? sportId, string? registrationStatus, int page, int pageSize)
    {
        DateOnly end = to ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _timeZone).DateTime);
        DateOnly start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (end == DateOnly.MaxValue || start > end || end.DayNumber - start.DayNumber > 365)
            throw new BusinessRuleException("INVALID_REPORT_DATE_RANGE", "Choose an ordered range of at most 366 days.");
        string status = string.IsNullOrWhiteSpace(registrationStatus) ? "BOOKED" : registrationStatus.Trim().ToUpperInvariant();
        if ((status != "BOOKED" && status != "CANCELED" && status != "ALL") ||
            classId <= 0 || sportId <= 0 || page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize)
            throw new BusinessRuleException("INVALID_CLASS_REPORT_FILTER", "Invalid class, sport, registration status or pagination.");

        DateTime startDate = start.ToDateTime(TimeOnly.MinValue);
        DateTime endExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        IQueryable<ClassSession> sessions = _context.ClassSessions.AsNoTracking().Where(session =>
            session.SessionDate >= startDate && session.SessionDate < endExclusive &&
            (!classId.HasValue || session.ClassId == classId.Value) &&
            (!sportId.HasValue || session.Class.DisciplineId == sportId.Value));
        IQueryable<ClassRegistration> registrations = _context.ClassRegistrations.AsNoTracking().Where(registration =>
            sessions.Any(session => session.Id == registration.SessionId) &&
            (status == "ALL" || registration.Status == status) &&
            (registration.Status == "CANCELED" ||
             (registration.Status == "BOOKED" && registration.Session.Status != "CANCELED")));
        IQueryable<Class> classes = _context.Classes.AsNoTracking().Where(currentClass =>
            sessions.Any(session => session.ClassId == currentClass.Id));
        IQueryable<ClassReportRowAPIViewModel> rows = classes.Select(currentClass => new ClassReportRowAPIViewModel
        {
            ClassId = currentClass.Id, ClassName = currentClass.Name,
            SportId = currentClass.DisciplineId, SportName = currentClass.Discipline.Name,
            SessionCount = sessions.Count(session => session.ClassId == currentClass.Id),
            CanceledSessionCount = sessions.Count(session => session.ClassId == currentClass.Id && session.Status == "CANCELED"),
            RegistrationCount = registrations.Count(registration => registration.Session.ClassId == currentClass.Id),
            UniqueMemberCount = registrations.Where(registration => registration.Session.ClassId == currentClass.Id)
                .Select(registration => registration.MemberId).Distinct().Count()
        });

        return new ClassReportAPIViewModel
        {
            From = start, To = end, TimeZoneId = _timeZone.Id, ClassId = classId, SportId = sportId,
            RegistrationStatus = status, Page = page, PageSize = pageSize,
            TotalClasses = await classes.CountAsync(), TotalSessions = await sessions.CountAsync(),
            CanceledSessions = await sessions.CountAsync(session => session.Status == "CANCELED"),
            TotalRegistrations = await registrations.CountAsync(),
            UniqueMembers = await registrations.Select(registration => registration.MemberId).Distinct().CountAsync(),
            Items = await rows.OrderBy(row => row.ClassName).ThenBy(row => row.ClassId)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync()
        };
    }
}
