using Services.ClassScheduling;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassRegistration;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.AuditLogService;
using Services.ClassSessionService;
using Services.NotificationService;
using Services.ScheduleQueryService;

namespace Services.ClassRegistrationService;

public class ClassRegistrationService : IClassRegistrationService
{
    private readonly SportsCenterManagementContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;

    public ClassRegistrationService(
        SportsCenterManagementContext context,
        IAuditLogService auditLogService,
        INotificationService notificationService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<PagedResult<ClassRegistrationViewModel>> GetMyRegistrationsAsync(
        string memberAccountId,
        bool includeCanceled, int page = 1, int pageSize = 20,
        DateTime? from = null, DateTime? to = null)
    {
        IQueryable<ClassRegistration> query = _context.ClassRegistrations.AsNoTracking()
            .Include(r => r.Session)
                .ThenInclude(s => s.Class)
                    .ThenInclude(c => c.Discipline)
            .Include(r => r.Session)
                .ThenInclude(s => s.Room)
            .Include(r => r.Member)
            .Include(r => r.RegisteredByAccount)
            .Include(r => r.CanceledByAccount)
            .Where(r => r.MemberId == memberAccountId);

        if (!includeCanceled)
        {
            query = query.Where(r => r.Status == "BOOKED");
        }

        if (from.HasValue) query = query.Where(item => item.Session.SessionDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(item => item.Session.SessionDate <= to.Value.Date);
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        int totalItems = await query.CountAsync();
        List<ClassRegistration> registrations = await query
            .OrderByDescending(r => r.RegisteredAt).ThenByDescending(r => r.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .ToListAsync();

        List<string> coachIds = registrations.Select(r => r.Session.CoachId).Distinct().ToList();
        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(coachIds);

        Dictionary<string, string> actorNames = await LoadActorNamesAsync(registrations);
        List<ClassRegistrationViewModel> results = new List<ClassRegistrationViewModel>();
        foreach (ClassRegistration reg in registrations)
        {
            results.Add(MapRegistration(reg, coachNames, actorNames));
        }

        return new PagedResult<ClassRegistrationViewModel>
        {
            Items = results, Page = normalizedPage, PageSize = normalizedPageSize, TotalItems = totalItems
        };
    }

    public async Task<PagedResult<ClassRegistrationViewModel>> GetRegistrationsForStaffAsync(
        string? memberAccountId,
        bool includeCanceled,
        int page,
        int pageSize)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<ClassRegistration> query = _context.ClassRegistrations.AsNoTracking()
            .Include(r => r.Session)
                .ThenInclude(s => s.Class)
                    .ThenInclude(c => c.Discipline)
            .Include(r => r.Session)
                .ThenInclude(s => s.Room)
            .Include(r => r.Member)
            .Include(r => r.RegisteredByAccount)
            .Include(r => r.CanceledByAccount);

        if (!string.IsNullOrWhiteSpace(memberAccountId))
        {
            query = query.Where(r => r.MemberId == memberAccountId.Trim());
        }

        if (!includeCanceled)
        {
            query = query.Where(r => r.Status == "BOOKED");
        }

        int totalItems = await query.CountAsync();

        List<ClassRegistration> registrations = await query
            .OrderByDescending(r => r.RegisteredAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        List<string> coachIds = registrations.Select(r => r.Session.CoachId).Distinct().ToList();
        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(coachIds);

        Dictionary<string, string> actorNames = await LoadActorNamesAsync(registrations);
        List<ClassRegistrationViewModel> items = new List<ClassRegistrationViewModel>();
        foreach (ClassRegistration reg in registrations)
        {
            items.Add(MapRegistration(reg, coachNames, actorNames));
        }

        return new PagedResult<ClassRegistrationViewModel>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<ClassRegistrationViewModel> RegisterAsync(
        string memberAccountId,
        int sessionId,
        string registeredVia,
        string registeredByAccountId)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = registeredByAccountId;
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
            throw new BusinessRuleException("SESSION_CANCELLED", "Buổi học đã bị hủy.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (session.SessionDate < today || (session.SessionDate == today && session.StartTime <= currentTime))
        {
            throw new BusinessRuleException("SESSION_ALREADY_STARTED", "Không thể đăng ký buổi học đã bắt đầu hoặc đã qua.");
        }

        if (session.Class.Status != "OPEN")
        {
            throw new BusinessRuleException("CLASS_NOT_OPEN", "Lớp học chưa mở đăng ký.");
        }

        Member? member = await _context.Members
            .Include(m => m.Account)
            .FirstOrDefaultAsync(m => m.AccountId == memberAccountId);

        if (member is null)
        {
            throw new BusinessRuleException("ACCOUNT_INACTIVE", "Không tìm thấy thông tin thành viên.");
        }

        if (member.Account.DeletedAt != null || member.Account.Status != "Active" || member.Account.IsLocked)
        {
            throw new BusinessRuleException("ACCOUNT_INACTIVE", "Tài khoản thành viên đã ngừng hoạt động hoặc bị khóa.");
        }

        int subscriptionId = await ClassEligibility.ValidateAsync(_context, memberAccountId,
            DateOnly.FromDateTime(session.SessionDate), session.Class.DisciplineId);

        bool alreadyBooked = await _context.ClassRegistrations.AsNoTracking()
            .AnyAsync(r => r.SessionId == sessionId && r.MemberId == memberAccountId && r.Status == "BOOKED");

        if (alreadyBooked)
        {
            throw new BusinessRuleException("DUPLICATE_REGISTRATION", "Thành viên đã có lượt đăng ký đang giữ chỗ cho buổi học này.");
        }

        bool memberConflict = await _context.ClassRegistrations.AsNoTracking()
            .Include(r => r.Session)
            .AnyAsync(r => r.MemberId == memberAccountId &&
                r.Status == "BOOKED" &&
                r.Session.SessionDate == session.SessionDate &&
                r.Session.Status == "SCHEDULED" &&
                r.Session.StartTime < session.EndTime &&
                r.Session.EndTime > session.StartTime);

        if (memberConflict)
        {
            throw new BusinessRuleException(
                "MEMBER_SCHEDULE_CONFLICT",
                "Thành viên đã có lịch học khác trùng với khung giờ này.");
        }

        if (session.BookedCount >= session.Capacity)
        {
            throw new BusinessRuleException("CLASS_FULL", "Buổi học đã đủ số lượng học viên.");
        }

        DateTime now = DateTime.UtcNow;
        ClassRegistration registration = new ClassRegistration
        {
            MemberSubscriptionId = subscriptionId,
            SessionId = sessionId,
            MemberId = memberAccountId,
            Status = "BOOKED",
            RegisteredAt = now,
            RegisteredVia = registeredVia,
            RegisteredBy = registeredByAccountId
        };

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            int rowsUpdated = 0;
            if (_context.Database.IsRelational())
            {
                rowsUpdated = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE [dbo].[ClassSession] SET [BookedCount] = [BookedCount] + 1 WHERE [Id] = {0} AND [BookedCount] < [Capacity] AND [Status] = 'SCHEDULED'",
                    sessionId);
            }
            else
            {
                if (session.BookedCount < session.Capacity)
                {
                    session.BookedCount++;
                    rowsUpdated = 1;
                }
            }

            if (rowsUpdated == 0)
            {
                throw new BusinessRuleException("CLASS_FULL", "Buổi học đã đủ số lượng học viên.");
            }

            await _context.ClassRegistrations.AddAsync(registration);
            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                registeredByAccountId,
                "CLASS_REGISTERED",
                "ClassRegistration",
                registration.Id.ToString(),
                "Đăng ký học viên " + member.FullName + " vào buổi học ID " + sessionId + ".");

            DateTime sessionStart = session.SessionDate.Date + session.StartTime;
            await _notificationService.SendNotificationAsync(
                memberAccountId,
                "CLASS_REGISTERED",
                "Đăng ký buổi học thành công",
                "Bạn đã đăng ký thành công lớp " + session.Class.Name + " vào ngày " + session.SessionDate.ToString("yyyy-MM-dd") + " " + session.StartTime.ToString(@"hh\:mm") + "–" + session.EndTime.ToString(@"hh\:mm") + ".",
                "ClassSession",
                session.Id.ToString(),
                sessionStart);

            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            if (_context.Database.IsRelational()) await _context.Entry(session).ReloadAsync();
            registration.Session = session;
            registration.Member = member;

            Dictionary<string, string> coachNames = await LoadCoachNamesAsync(new[] { session.CoachId });
            return MapRegistration(registration, coachNames, await LoadActorNamesAsync(new[] { registration }));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            throw new BusinessRuleException("DUPLICATE_REGISTRATION", "Thành viên đã có lượt đăng ký đang giữ chỗ cho buổi học này.");
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    public async Task<ClassRegistrationViewModel> CancelRegistrationAsync(
        int registrationId,
        string? reason,
        string actorAccountId,
        bool isStaff)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;
        ClassRegistration? registration = await _context.ClassRegistrations
            .Include(r => r.Session)
                .ThenInclude(s => s.Class)
                    .ThenInclude(c => c.Discipline)
            .Include(r => r.Session)
                .ThenInclude(s => s.Room)
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (registration is null)
        {
            throw new BusinessRuleException("REGISTRATION_NOT_FOUND", "Không tìm thấy thông tin đăng ký.");
        }

        if (!isStaff && registration.MemberId != actorAccountId)
        {
            throw new BusinessRuleException("FORBIDDEN", "Bạn không có quyền thao tác trên đăng ký này.", statusCode: 403);
        }

        if (registration.Status == "CANCELED")
        {
            throw new BusinessRuleException("REGISTRATION_ALREADY_CANCELED", "Đăng ký này đã bị hủy trước đó.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        if (registration.Session.SessionDate < today ||
            (registration.Session.SessionDate == today && registration.Session.StartTime <= currentTime))
        {
            throw new BusinessRuleException(
                "SESSION_ALREADY_STARTED",
                "Không thể hủy đăng ký khi buổi học đã bắt đầu hoặc đã qua.");
        }

        DateTime sessionStart = registration.Session.SessionDate.Date + registration.Session.StartTime;
        if (!isStaff && sessionStart - localNow < TimeSpan.FromHours(7))
        {
            throw new BusinessRuleException(
                "REGISTRATION_CANCELLATION_TOO_LATE",
                "Members must cancel at least seven hours before the session starts.");
        }

        string? normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (isStaff)
        {
            if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length < 3 || normalizedReason.Length > 500)
            {
                throw new BusinessRuleException(
                    "VALIDATION_ERROR",
                    "Lý do hủy bắt buộc đối với nhân viên (từ 3 đến 500 ký tự).",
                    new Dictionary<string, string[]> { { "reason", new[] { "Lý do hủy từ 3 đến 500 ký tự." } } });
            }
        }

        DateTime now = DateTime.UtcNow;
        IDbContextTransaction? transaction = operation.Transaction;
        try
        {

            int rowsUpdated = 0;
            if (_context.Database.IsRelational())
            {
                object?[] sqlParams = new object?[]
                {
                    now,
                    actorAccountId,
                    normalizedReason,
                    registrationId
                };
                rowsUpdated = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE [dbo].[ClassRegistration] SET [Status] = 'CANCELED', [CanceledAt] = {0}, [CanceledBy] = {1}, [CancelReason] = {2} WHERE [Id] = {3} AND [Status] = 'BOOKED'",
                    sqlParams);
            }
            else
            {
                if (registration.Status == "BOOKED")
                {
                    registration.Status = "CANCELED";
                    registration.CanceledAt = now;
                    registration.CanceledBy = actorAccountId;
                    registration.CancelReason = normalizedReason;
                    rowsUpdated = 1;
                }
            }

            if (rowsUpdated == 0)
            {
                throw new BusinessRuleException("REGISTRATION_ALREADY_CANCELED", "Đăng ký này đã bị hủy trước đó.");
            }

            if (_context.Database.IsRelational()) await _context.Entry(registration).ReloadAsync();

            if (_context.Database.IsRelational())
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE [dbo].[ClassSession] SET [BookedCount] = CASE WHEN [BookedCount] > 0 THEN [BookedCount] - 1 ELSE 0 END WHERE [Id] = {0}",
                    registration.SessionId);
            }
            else
            {
                if (registration.Session.BookedCount > 0)
                {
                    registration.Session.BookedCount--;
                }
            }

            registration.Status = "CANCELED";
            registration.CanceledAt = now;
            registration.CanceledBy = actorAccountId;
            registration.CancelReason = normalizedReason;

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CLASS_REGISTRATION_CANCELED",
                "ClassRegistration",
                registration.Id.ToString(),
                "Hủy đăng ký học viên " + registration.Member.FullName + " cho buổi học ID " + registration.SessionId + ".");

            await _context.SaveChangesAsync();

            await operation.CommitAsync();

            if (_context.Database.IsRelational()) await _context.Entry(registration.Session).ReloadAsync();
            Dictionary<string, string> coachNames = await LoadCoachNamesAsync(new[] { registration.Session.CoachId });
            return MapRegistration(registration, coachNames, await LoadActorNamesAsync(new[] { registration }));
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Đăng ký cả lớp: giữ chỗ cho mọi buổi còn ở tương lai của lớp trong một
    /// giao dịch. Thành viên chọn lớp chứ không chọn từng buổi, nên nếu có một
    /// buổi không đủ chỗ hoặc trùng lịch thì cả lượt đăng ký bị từ chối, kèm
    /// ngày cụ thể để thành viên biết vì sao.
    /// </summary>
    public async Task<ClassEnrollmentViewModel> RegisterClassAsync(
        string memberAccountId,
        int classId,
        string registeredVia,
        string registeredByAccountId)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = registeredByAccountId;

        Class? currentClass = await _context.Classes
            .Include(item => item.Discipline)
            .Include(item => item.ClassSessions)
                .ThenInclude(session => session.Room)
            .FirstOrDefaultAsync(item => item.Id == classId);

        if (currentClass is null)
        {
            throw new BusinessRuleException("CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        if (currentClass.Status != "OPEN")
        {
            throw new BusinessRuleException("CLASS_NOT_OPEN", "Lớp học chưa mở đăng ký.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        List<ClassSession> upcoming = currentClass.ClassSessions
            .Where(session => session.Status == "SCHEDULED" &&
                (session.SessionDate > today ||
                 (session.SessionDate == today && session.StartTime > currentTime)))
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartTime)
            .ToList();

        if (upcoming.Count == 0)
        {
            throw new BusinessRuleException(
                "CLASS_HAS_NO_VALID_SESSION",
                "Lớp học không còn buổi nào ở tương lai để đăng ký.");
        }

        Member member = await LoadActiveMemberAsync(memberAccountId);

        bool alreadyBooked = await _context.ClassRegistrations.AsNoTracking()
            .AnyAsync(item => item.MemberId == memberAccountId &&
                item.Status == "BOOKED" &&
                item.Session.ClassId == classId);

        if (alreadyBooked)
        {
            throw new BusinessRuleException(
                "DUPLICATE_REGISTRATION",
                "Thành viên đã đăng ký lớp học này.");
        }

        // Gói tập phải còn hiệu lực cho mọi buổi của lớp: đăng ký cả lớp mà nửa
        // sau không dùng được thì lượt đăng ký không có nghĩa.
        int subscriptionId = 0;
        foreach (ClassSession session in upcoming)
        {
            try
            {
                subscriptionId = await ClassEligibility.ValidateAsync(
                    _context,
                    memberAccountId,
                    DateOnly.FromDateTime(session.SessionDate),
                    currentClass.DisciplineId);
            }
            catch (BusinessRuleException caught)
            {
                throw new BusinessRuleException(
                    caught.Code,
                    caught.Message + " (buổi ngày " + session.SessionDate.ToString("yyyy-MM-dd") + ")");
            }
        }

        ClassSession? full = upcoming.FirstOrDefault(session => session.BookedCount >= session.Capacity);
        if (full is not null)
        {
            throw new BusinessRuleException(
                "CLASS_FULL",
                "Buổi ngày " + full.SessionDate.ToString("yyyy-MM-dd") + " đã đủ số lượng học viên nên không thể đăng ký cả lớp.");
        }

        DateTime firstDate = upcoming[0].SessionDate;
        DateTime lastDate = upcoming[upcoming.Count - 1].SessionDate;

        var ownedSlots = await _context.ClassRegistrations.AsNoTracking()
            .Where(item => item.MemberId == memberAccountId &&
                item.Status == "BOOKED" &&
                item.Session.Status == "SCHEDULED" &&
                item.Session.SessionDate >= firstDate &&
                item.Session.SessionDate <= lastDate)
            .Select(item => new
            {
                item.Session.SessionDate,
                item.Session.StartTime,
                item.Session.EndTime
            })
            .ToListAsync();

        ClassSession? clash = upcoming.FirstOrDefault(session => ownedSlots.Any(slot =>
            slot.SessionDate == session.SessionDate &&
            slot.StartTime < session.EndTime &&
            slot.EndTime > session.StartTime));

        if (clash is not null)
        {
            throw new BusinessRuleException(
                "MEMBER_SCHEDULE_CONFLICT",
                "Thành viên đã có lịch học khác trùng khung giờ vào ngày " + clash.SessionDate.ToString("yyyy-MM-dd") + ".");
        }

        DateTime now = DateTime.UtcNow;
        List<ClassRegistration> registrations = upcoming
            .Select(session => new ClassRegistration
            {
                MemberSubscriptionId = subscriptionId,
                SessionId = session.Id,
                MemberId = memberAccountId,
                Status = "BOOKED",
                RegisteredAt = now,
                RegisteredVia = registeredVia,
                RegisteredBy = registeredByAccountId
            })
            .ToList();

        IDbContextTransaction? transaction = operation.Transaction;
        try
        {
            foreach (ClassSession session in upcoming)
            {
                int rowsUpdated;
                if (_context.Database.IsRelational())
                {
                    rowsUpdated = await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE [dbo].[ClassSession] SET [BookedCount] = [BookedCount] + 1 WHERE [Id] = {0} AND [BookedCount] < [Capacity] AND [Status] = 'SCHEDULED'",
                        session.Id);
                }
                else
                {
                    rowsUpdated = session.BookedCount < session.Capacity ? 1 : 0;
                    if (rowsUpdated == 1) session.BookedCount++;
                }

                if (rowsUpdated == 0)
                {
                    throw new BusinessRuleException(
                        "CLASS_FULL",
                        "Buổi ngày " + session.SessionDate.ToString("yyyy-MM-dd") + " đã đủ số lượng học viên nên không thể đăng ký cả lớp.");
                }
            }

            await _context.ClassRegistrations.AddRangeAsync(registrations);
            await _context.SaveChangesAsync();

            await _auditLogService.StageAuditLogAsync(
                registeredByAccountId,
                "CLASS_REGISTERED",
                "Class",
                classId.ToString(),
                "Đăng ký học viên " + member.FullName + " vào lớp " + currentClass.Name + " (" + registrations.Count + " buổi).");

            // Thông báo phải gắn vào một buổi học: ClassChangeEvent khóa theo
            // SessionId. Lấy buổi gần nhất của lớp làm mốc, còn nội dung nói về
            // cả lớp — một lượt đăng ký lớp chỉ sinh một thông báo.
            ClassSession firstSession = upcoming[0];
            DateTime firstStart = firstSession.SessionDate.Date + firstSession.StartTime;
            await _notificationService.SendNotificationAsync(
                memberAccountId,
                "CLASS_REGISTERED",
                "Đăng ký lớp học thành công",
                "Bạn đã đăng ký lớp " + currentClass.Name + " với " + registrations.Count + " buổi, từ " +
                    firstDate.ToString("yyyy-MM-dd") + " đến " + lastDate.ToString("yyyy-MM-dd") +
                    ". Buổi đầu tiên bắt đầu " + firstSession.StartTime.ToString(@"hh\:mm") + ".",
                "ClassSession",
                firstSession.Id.ToString(),
                firstStart);

            await _context.SaveChangesAsync();

            await operation.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            throw new BusinessRuleException("DUPLICATE_REGISTRATION", "Thành viên đã đăng ký lớp học này.");
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }

        return await BuildEnrollmentAsync(memberAccountId, classId);
    }

    /// <summary>
    /// Hủy đăng ký cả lớp: nhả chỗ ở mọi buổi chưa bắt đầu. Buổi đã diễn ra giữ
    /// nguyên để không mất lịch sử tham gia.
    /// </summary>
    public async Task<ClassEnrollmentViewModel> CancelClassRegistrationAsync(
        string memberAccountId,
        int classId,
        string? reason,
        string actorAccountId,
        bool isStaff)
    {
        await using Flow2WriteScope operation = await Flow2WriteScope.BeginAsync(_context);
        operation.ActorAccountId = actorAccountId;

        if (!isStaff && memberAccountId != actorAccountId)
        {
            throw new BusinessRuleException("FORBIDDEN", "Bạn không có quyền thao tác trên đăng ký này.", statusCode: 403);
        }

        string? normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (isStaff && (normalizedReason is null || normalizedReason.Length < 3 || normalizedReason.Length > 500))
        {
            throw new BusinessRuleException(
                "VALIDATION_ERROR",
                "Lý do hủy bắt buộc đối với nhân viên (từ 3 đến 500 ký tự).",
                new Dictionary<string, string[]> { { "reason", new[] { "Lý do hủy từ 3 đến 500 ký tự." } } });
        }

        List<ClassRegistration> registrations = await _context.ClassRegistrations
            .Include(item => item.Session)
            .Include(item => item.Member)
            .Where(item => item.MemberId == memberAccountId &&
                item.Status == "BOOKED" &&
                item.Session.ClassId == classId)
            .ToListAsync();

        if (registrations.Count == 0)
        {
            throw new BusinessRuleException("REGISTRATION_NOT_FOUND", "Không tìm thấy lượt đăng ký đang giữ chỗ cho lớp này.");
        }

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        List<ClassRegistration> cancelable = registrations
            .Where(item => item.Session.SessionDate > today ||
                (item.Session.SessionDate == today && item.Session.StartTime > currentTime))
            .OrderBy(item => item.Session.SessionDate)
            .ToList();

        if (cancelable.Count == 0)
        {
            throw new BusinessRuleException(
                "SESSION_ALREADY_STARTED",
                "Mọi buổi đã đăng ký đều đã bắt đầu hoặc đã qua nên không thể hủy.");
        }

        // Giữ nguyên quy tắc 7 giờ của thành viên, tính trên buổi gần nhất.
        ClassRegistration next = cancelable[0];
        DateTime nextStart = next.Session.SessionDate.Date + next.Session.StartTime;
        if (!isStaff && nextStart - localNow < TimeSpan.FromHours(7))
        {
            throw new BusinessRuleException(
                "REGISTRATION_CANCELLATION_TOO_LATE",
                "Members must cancel at least seven hours before the session starts.");
        }

        // Lấy tên trước khi nạp lại entity, để câu audit không phụ thuộc vào
        // trạng thái navigation sau ReloadAsync.
        string memberName = cancelable[0].Member?.FullName ?? memberAccountId;

        DateTime now = DateTime.UtcNow;
        IDbContextTransaction? transaction = operation.Transaction;
        try
        {
            foreach (ClassRegistration registration in cancelable)
            {
                if (_context.Database.IsRelational())
                {
                    object?[] sqlParams = new object?[] { now, actorAccountId, normalizedReason, registration.Id };
                    int rowsUpdated = await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE [dbo].[ClassRegistration] SET [Status] = 'CANCELED', [CanceledAt] = {0}, [CanceledBy] = {1}, [CancelReason] = {2} WHERE [Id] = {3} AND [Status] = 'BOOKED'",
                        sqlParams);

                    if (rowsUpdated == 0)
                    {
                        continue;
                    }

                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE [dbo].[ClassSession] SET [BookedCount] = CASE WHEN [BookedCount] > 0 THEN [BookedCount] - 1 ELSE 0 END WHERE [Id] = {0}",
                        registration.SessionId);

                    // Nạp lại thay vì gán tay: SQL thô đã đổi cả RowVersion, nên
                    // một entity đang theo dõi bị sửa trong bộ nhớ sẽ làm
                    // SaveChanges ném DbUpdateConcurrencyException.
                    await _context.Entry(registration).ReloadAsync();
                    await _context.Entry(registration.Session).ReloadAsync();
                }
                else
                {
                    if (registration.Session.BookedCount > 0)
                    {
                        registration.Session.BookedCount--;
                    }

                    registration.Status = "CANCELED";
                    registration.CanceledAt = now;
                    registration.CanceledBy = actorAccountId;
                    registration.CancelReason = normalizedReason;
                }
            }

            await _auditLogService.StageAuditLogAsync(
                actorAccountId,
                "CLASS_REGISTRATION_CANCELED",
                "Class",
                classId.ToString(),
                "Hủy đăng ký lớp của học viên " + memberName + " (" + cancelable.Count + " buổi).");

            await _context.SaveChangesAsync();

            await operation.CommitAsync();
        }
        finally
        {
            if (transaction is not null)
            {
                await operation.DisposeAsync();
            }
        }

        return await BuildEnrollmentAsync(memberAccountId, classId);
    }

    /// <summary>Danh sách lớp mà thành viên đã đăng ký, gộp theo lớp.</summary>
    public async Task<PagedResult<ClassEnrollmentViewModel>> GetMyClassEnrollmentsAsync(
        string memberAccountId,
        bool includeCanceled,
        int page,
        int pageSize)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<ClassRegistration> query = _context.ClassRegistrations.AsNoTracking()
            .Where(item => item.MemberId == memberAccountId);

        if (!includeCanceled)
        {
            query = query.Where(item => item.Status == "BOOKED");
        }

        List<int> classIds = await query
            .Select(item => item.Session.ClassId)
            .Distinct()
            .OrderByDescending(id => id)
            .ToListAsync();

        List<int> pageIds = classIds
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        List<ClassEnrollmentViewModel> items = new List<ClassEnrollmentViewModel>();
        foreach (int classId in pageIds)
        {
            ClassEnrollmentViewModel? enrollment = await TryBuildEnrollmentAsync(memberAccountId, classId);
            if (enrollment is not null)
            {
                items.Add(enrollment);
            }
        }

        return new PagedResult<ClassEnrollmentViewModel>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = classIds.Count
        };
    }

    private async Task<Member> LoadActiveMemberAsync(string memberAccountId)
    {
        Member? member = await _context.Members
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.AccountId == memberAccountId);

        if (member is null)
        {
            throw new BusinessRuleException("ACCOUNT_INACTIVE", "Không tìm thấy thông tin thành viên.");
        }

        if (member.Account.DeletedAt != null || member.Account.Status != "Active" || member.Account.IsLocked)
        {
            throw new BusinessRuleException("ACCOUNT_INACTIVE", "Tài khoản thành viên đã ngừng hoạt động hoặc bị khóa.");
        }

        return member;
    }

    private async Task<ClassEnrollmentViewModel> BuildEnrollmentAsync(string memberAccountId, int classId) =>
        await TryBuildEnrollmentAsync(memberAccountId, classId)
            ?? throw new BusinessRuleException("REGISTRATION_NOT_FOUND", "Không tìm thấy lượt đăng ký của lớp này.");

    /// <summary>Gộp các lượt đăng ký theo buổi của một lớp thành một lượt đăng ký lớp.</summary>
    private async Task<ClassEnrollmentViewModel?> TryBuildEnrollmentAsync(string memberAccountId, int classId)
    {
        List<ClassRegistration> registrations = await _context.ClassRegistrations.AsNoTracking()
            .Include(item => item.Member)
            .Include(item => item.Session)
                .ThenInclude(session => session.Class)
                    .ThenInclude(current => current.Discipline)
            .Include(item => item.Session)
                .ThenInclude(session => session.Room)
            .Where(item => item.MemberId == memberAccountId && item.Session.ClassId == classId)
            .OrderBy(item => item.Session.SessionDate)
            .ThenBy(item => item.Session.StartTime)
            .ToListAsync();

        if (registrations.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> coachNames = await LoadCoachNamesAsync(
            registrations.Select(item => item.Session.CoachId));
        Dictionary<string, string> actorNames = await LoadActorNamesAsync(registrations);

        List<ClassRegistration> booked = registrations.Where(item => item.Status == "BOOKED").ToList();
        List<ClassRegistration> active = booked.Count > 0 ? booked : registrations;
        ClassRegistration representative = active[0];

        DateTime localNow = DateTime.UtcNow.AddHours(7);
        DateTime today = localNow.Date;
        TimeSpan currentTime = localNow.TimeOfDay;

        Class currentClass = representative.Session.Class;
        ClassRegistration? canceled = registrations
            .Where(item => item.Status == "CANCELED")
            .OrderByDescending(item => item.CanceledAt)
            .FirstOrDefault();

        return new ClassEnrollmentViewModel
        {
            ClassId = classId,
            ClassName = currentClass.Name,
            SportId = currentClass.DisciplineId,
            SportName = currentClass.Discipline?.Name ?? string.Empty,
            MemberAccountId = memberAccountId,
            MemberName = representative.Member?.FullName ?? string.Empty,
            MemberCode = representative.Member?.MemberCode ?? string.Empty,
            Status = booked.Count > 0 ? "BOOKED" : "CANCELED",
            RegisteredAt = registrations.Min(item => item.RegisteredAt),
            RegisteredVia = representative.RegisteredVia,
            RegisteredByName = actorNames.GetValueOrDefault(representative.RegisteredBy),
            CanceledAt = booked.Count > 0 ? null : canceled?.CanceledAt,
            CanceledByName = booked.Count > 0 || canceled?.CanceledBy is null
                ? null
                : actorNames.GetValueOrDefault(canceled.CanceledBy),
            CancelReason = booked.Count > 0 ? null : canceled?.CancelReason,
            BookedSessionCount = booked.Count,
            UpcomingSessionCount = booked.Count(item =>
                item.Session.Status == "SCHEDULED" &&
                (item.Session.SessionDate > today ||
                 (item.Session.SessionDate == today && item.Session.StartTime > currentTime))),
            ScheduleBlocks = BuildEnrollmentSchedule(active, coachNames),
            Sessions = registrations
                .Select(item => MapRegistration(item, coachNames, actorNames))
                .ToList()
        };
    }

    /// <summary>
    /// Các đợt lịch của những buổi thành viên đang giữ chỗ. Dùng chung cách gộp
    /// với <see cref="ClassService.ClassService.BuildScheduleBlocks"/>, để lịch
    /// lớp ở màn hình quản lý và ở màn hình thành viên không hiện khác nhau.
    /// </summary>
    private static List<APIViewModel.Class.ClassScheduleBlockViewModel> BuildEnrollmentSchedule(
        List<ClassRegistration> registrations,
        Dictionary<string, string> coachNames) =>
        ClassService.ClassService.BuildScheduleBlocks(
            registrations.Select(item => item.Session),
            coachNames);

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

    private async Task<Dictionary<string, string>> LoadActorNamesAsync(IEnumerable<ClassRegistration> registrations)
    {
        List<string> ids = registrations.SelectMany(item => item.CanceledBy is null
            ? new[] { item.RegisteredBy } : new[] { item.RegisteredBy, item.CanceledBy }).Distinct().ToList();
        return await _context.Accounts.AsNoTracking().Include(item => item.Member).Include(item => item.Coach)
            .Include(item => item.Receptionist).Include(item => item.CenterManager).Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Member != null ? item.Member.FullName ?? string.Empty :
                item.Coach != null ? item.Coach.FullName : item.Receptionist != null ? item.Receptionist.FullName :
                item.CenterManager != null ? item.CenterManager.FullName : string.Empty);
    }

    private static ClassRegistrationViewModel MapRegistration(
        ClassRegistration registration,
        Dictionary<string, string> coachNames, Dictionary<string, string> actorNames)
    {
        ClassSessionViewDto sessionDto = Services.ClassSessionService.ClassSessionService.MapSession(registration.Session, coachNames);

        return new ClassRegistrationViewModel
        {
            Id = registration.Id,
            SessionId = registration.SessionId,
            MemberAccountId = registration.MemberId,
            MemberName = registration.Member?.FullName ?? string.Empty,
            MemberCode = registration.Member?.MemberCode ?? string.Empty,
            Status = registration.Status,
            RegisteredAt = registration.RegisteredAt,
            RegisteredVia = registration.RegisteredVia,
            RegisteredByName = actorNames.GetValueOrDefault(registration.RegisteredBy),
            CanceledAt = registration.CanceledAt,
            CanceledByName = registration.CanceledBy is null ? null : actorNames.GetValueOrDefault(registration.CanceledBy),
            CancelReason = registration.CancelReason,
            Session = sessionDto
        };
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
