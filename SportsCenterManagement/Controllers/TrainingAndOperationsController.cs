using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class TrainingAndOperationsController : ControllerBase
{
    private readonly SportsCenterManagementContext _db;

    public TrainingAndOperationsController(SportsCenterManagementContext db)
    {
        _db = db;
    }

    private string CallerId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static DateTime NowVietnam()
    {
        try
        {
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            return DateTime.UtcNow.AddHours(7);
        }
    }

    private static DateOnly TodayVietnam() => DateOnly.FromDateTime(NowVietnam());

    private async Task AutoCloseStaleVisitsAsync(DateOnly today)
    {
        var staleVisits = await _db.CenterVisits
            .Where(v => v.VisitDate < today && !v.CheckedOutAt.HasValue)
            .ToListAsync();
        if (staleVisits.Count == 0)
        {
            return;
        }

        foreach (var stale in staleVisits)
        {
            DateTime closingUtc = stale.VisitDate.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
            if (closingUtc <= stale.CheckedInAt)
            {
                closingUtc = stale.CheckedInAt.AddHours(1);
            }

            stale.CheckedOutAt = closingUtc;
            stale.CheckedOutBy = "SYSTEM";
            stale.Note = string.IsNullOrWhiteSpace(stale.Note)
                ? "Hệ thống tự động đóng lượt cuối ngày"
                : $"{stale.Note} · Tự động đóng cuối ngày";
        }

        await _db.SaveChangesAsync();
    }

    private async Task EnsureTodayCenterVisitForAttendedStudentAsync(string memberId, string className, DateOnly today)
    {
        bool hasVisitToday = await _db.CenterVisits
            .AnyAsync(v => v.MemberId == memberId && v.VisitDate == today);
        if (hasVisitToday)
        {
            return;
        }

        _db.CenterVisits.Add(new CenterVisit
        {
            Id = $"VIS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            MemberId = memberId,
            VisitDate = today,
            CheckedInAt = DateTime.UtcNow,
            CheckedInBy = string.IsNullOrWhiteSpace(CallerId) ? "SYSTEM" : CallerId,
            Note = $"Đồng bộ tự động từ điểm danh lớp {className}"
        });
    }

    private async Task<string> ResolveEffectiveCoachIdAsync(int? classId = null, int? sessionId = null, string? memberId = null)
    {
        if (User.IsInRole("Coach") && !string.IsNullOrWhiteSpace(CallerId))
        {
            return CallerId;
        }

        if (sessionId.HasValue)
        {
            string? sessionCoachId = await _db.ClassSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId.Value)
                .Select(s => s.CoachId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(sessionCoachId))
            {
                return sessionCoachId;
            }
        }

        if (classId.HasValue)
        {
            string? classCoachId = await _db.ClassSessions
                .AsNoTracking()
                .Where(s => s.ClassId == classId.Value && s.Status != "CANCELED")
                .OrderByDescending(s => s.SessionDate)
                .Select(s => s.CoachId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(classCoachId))
            {
                return classCoachId;
            }
        }

        if (!string.IsNullOrWhiteSpace(memberId))
        {
            string? studentCoachId = await _db.ClassRegistrations
                .AsNoTracking()
                .Where(r => r.MemberId == memberId && r.Status == "BOOKED")
                .OrderByDescending(r => r.Session.SessionDate)
                .Select(r => r.Session.CoachId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(studentCoachId))
            {
                return studentCoachId;
            }
        }

        string? fallbackCoachId = await _db.Coaches
            .AsNoTracking()
            .Include(c => c.Account)
            .Where(c => c.Account.DeletedAt == null && c.Account.Status == "Active" && !c.Account.IsLocked)
            .OrderBy(c => c.AccountId)
            .Select(c => c.AccountId)
            .FirstOrDefaultAsync();

        return fallbackCoachId ?? "acc-coa-001";
    }

    private void AddAuditLog(string action, string entityType, string entityId, string description)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid().ToString("N"),
            AccountId = string.IsNullOrWhiteSpace(CallerId) ? "acc-mgr-001" : CallerId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<(Member? Member, string? ErrorMessage, bool IsForbidden)> ValidateMemberAndCoachScopeAsync(
        string memberId,
        bool checkCoachScope)
    {
        var member = await _db.Members
            .AsNoTracking()
            .Include(m => m.Account)
            .FirstOrDefaultAsync(m => m.AccountId == memberId);

        if (member is null || member.Account.DeletedAt.HasValue)
        {
            return (null, "Không tìm thấy hồ sơ học viên trong hệ thống.", false);
        }

        if (!string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase) || member.Account.IsLocked)
        {
            return (null, "Tài khoản học viên hiện đang bị khóa hoặc ngừng hoạt động nên không thể thực hiện thao tác này.", false);
        }

        if (checkCoachScope && User.IsInRole("Coach") && !User.IsInRole("CenterManager"))
        {
            string coachId = CallerId;
            bool isAssignedStudent = await _db.ClassRegistrations
                .AsNoTracking()
                .AnyAsync(r => r.MemberId == memberId && r.Status == "BOOKED" && r.Session.CoachId == coachId);

            if (!isAssignedStudent)
            {
                bool hasExistingPlanOrResult = await _db.TrainingPlans.AsNoTracking().AnyAsync(p => p.MemberId == memberId && p.CoachId == coachId)
                    || await _db.TrainingResults.AsNoTracking().AnyAsync(r => r.MemberId == memberId && r.CoachId == coachId);

                if (!hasExistingPlanOrResult)
                {
                    return (null, "Bạn chỉ được phép thao tác huấn luyện đối với học viên thuộc các lớp do mình phụ trách.", true);
                }
            }
        }

        return (member, null, false);
    }

    // =====================================================================
    // CỤM 1: ĐIỂM DANH LỚP HỌC (COACH) & CHECK-IN / HỖ TRỢ / BẢO LƯU TẠI QUẦY
    // =====================================================================

    [HttpGet("sessions/{sessionId:int}/attendance")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> GetSessionAttendanceAsync(int sessionId)
    {
        var session = await _db.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            return NotFound("Không tìm thấy buổi học.");
        }

        if (User.IsInRole("Coach") && !User.IsInRole("CenterManager") && !string.Equals(session.CoachId, CallerId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        DateTime vnNow = NowVietnam();
        DateOnly today = DateOnly.FromDateTime(vnNow);
        DateOnly sessionDay = DateOnly.FromDateTime(session.SessionDate);
        bool isCanceled = string.Equals(session.Status, "CANCELED", StringComparison.OrdinalIgnoreCase);
        bool isFutureDate = sessionDay > today;
        bool isTooEarlyToday = sessionDay == today && vnNow.TimeOfDay < session.StartTime - TimeSpan.FromMinutes(15);
        bool canTakeAttendance = !isCanceled && !isFutureDate && !isTooEarlyToday;
        string? attendanceBlockedReason = isCanceled
            ? "Buổi học này đã bị hủy nên không thể điểm danh."
            : isFutureDate
                ? $"Buổi học diễn ra vào ngày {session.SessionDate:dd/MM/yyyy} (chưa diễn ra). Chỉ được điểm danh các buổi học hôm nay hoặc đã diễn ra."
                : isTooEarlyToday
                    ? $"Ca học bắt đầu lúc {session.StartTime:hh\\:mm} hôm nay. Hệ thống chỉ mở điểm danh từ 15 phút trước giờ vào lớp."
                    : null;

        var registrations = await _db.ClassRegistrations
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .ToListAsync();

        var memberIds = registrations.Select(r => r.MemberId).Distinct().ToList();
        var attendances = await _db.ClassSessionAttendances
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .ToListAsync();
        var profiles = await _db.MemberTrainingProfiles
            .AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId))
            .ToListAsync();

        string sessionDateStr = session.SessionDate.ToString("yyyy-MM-dd");
        string timeRangeStr = $"{session.StartTime:hh\\:mm} - {session.EndTime:hh\\:mm}";
        string roomNameStr = session.Room?.Name ?? "Phòng tập";

        var items = registrations.Select(r =>
        {
            var att = attendances.FirstOrDefault(a => a.RegistrationId == r.Id);
            var prof = profiles.FirstOrDefault(p => p.MemberId == r.MemberId);
            return new
            {
                registrationId = r.Id,
                memberId = r.MemberId,
                attendanceStatus = att?.Status,
                attendanceNote = att?.Note,
                recordedAt = att?.RecordedAt,
                fitnessGoal = prof?.FitnessGoal ?? "Nâng cao thể lực nền tảng, cải thiện độ linh hoạt",
                fitnessLevel = prof?.FitnessLevel ?? "BEGINNER",
                sessionClassName = session.Class.Name,
                sessionDate = sessionDateStr,
                sessionTimeRange = timeRangeStr,
                sessionRoomName = roomNameStr,
                sessionStatus = session.Status,
                canTakeAttendance,
                attendanceBlockedReason
            };
        });

        return Ok(items);
    }

    [HttpPut("sessions/{sessionId:int}/registrations/{registrationId:int}/attendance")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> RecordSessionAttendanceAsync(
        int sessionId,
        int registrationId,
        [FromBody] RecordClassAttendanceRequest request)
    {
        string status = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (status is not ("PRESENT" or "LATE" or "ABSENT"))
        {
            return BadRequest("Trạng thái điểm danh phải là PRESENT, LATE hoặc ABSENT.");
        }

        var session = await _db.ClassSessions
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            return NotFound("Không tìm thấy buổi học.");
        }

        if (User.IsInRole("Coach") && !User.IsInRole("CenterManager") && !string.Equals(session.CoachId, CallerId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (string.Equals(session.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Không thể điểm danh buổi học đã bị hủy.");
        }

        DateTime vnNow = NowVietnam();
        DateOnly today = DateOnly.FromDateTime(vnNow);
        DateOnly sessionDay = DateOnly.FromDateTime(session.SessionDate);
        if (sessionDay > today)
        {
            return BadRequest($"Buổi học diễn ra vào ngày {session.SessionDate:dd/MM/yyyy} (chưa diễn ra). Chỉ được điểm danh buổi học hôm nay hoặc đã diễn ra.");
        }
        if (sessionDay == today && vnNow.TimeOfDay < session.StartTime - TimeSpan.FromMinutes(15))
        {
            return BadRequest($"Ca học bắt đầu lúc {session.StartTime:hh\\:mm} hôm nay. Chỉ được điểm danh từ 15 phút trước giờ bắt đầu ca học.");
        }

        var reg = await _db.ClassRegistrations
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.SessionId == sessionId);
        if (reg is null)
        {
            return NotFound("Không tìm thấy đăng ký học viên trong buổi học này.");
        }

        if (!string.Equals(reg.Status, "BOOKED", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Học viên đã hủy đăng ký buổi học này nên không thể điểm danh.");
        }

        var existing = await _db.ClassSessionAttendances
            .FirstOrDefaultAsync(a => a.RegistrationId == registrationId);
        string? trimmedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (existing is null)
        {
            existing = new ClassSessionAttendance
            {
                RegistrationId = registrationId,
                SessionId = sessionId,
                MemberId = reg.MemberId,
                Status = status,
                Note = trimmedNote,
                RecordedBy = CallerId,
                RecordedAt = DateTime.UtcNow
            };
            _db.ClassSessionAttendances.Add(existing);
        }
        else
        {
            existing.Status = status;
            existing.Note = trimmedNote;
            existing.RecordedBy = CallerId;
            existing.RecordedAt = DateTime.UtcNow;
        }

        if (sessionDay == today && status is "PRESENT" or "LATE")
        {
            await EnsureTodayCenterVisitForAttendedStudentAsync(reg.MemberId, session.Class.Name, today);
        }

        string statusLabel = status switch
        {
            "PRESENT" => "Có mặt",
            "LATE" => "Đi muộn",
            _ => "Vắng mặt"
        };

        AddAuditLog(
            "RECORD_ATTENDANCE",
            "ClassSession",
            sessionId.ToString(),
            $"Điểm danh học viên {reg.MemberId} trong buổi học #{sessionId} ({session.Class.Name}): {statusLabel}");

        _db.Notifications.Add(new Notification
        {
            RecipientId = reg.MemberId,
            Type = "CLASS_ATTENDANCE_RECORDED",
            Title = $"Điểm danh lớp {session.Class.Name}: {statusLabel}",
            Body = $"Huấn luyện viên đã ghi nhận trạng thái điểm danh buổi học ngày {session.SessionDate:dd/MM/yyyy} là \"{statusLabel}\"{(trimmedNote is not null ? $". Ghi chú: {trimmedNote}" : ".")}",
            EntityType = "ClassSession",
            EntityId = sessionId.ToString(),
            EventAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            registrationId = existing.RegistrationId,
            sessionId = existing.SessionId,
            memberId = existing.MemberId,
            attendanceStatus = existing.Status,
            attendanceNote = existing.Note,
            recordedAt = existing.RecordedAt
        });
    }

    [HttpPost("sessions/{sessionId:int}/attendance/mark-all-present")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> MarkAllSessionAttendancePresentAsync(int sessionId)
    {
        var session = await _db.ClassSessions
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            return NotFound("Không tìm thấy buổi học.");
        }

        if (User.IsInRole("Coach") && !User.IsInRole("CenterManager") && !string.Equals(session.CoachId, CallerId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (string.Equals(session.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Không thể điểm danh buổi học đã bị hủy.");
        }

        DateTime vnNow = NowVietnam();
        DateOnly today = DateOnly.FromDateTime(vnNow);
        DateOnly sessionDay = DateOnly.FromDateTime(session.SessionDate);
        if (sessionDay > today)
        {
            return BadRequest($"Buổi học diễn ra vào ngày {session.SessionDate:dd/MM/yyyy} (chưa diễn ra). Chỉ được điểm danh buổi học hôm nay hoặc đã diễn ra.");
        }
        if (sessionDay == today && vnNow.TimeOfDay < session.StartTime - TimeSpan.FromMinutes(15))
        {
            return BadRequest($"Ca học bắt đầu lúc {session.StartTime:hh\\:mm} hôm nay. Chỉ được điểm danh từ 15 phút trước giờ bắt đầu ca học.");
        }

        var bookedRegs = await _db.ClassRegistrations
            .Where(r => r.SessionId == sessionId && r.Status == "BOOKED")
            .ToListAsync();
        if (bookedRegs.Count == 0)
        {
            return BadRequest("Không có học viên nào đăng ký trong buổi học này.");
        }

        var existingAttendances = await _db.ClassSessionAttendances
            .Where(a => a.SessionId == sessionId)
            .ToListAsync();

        int updatedCount = 0;
        DateTime now = DateTime.UtcNow;
        foreach (var reg in bookedRegs)
        {
            var existing = existingAttendances.FirstOrDefault(a => a.RegistrationId == reg.Id);
            if (existing is null)
            {
                _db.ClassSessionAttendances.Add(new ClassSessionAttendance
                {
                    RegistrationId = reg.Id,
                    SessionId = sessionId,
                    MemberId = reg.MemberId,
                    Status = "PRESENT",
                    Note = "Điểm danh có mặt đúng giờ",
                    RecordedBy = CallerId,
                    RecordedAt = now
                });
                updatedCount++;
            }

            if (sessionDay == today)
            {
                await EnsureTodayCenterVisitForAttendedStudentAsync(reg.MemberId, session.Class.Name, today);
            }
        }

        if (updatedCount > 0)
        {
            AddAuditLog(
                "RECORD_ATTENDANCE",
                "ClassSession",
                sessionId.ToString(),
                $"Điểm danh nhanh Có mặt ({updatedCount}/{bookedRegs.Count} học viên) cho buổi học #{sessionId} ({session.Class.Name}).");
            await _db.SaveChangesAsync();
        }

        return Ok(new { markedCount = updatedCount, totalBooked = bookedRegs.Count });
    }

    [HttpGet("reception/snapshot")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> GetReceptionSnapshotAsync()
    {
        DateOnly today = TodayVietnam();
        await AutoCloseStaleVisitsAsync(today);

        var visits = await _db.CenterVisits
            .AsNoTracking()
            .OrderByDescending(v => v.CheckedInAt)
            .Take(200)
            .ToListAsync();

        var requests = await _db.SupportRequests
            .AsNoTracking()
            .OrderByDescending(r => r.UpdatedAt)
            .Take(200)
            .ToListAsync();

        var allSubs = await _db.MemberSubscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.EndDate)
            .ToListAsync();

        var accountIds = visits.Select(v => v.CheckedInBy)
            .Concat(visits.Where(v => v.CheckedOutBy != null).Select(v => v.CheckedOutBy!))
            .Concat(requests.Select(r => r.CreatedBy))
            .Concat(requests.Where(r => r.UpdatedBy != null).Select(r => r.UpdatedBy!))
            .Distinct()
            .ToList();

        var staffNames = await ResolveActorNamesAsync(accountIds);

        var todayVisitsByMember = visits
            .Where(v => v.VisitDate == today)
            .GroupBy(v => v.MemberId)
            .ToDictionary(g => g.Key, g => g.First());

        var memberEligibility = allSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var activeValid = g.FirstOrDefault(s =>
                        string.Equals(s.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase) &&
                        !s.IsSuspended &&
                        s.StartDate <= today &&
                        s.EndDate >= today);
                    var suspendedSub = g.FirstOrDefault(s =>
                        string.Equals(s.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase) &&
                        s.IsSuspended &&
                        s.StartDate <= today &&
                        s.EndDate >= today);
                    var pendingSub = g.FirstOrDefault(s =>
                        string.Equals(s.Status, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase));
                    var latestSub = activeValid ?? suspendedSub ?? pendingSub ?? g.First();

                    todayVisitsByMember.TryGetValue(g.Key, out var todayVisit);
                    bool checkedInToday = todayVisit is not null;
                    bool currentlyInCenter = todayVisit is not null && !todayVisit.CheckedOutAt.HasValue;

                    string statusText;
                    bool canCheckIn = false;
                    if (activeValid is not null)
                    {
                        if (currentlyInCenter)
                        {
                            statusText = $"Đang tập tại trung tâm (đã vào lúc {DateTime.SpecifyKind(todayVisit!.CheckedInAt, DateTimeKind.Utc).AddHours(7):HH:mm})";
                        }
                        else if (checkedInToday)
                        {
                            canCheckIn = true;
                            statusText = $"Đủ điều kiện vào ca tiếp theo (đã hoàn tất 1 lượt hôm nay) · Gói {activeValid.PackageName}";
                        }
                        else
                        {
                            canCheckIn = true;
                            statusText = $"Đủ điều kiện vào tập · {activeValid.PackageName} (HSD: {activeValid.EndDate:dd/MM/yyyy})";
                        }
                    }
                    else if (suspendedSub is not null)
                    {
                        statusText = $"Gói {suspendedSub.PackageName} đang tạm ngưng (bảo lưu) — Không thể vào tập";
                    }
                    else if (pendingSub is not null)
                    {
                        statusText = $"Gói {pendingSub.PackageName} đang chờ thanh toán — Cần thu tiền trước khi điểm danh";
                    }
                    else
                    {
                        statusText = $"Gói {latestSub.PackageName} đã hết hạn ngày {latestSub.EndDate:dd/MM/yyyy} — Cần gia hạn";
                    }

                    return new
                    {
                        memberId = g.Key,
                        canCheckIn,
                        checkedInToday,
                        currentlyInCenter,
                        packageName = latestSub.PackageName,
                        endDate = latestSub.EndDate.ToString("yyyy-MM-dd"),
                        statusText
                    };
                });

        return Ok(new
        {
            version = 1,
            visits = visits.Select(v => new
            {
                id = v.Id,
                memberId = v.MemberId,
                date = v.VisitDate.ToString("yyyy-MM-dd"),
                checkedInAt = DateTime.SpecifyKind(v.CheckedInAt, DateTimeKind.Utc).ToString("O"),
                checkedOutAt = v.CheckedOutAt.HasValue
                    ? DateTime.SpecifyKind(v.CheckedOutAt.Value, DateTimeKind.Utc).ToString("O")
                    : null,
                checkedInBy = staffNames.GetValueOrDefault(v.CheckedInBy, v.CheckedInBy),
                checkedOutBy = v.CheckedOutBy != null
                    ? staffNames.GetValueOrDefault(v.CheckedOutBy, v.CheckedOutBy)
                    : null,
                note = v.Note
            }),
            requests = requests.Select(r => new
            {
                id = r.Id,
                memberId = r.MemberId,
                category = r.Category,
                subject = r.Subject,
                note = r.Note,
                status = r.Status,
                resolutionNote = r.ResolutionNote,
                createdBy = staffNames.GetValueOrDefault(r.CreatedBy, r.CreatedBy),
                updatedBy = r.UpdatedBy != null ? staffNames.GetValueOrDefault(r.UpdatedBy, r.UpdatedBy) : null,
                createdAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc).ToString("O"),
                updatedAt = DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc).ToString("O")
            }),
            memberEligibility
        });
    }

    [HttpPost("reception/check-in")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> CheckInMemberAsync([FromBody] CheckInMemberRequest request)
    {
        string memberId = (request.MemberId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(memberId))
        {
            return BadRequest("Vui lòng chọn thành viên cần điểm danh.");
        }

        var member = await _db.Members
            .Include(m => m.Account)
            .FirstOrDefaultAsync(m => m.AccountId == memberId);
        if (member is null || member.Account.DeletedAt.HasValue)
        {
            return NotFound("Không tìm thấy thành viên.");
        }

        if (!string.Equals(member.Account.Status, "Active", StringComparison.OrdinalIgnoreCase) || member.Account.IsLocked)
        {
            return BadRequest("Tài khoản thành viên đang bị khóa hoặc ngừng hoạt động.");
        }

        DateOnly today = TodayVietnam();
        await AutoCloseStaleVisitsAsync(today);

        var memberSubs = await _db.MemberSubscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.EndDate)
            .ToListAsync();

        var activeSub = memberSubs.FirstOrDefault(s =>
            string.Equals(s.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase) &&
            !s.IsSuspended &&
            s.StartDate <= today &&
            s.EndDate >= today);

        if (activeSub is null)
        {
            var suspendedSub = memberSubs.FirstOrDefault(s =>
                string.Equals(s.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase) &&
                s.IsSuspended &&
                s.StartDate <= today &&
                s.EndDate >= today);
            if (suspendedSub is not null)
            {
                return BadRequest($"Gói tập \"{suspendedSub.PackageName}\" của thành viên đang trong thời gian tạm ngưng (bảo lưu), không thể điểm danh vào tập.");
            }

            var pendingSub = memberSubs.FirstOrDefault(s =>
                string.Equals(s.Status, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase));
            if (pendingSub is not null)
            {
                return BadRequest($"Gói tập \"{pendingSub.PackageName}\" của thành viên đang chờ thanh toán. Vui lòng thu tiền tại mục Hóa đơn trước khi điểm danh.");
            }

            return BadRequest("Thành viên chưa có gói tập đang hoạt động hoặc gói tập đã hết hạn sử dụng.");
        }

        var openTodayVisit = await _db.CenterVisits
            .FirstOrDefaultAsync(v => v.MemberId == memberId && v.VisitDate == today && !v.CheckedOutAt.HasValue);
        if (openTodayVisit is not null)
        {
            return Conflict("Thành viên đang tập tại trung tâm (chưa ghi nhận giờ ra về cho lượt vào trước đó).");
        }

        var visit = new CenterVisit
        {
            Id = $"VIS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            MemberId = memberId,
            VisitDate = today,
            CheckedInAt = DateTime.UtcNow,
            CheckedInBy = CallerId,
            Note = string.IsNullOrWhiteSpace(request.Note)
                ? $"Gói sử dụng: {activeSub.PackageName}"
                : request.Note.Trim()
        };

        _db.CenterVisits.Add(visit);
        AddAuditLog(
            "CHECK_IN",
            "CenterVisit",
            visit.Id,
            $"Điểm danh thành viên {member.FullName ?? member.MemberCode} đến trung tâm ({activeSub.PackageName}).");

        await _db.SaveChangesAsync();
        return Ok(new { id = visit.Id });
    }

    [HttpPut("reception/visits/{visitId}/check-out")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> CheckOutMemberAsync(string visitId)
    {
        var visit = await _db.CenterVisits.FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null)
        {
            return NotFound("Không tìm thấy lượt điểm danh.");
        }

        if (visit.CheckedOutAt.HasValue)
        {
            return Conflict("Lượt điểm danh này đã được ghi nhận giờ ra về.");
        }

        visit.CheckedOutAt = DateTime.UtcNow;
        visit.CheckedOutBy = CallerId;

        AddAuditLog(
            "CHECK_OUT",
            "CenterVisit",
            visit.Id,
            $"Ghi nhận thành viên {visit.MemberId} kết thúc buổi tập và rời trung tâm (Mã lượt: {visit.Id}).");

        await _db.SaveChangesAsync();

        return Ok(new { id = visit.Id, checkedOutAt = visit.CheckedOutAt });
    }

    [HttpPut("subscriptions/{subscriptionId:int}/suspension")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> UpdateSubscriptionSuspensionAsync(
        int subscriptionId,
        [FromBody] UpdateSubscriptionSuspensionRequest request)
    {
        var subscription = await _db.MemberSubscriptions
            .Include(s => s.Member)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription is null)
        {
            return NotFound("Không tìm thấy gói đăng ký của thành viên.");
        }

        if (!string.Equals(subscription.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Chỉ có thể tạm ngưng (bảo lưu) hoặc kích hoạt lại các gói tập đã được xác nhận thanh toán (CONFIRMED).");
        }

        DateOnly today = TodayVietnam();
        if (subscription.EndDate < today)
        {
            return BadRequest("Gói tập này đã hết hạn sử dụng nên không thể thực hiện bảo lưu hoặc mở lại.");
        }

        if (request.IsSuspended && subscription.StartDate > today)
        {
            return BadRequest($"Gói tập này chỉ bắt đầu có hiệu lực từ ngày {subscription.StartDate:dd/MM/yyyy} nên chưa thể thực hiện bảo lưu.");
        }

        if (subscription.IsSuspended == request.IsSuspended)
        {
            return BadRequest(request.IsSuspended
                ? "Gói tập này đang ở trạng thái tạm ngưng (bảo lưu) rồi."
                : "Gói tập này đang hoạt động bình thường.");
        }

        string? trimmedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (request.IsSuspended && (trimmedReason is null || trimmedReason.Length < 4))
        {
            return BadRequest("Vui lòng nhập lý do tạm ngưng / bảo lưu gói tập (tối thiểu 4 ký tự).");
        }

        subscription.IsSuspended = request.IsSuspended;
        subscription.SuspensionReason = request.IsSuspended ? trimmedReason : null;

        DateTime now = DateTime.UtcNow;
        string memberName = subscription.Member?.FullName ?? subscription.MemberId;
        int compensatedDays = 0;

        if (request.IsSuspended)
        {
            DateTime localNow = now.AddHours(7);
            DateTime todayDate = localNow.Date;
            TimeSpan currentTime = localNow.TimeOfDay;

            var upcomingBookings = await _db.ClassRegistrations
                .Include(r => r.Session)
                .Where(r =>
                    r.MemberId == subscription.MemberId &&
                    r.Status == "BOOKED" &&
                    r.Session.Status == "SCHEDULED" &&
                    (r.Session.SessionDate > todayDate ||
                     (r.Session.SessionDate == todayDate && r.Session.StartTime > currentTime)))
                .ToListAsync();

            foreach (var booking in upcomingBookings)
            {
                booking.Status = "CANCELED";
                booking.CanceledAt = now;
                booking.CanceledBy = string.IsNullOrWhiteSpace(CallerId) ? "acc-mgr-001" : CallerId;
                booking.CancelReason = $"Tự động hủy giữ chỗ do tạm ngưng (bảo lưu) gói tập: {trimmedReason}";
                if (booking.Session.BookedCount > 0)
                {
                    booking.Session.BookedCount -= 1;
                    booking.Session.UpdatedAt = now;
                }
            }

            string releasedNote = upcomingBookings.Count > 0
                ? $" Đã tự động giải phóng {upcomingBookings.Count} buổi học đặt trước."
                : string.Empty;

            AddAuditLog(
                "SUSPEND_SUBSCRIPTION",
                "MemberSubscription",
                subscriptionId.ToString(),
                $"Tạm ngưng (bảo lưu) gói tập \"{subscription.PackageName}\" của hội viên {memberName}. Lý do: {trimmedReason}.{releasedNote}");

            _db.Notifications.Add(new Notification
            {
                RecipientId = subscription.MemberId,
                Type = "MEMBERSHIP_SUSPENDED",
                Title = $"Gói tập {subscription.PackageName} đã được tạm ngưng (bảo lưu)",
                Body = $"Gói thành viên \"{subscription.PackageName}\" của bạn đã chuyển sang trạng thái tạm ngưng. Lý do: {trimmedReason}.{releasedNote} Vui lòng liên hệ quầy lễ tân khi bạn muốn mở lại gói tập.",
                EntityType = "MemberSubscription",
                EntityId = subscriptionId.ToString(),
                EventAt = now,
                CreatedAt = now,
                IsRead = false
            });
        }
        else
        {
            var lastSuspendLog = await _db.AuditLogs
                .AsNoTracking()
                .Where(a =>
                    a.EntityType == "MemberSubscription" &&
                    a.EntityId == subscriptionId.ToString() &&
                    a.Action == "SUSPEND_SUBSCRIPTION")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastSuspendLog is not null)
            {
                DateOnly suspendedStartDate = DateOnly.FromDateTime(lastSuspendLog.CreatedAt.AddHours(7));
                compensatedDays = Math.Max(0, today.DayNumber - suspendedStartDate.DayNumber);
            }

            if (compensatedDays > 0)
            {
                subscription.EndDate = subscription.EndDate.AddDays(compensatedDays);

                var queuedUpcomingSubs = await _db.MemberSubscriptions
                    .Where(s =>
                        s.MemberId == subscription.MemberId &&
                        s.Id != subscription.Id &&
                        s.StartDate > today &&
                        (s.Status == "CONFIRMED" || s.Status == "PENDING_PAYMENT"))
                    .OrderBy(s => s.StartDate)
                    .ToListAsync();

                foreach (var queued in queuedUpcomingSubs)
                {
                    queued.StartDate = queued.StartDate.AddDays(compensatedDays);
                    queued.EndDate = queued.EndDate.AddDays(compensatedDays);
                }
            }

            string compensatedText = compensatedDays > 0
                ? $" Đã tự động cộng bù {compensatedDays} ngày bảo lưu (hạn mới: {subscription.EndDate:dd/MM/yyyy})."
                : string.Empty;

            AddAuditLog(
                "RESUME_SUBSCRIPTION",
                "MemberSubscription",
                subscriptionId.ToString(),
                $"Kích hoạt mở lại gói tập \"{subscription.PackageName}\" cho hội viên {memberName}.{compensatedText}");

            _db.Notifications.Add(new Notification
            {
                RecipientId = subscription.MemberId,
                Type = "MEMBERSHIP_RESUMED",
                Title = $"Gói tập {subscription.PackageName} đã được kích hoạt trở lại",
                Body = $"Gói thành viên \"{subscription.PackageName}\" (HSD: {subscription.EndDate:dd/MM/yyyy}) của bạn đã hoạt động bình thường trở lại.{compensatedText} Bạn có thể đặt lịch học và điểm danh tại trung tâm.",
                EntityType = "MemberSubscription",
                EntityId = subscriptionId.ToString(),
                EventAt = now,
                CreatedAt = now,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = subscription.Id,
            memberId = subscription.MemberId,
            isSuspended = subscription.IsSuspended,
            suspensionReason = subscription.SuspensionReason,
            endDate = subscription.EndDate.ToString("yyyy-MM-dd"),
            compensatedDays
        });
    }

    [HttpPost("reception/support-requests")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> CreateSupportRequestAsync([FromBody] CreateSupportRequestDto input)
    {
        string memberId = (input.MemberId ?? string.Empty).Trim();
        string subject = (input.Subject ?? string.Empty).Trim();
        string note = (input.Note ?? string.Empty).Trim();
        string category = string.IsNullOrWhiteSpace(input.Category) ? "Gói tập" : input.Category.Trim();

        if (string.IsNullOrEmpty(memberId) || subject.Length < 4 || note.Length < 8)
        {
            return BadRequest("Vui lòng chọn thành viên, nhập tiêu đề (tối thiểu 4 ký tự) và nội dung chi tiết (tối thiểu 8 ký tự).");
        }

        bool memberExists = await _db.Members
            .Include(m => m.Account)
            .AnyAsync(m => m.AccountId == memberId && m.Account.DeletedAt == null);
        if (!memberExists)
        {
            return NotFound("Không tìm thấy thành viên.");
        }

        DateTime now = DateTime.UtcNow;
        var req = new SupportRequest
        {
            Id = $"SUP-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            MemberId = memberId,
            Category = category,
            Subject = subject,
            Note = note,
            Status = "OPEN",
            CreatedBy = CallerId,
            UpdatedBy = CallerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SupportRequests.Add(req);
        AddAuditLog(
            "CREATE_SUPPORT_REQUEST",
            "SupportRequest",
            req.Id,
            $"Tiếp nhận yêu cầu hỗ trợ [{category}] cho hội viên {memberId}: {subject}");

        _db.Notifications.Add(new Notification
        {
            RecipientId = memberId,
            Type = "SUPPORT_REQUEST_CREATED",
            Title = $"Đã tiếp nhận yêu cầu hỗ trợ: {subject}",
            Body = $"Quầy lễ tân đã ghi nhận yêu cầu hỗ trợ [{category}] của bạn. Mã phiếu: {req.Id}.",
            EntityType = "SupportRequest",
            EntityId = req.Id,
            EventAt = now,
            CreatedAt = now,
            IsRead = false
        });

        await _db.SaveChangesAsync();
        return Ok(new { id = req.Id });
    }

    [HttpPut("reception/support-requests/{requestId}")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> UpdateSupportRequestAsync(
        string requestId,
        [FromBody] UpdateSupportRequestDto input)
    {
        var req = await _db.SupportRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (req is null)
        {
            return NotFound("Không tìm thấy yêu cầu hỗ trợ.");
        }

        string status = (input.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (status is not ("OPEN" or "IN_PROGRESS" or "RESOLVED" or "CLOSED"))
        {
            return BadRequest("Trạng thái xử lý không hợp lệ.");
        }

        if (string.Equals(req.Status, "CLOSED", StringComparison.OrdinalIgnoreCase) && status != "CLOSED")
        {
            return BadRequest("Phiếu hỗ trợ này đã đóng (CLOSED) nên không thể mở lại. Vui lòng tạo phiếu hỗ trợ mới nếu phát sinh yêu cầu khác.");
        }

        string resolutionNote = (input.ResolutionNote ?? string.Empty).Trim();
        if (resolutionNote.Length < 4)
        {
            return BadRequest("Ghi chú xử lý cần ít nhất 4 ký tự.");
        }

        req.Status = status;
        req.ResolutionNote = resolutionNote;
        req.UpdatedBy = CallerId;
        req.UpdatedAt = DateTime.UtcNow;

        string statusLabel = status switch
        {
            "OPEN" => "Mới tiếp nhận",
            "IN_PROGRESS" => "Đang xử lý",
            "RESOLVED" => "Đã giải quyết",
            "CLOSED" => "Đã đóng phiếu",
            _ => "Đã giải quyết"
        };

        AddAuditLog(
            "UPDATE_SUPPORT_REQUEST",
            "SupportRequest",
            req.Id,
            $"Cập nhật phiếu hỗ trợ {req.Id} sang trạng thái \"{statusLabel}\": {resolutionNote}");

        _db.Notifications.Add(new Notification
        {
            RecipientId = req.MemberId,
            Type = "SUPPORT_REQUEST_UPDATED",
            Title = $"Cập nhật yêu cầu hỗ trợ: {req.Subject}",
            Body = $"Phiếu hỗ trợ {req.Id} đã chuyển sang trạng thái \"{statusLabel}\". Phản hồi từ lễ tân: {resolutionNote}",
            EntityType = "SupportRequest",
            EntityId = req.Id,
            EventAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _db.SaveChangesAsync();
        return Ok(new { id = req.Id, status = req.Status });
    }

    // =====================================================================
    // CỤM 2: MỤC TIÊU, KẾ HOẠCH TẬP LUYỆN, BÀI TẬP VỀ NHÀ & KẾT QUẢ / NHẬN XÉT
    // =====================================================================

    [HttpGet("coach/workspace")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> GetCoachTrainingWorkspaceAsync()
    {
        string coachId = CallerId;
        bool isManager = User.IsInRole("CenterManager");
        DateOnly today = TodayVietnam();

        var sessionsQuery = _db.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Room)
            .AsQueryable();

        if (!isManager)
        {
            sessionsQuery = sessionsQuery.Where(s => s.CoachId == coachId);
        }

        var sessions = await sessionsQuery
            .OrderByDescending(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .Take(100)
            .ToListAsync();

        var classIds = sessions.Select(s => s.ClassId).Distinct().ToList();
        var classes = await _db.Classes
            .AsNoTracking()
            .Include(c => c.Discipline)
            .Where(c => isManager || classIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync();

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var bookedRegs = await _db.ClassRegistrations
            .AsNoTracking()
            .Where(r => (isManager || sessionIds.Contains(r.SessionId)) && r.Status == "BOOKED")
            .ToListAsync();

        var attendances = await _db.ClassSessionAttendances
            .AsNoTracking()
            .Where(a => isManager || sessionIds.Contains(a.SessionId))
            .ToListAsync();

        var memberIds = bookedRegs.Select(r => r.MemberId).Distinct().ToList();
        var members = await _db.Members
            .AsNoTracking()
            .Include(m => m.Account)
            .Where(m => m.Account.DeletedAt == null && (isManager || memberIds.Contains(m.AccountId)))
            .OrderBy(m => m.FullName)
            .ToListAsync();

        var allMemberIds = members.Select(m => m.AccountId).ToList();
        var profiles = await _db.MemberTrainingProfiles
            .AsNoTracking()
            .Where(p => allMemberIds.Contains(p.MemberId))
            .ToListAsync();

        var plans = await _db.TrainingPlans
            .AsNoTracking()
            .Where(p => isManager || p.CoachId == coachId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var results = await _db.TrainingResults
            .AsNoTracking()
            .Where(r => isManager || r.CoachId == coachId)
            .OrderByDescending(r => r.RecordedDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        var classMap = classes.ToDictionary(c => c.Id, c => c.Name);
        var sessionClassMap = sessions.ToDictionary(s => s.Id, s => s.ClassId);
        var memberMap = members.ToDictionary(m => m.AccountId, m => m.FullName ?? m.MemberCode);
        DateTime vnNow = NowVietnam();

        return Ok(new
        {
            classes = classes.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                sportName = c.Discipline?.Name ?? "Bộ môn"
            }),
            sessions = sessions.Select(s =>
            {
                DateOnly sDay = DateOnly.FromDateTime(s.SessionDate);
                bool canEvaluateOrAttend = (sDay < today || (sDay == today && vnNow.TimeOfDay >= s.StartTime - TimeSpan.FromMinutes(15)))
                    && !string.Equals(s.Status, "CANCELED", StringComparison.OrdinalIgnoreCase);
                return new
                {
                    id = s.Id,
                    classId = s.ClassId,
                    className = s.Class.Name,
                    sessionDate = s.SessionDate.ToString("yyyy-MM-dd"),
                    startTime = s.StartTime.ToString(@"hh\:mm"),
                    endTime = s.EndTime.ToString(@"hh\:mm"),
                    roomName = s.Room?.Name ?? "Phòng tập",
                    status = s.Status,
                    isPastOrToday = canEvaluateOrAttend,
                    bookedMemberIds = bookedRegs.Where(r => r.SessionId == s.Id).Select(r => r.MemberId).Distinct().ToList()
                };
            }),
            students = members.Select(m =>
            {
                var prof = profiles.FirstOrDefault(p => p.MemberId == m.AccountId);
                var memberRegs = bookedRegs.Where(r => r.MemberId == m.AccountId).ToList();
                var enrolledClassIds = memberRegs
                    .Select(r => sessionClassMap.GetValueOrDefault(r.SessionId))
                    .Where(cid => cid > 0)
                    .Distinct()
                    .ToList();
                var enrolledClassNames = enrolledClassIds
                    .Select(cid => classMap.GetValueOrDefault(cid, $"Lớp #{cid}"))
                    .Distinct()
                    .ToList();
                int attendedCount = attendances.Count(a =>
                    a.MemberId == m.AccountId &&
                    (a.Status == "PRESENT" || a.Status == "LATE"));

                return new
                {
                    memberId = m.AccountId,
                    memberCode = m.MemberCode,
                    fullName = m.FullName ?? m.MemberCode,
                    email = m.Account.Email,
                    phone = m.Account.Phone,
                    fitnessGoal = prof?.FitnessGoal ?? "Nâng cao thể lực & sức bền nền tảng",
                    fitnessLevel = prof?.FitnessLevel ?? "BEGINNER",
                    currentWeightKg = prof?.CurrentWeightKg,
                    targetWeightKg = prof?.TargetWeightKg,
                    notes = prof?.Notes,
                    enrolledClassIds,
                    enrolledClassNames,
                    attendedCount,
                    totalRegisteredSessions = memberRegs.Count
                };
            }),
            plans = plans.Select(p => new
            {
                id = p.Id,
                planType = p.PlanType,
                memberId = p.MemberId,
                memberName = p.MemberId != null ? memberMap.GetValueOrDefault(p.MemberId, p.MemberId) : null,
                classId = p.ClassId,
                className = p.ClassId.HasValue ? classMap.GetValueOrDefault(p.ClassId.Value, $"Lớp #{p.ClassId}") : null,
                title = p.Title,
                goal = p.Goal,
                exercises = ParseExercises(p.ExercisesJson),
                homework = p.Homework,
                startDate = p.StartDate.ToString("yyyy-MM-dd"),
                endDate = p.EndDate.ToString("yyyy-MM-dd"),
                createdAt = DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc).ToString("O")
            }),
            results = results.Select(r => new
            {
                id = r.Id,
                memberId = r.MemberId,
                memberName = memberMap.GetValueOrDefault(r.MemberId, r.MemberId),
                sessionId = r.SessionId,
                planId = r.PlanId,
                recordedDate = r.RecordedDate.ToString("yyyy-MM-dd"),
                completedExercises = r.CompletedExercises,
                completionRate = r.CompletionRate,
                progressRating = r.ProgressRating,
                resultSummary = r.ResultSummary,
                coachFeedback = r.CoachFeedback,
                homeworkAssigned = r.HomeworkAssigned,
                createdAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc).ToString("O")
            })
        });
    }

    [HttpPut("students/{memberId}/profile")]
    [Authorize(Roles = "Coach,Member,CenterManager")]
    public async Task<IActionResult> UpsertMemberTrainingProfileAsync(
        string memberId,
        [FromBody] UpsertTrainingProfileRequest input)
    {
        if (User.IsInRole("Member") && !string.Equals(CallerId, memberId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        var (member, errorMsg, isForbidden) = await ValidateMemberAndCoachScopeAsync(
            memberId,
            checkCoachScope: User.IsInRole("Coach"));
        if (isForbidden)
        {
            return Forbid();
        }
        if (errorMsg is not null || member is null)
        {
            return BadRequest(errorMsg);
        }

        string goal = (input.FitnessGoal ?? string.Empty).Trim();
        string level = (input.FitnessLevel ?? "BEGINNER").Trim().ToUpperInvariant();
        if (goal.Length < 4)
        {
            return BadRequest("Mục tiêu tập luyện cần ít nhất 4 ký tự.");
        }
        if (level is not ("BEGINNER" or "INTERMEDIATE" or "ADVANCED"))
        {
            return BadRequest("Trình độ tập luyện phải là BEGINNER, INTERMEDIATE hoặc ADVANCED.");
        }
        if (input.CurrentWeightKg is < 20 or > 250 || input.TargetWeightKg is < 20 or > 250)
        {
            return BadRequest("Cân nặng phải nằm trong khoảng hợp lý từ 20 kg đến 250 kg.");
        }

        var profile = await _db.MemberTrainingProfiles.FirstOrDefaultAsync(p => p.MemberId == memberId);
        if (profile is null)
        {
            profile = new MemberTrainingProfile
            {
                MemberId = memberId,
                FitnessGoal = goal,
                FitnessLevel = level,
                CurrentWeightKg = input.CurrentWeightKg,
                TargetWeightKg = input.TargetWeightKg,
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                UpdatedAt = DateTime.UtcNow
            };
            _db.MemberTrainingProfiles.Add(profile);
        }
        else
        {
            profile.FitnessGoal = goal;
            profile.FitnessLevel = level;
            profile.CurrentWeightKg = input.CurrentWeightKg;
            profile.TargetWeightKg = input.TargetWeightKg;
            profile.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
            profile.UpdatedAt = DateTime.UtcNow;
        }

        AddAuditLog(
            "UPDATE_STUDENT_GOAL",
            "MemberTrainingProfile",
            memberId,
            $"Cập nhật mục tiêu tập luyện cho học viên {member.FullName ?? member.MemberCode} ({level}): {goal}");

        await _db.SaveChangesAsync();
        return Ok(profile);
    }

    [HttpPost("coach/plans")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> CreateTrainingPlanAsync([FromBody] CreateTrainingPlanRequest input)
    {
        string planType = (input.PlanType ?? "INDIVIDUAL").Trim().ToUpperInvariant();
        string title = (input.Title ?? string.Empty).Trim();
        string goal = (input.Goal ?? string.Empty).Trim();

        if (planType is not ("INDIVIDUAL" or "CLASS"))
        {
            return BadRequest("Loại kế hoạch phải là INDIVIDUAL hoặc CLASS.");
        }
        if (title.Length < 4 || goal.Length < 4)
        {
            return BadRequest("Tiêu đề và mục tiêu kế hoạch cần ít nhất 4 ký tự.");
        }
        if (input.Exercises is null || input.Exercises.Count == 0)
        {
            return BadRequest("Kế hoạch cần ít nhất 1 bài tập.");
        }
        if (!DateOnly.TryParse(input.StartDate, out DateOnly startDate) ||
            !DateOnly.TryParse(input.EndDate, out DateOnly endDate) ||
            endDate < startDate)
        {
            return BadRequest("Ngày kết thúc kế hoạch phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        string coachId = await ResolveEffectiveCoachIdAsync(
            classId: planType == "CLASS" ? input.ClassId : null,
            memberId: planType == "INDIVIDUAL" ? input.MemberId : null);

        if (planType == "INDIVIDUAL")
        {
            if (string.IsNullOrWhiteSpace(input.MemberId))
            {
                return BadRequest("Vui lòng chọn học viên cho kế hoạch cá nhân.");
            }

            var (_, errorMsg, isForbidden) = await ValidateMemberAndCoachScopeAsync(
                input.MemberId.Trim(),
                checkCoachScope: true);
            if (isForbidden)
            {
                return Forbid();
            }
            if (errorMsg is not null)
            {
                return BadRequest(errorMsg);
            }
        }
        else if (planType == "CLASS")
        {
            if (!input.ClassId.HasValue)
            {
                return BadRequest("Vui lòng chọn lớp học cho kế hoạch cả lớp.");
            }

            if (User.IsInRole("Coach") && !User.IsInRole("CenterManager"))
            {
                bool teachesClass = await _db.ClassSessions
                    .AnyAsync(s => s.ClassId == input.ClassId.Value && s.CoachId == coachId);
                if (!teachesClass)
                {
                    return BadRequest("Bạn chỉ được tạo kế hoạch tập luyện cho các lớp mình phụ trách giảng dạy.");
                }
            }
        }

        var plan = new TrainingPlan
        {
            CoachId = coachId,
            PlanType = planType,
            MemberId = planType == "INDIVIDUAL" ? input.MemberId!.Trim() : null,
            ClassId = planType == "CLASS" ? input.ClassId : null,
            Title = title,
            Goal = goal,
            ExercisesJson = JsonSerializer.Serialize(input.Exercises),
            Homework = string.IsNullOrWhiteSpace(input.Homework) ? null : input.Homework.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.TrainingPlans.Add(plan);
        await _db.SaveChangesAsync();

        AddAuditLog(
            "CREATE_TRAINING_PLAN",
            "TrainingPlan",
            plan.Id.ToString(),
            $"Tạo giáo án tập luyện [{planType}] \"{plan.Title}\" ({plan.StartDate:dd/MM/yyyy} – {plan.EndDate:dd/MM/yyyy}).");

        List<string> recipients = new();
        if (planType == "INDIVIDUAL" && plan.MemberId is not null)
        {
            recipients.Add(plan.MemberId);
        }
        else if (planType == "CLASS" && plan.ClassId.HasValue)
        {
            recipients = await _db.ClassRegistrations
                .Where(r => r.Session.ClassId == plan.ClassId.Value && r.Status == "BOOKED")
                .Select(r => r.MemberId)
                .Distinct()
                .ToListAsync();
        }

        foreach (string recipientId in recipients)
        {
            _db.Notifications.Add(new Notification
            {
                RecipientId = recipientId,
                Type = "TRAINING_PLAN_CREATED",
                Title = $"Kế hoạch tập luyện mới: {plan.Title}",
                Body = $"HLV đã giao kế hoạch tập luyện ({plan.StartDate:dd/MM/yyyy} – {plan.EndDate:dd/MM/yyyy}). Mục tiêu: {plan.Goal}."
                    + (!string.IsNullOrWhiteSpace(plan.Homework) ? $" Bài tập về nhà: {plan.Homework}" : string.Empty),
                EntityType = "TrainingPlan",
                EntityId = plan.Id.ToString(),
                EventAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { id = plan.Id });
    }

    [HttpPost("coach/results")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> CreateTrainingResultAsync([FromBody] CreateTrainingResultRequest input)
    {
        string memberId = (input.MemberId ?? string.Empty).Trim();
        string completedExercises = (input.CompletedExercises ?? string.Empty).Trim();
        string resultSummary = (input.ResultSummary ?? string.Empty).Trim();
        string coachFeedback = (input.CoachFeedback ?? string.Empty).Trim();
        string rating = (input.ProgressRating ?? "ON_TRACK").Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(memberId) || completedExercises.Length < 3 || resultSummary.Length < 4 || coachFeedback.Length < 4)
        {
            return BadRequest("Vui lòng nhập đầy đủ bài tập đã hoàn thành, kết quả buổi tập và nhận xét đánh giá.");
        }
        if (input.CompletionRate is < 0 or > 100)
        {
            return BadRequest("Mức độ hoàn thành phải từ 0% đến 100%.");
        }
        if (rating is not ("EXCEEDED" or "ON_TRACK" or "NEEDS_IMPROVEMENT"))
        {
            return BadRequest("Đánh giá tiến độ không hợp lệ.");
        }

        var (member, errorMsg, isForbidden) = await ValidateMemberAndCoachScopeAsync(
            memberId,
            checkCoachScope: true);
        if (isForbidden)
        {
            return Forbid();
        }
        if (errorMsg is not null || member is null)
        {
            return BadRequest(errorMsg);
        }

        DateTime vnNow = NowVietnam();
        DateOnly today = DateOnly.FromDateTime(vnNow);
        DateOnly recordedDate = DateOnly.TryParse(input.RecordedDate, out DateOnly parsedDate)
            ? parsedDate
            : today;

        if (recordedDate > today)
        {
            return BadRequest($"Không thể ghi nhận kết quả tập luyện cho ngày trong tương lai ({recordedDate:dd/MM/yyyy}).");
        }

        int? sessionClassId = null;
        if (input.SessionId.HasValue)
        {
            var session = await _db.ClassSessions
                .AsNoTracking()
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == input.SessionId.Value);
            if (session is null)
            {
                return NotFound("Không tìm thấy buổi học liên quan.");
            }
            if (User.IsInRole("Coach") && !User.IsInRole("CenterManager") && !string.Equals(session.CoachId, CallerId, StringComparison.Ordinal))
            {
                return Forbid();
            }

            DateOnly sessionDay = DateOnly.FromDateTime(session.SessionDate);
            if (sessionDay > today)
            {
                return BadRequest("Không thể ghi nhận kết quả cho buổi học chưa diễn ra.");
            }
            if (sessionDay == today && vnNow.TimeOfDay < session.StartTime - TimeSpan.FromMinutes(15))
            {
                return BadRequest($"Ca học bắt đầu lúc {session.StartTime:hh\\:mm} hôm nay. Chỉ được chấm kết quả sau khi ca học đã bắt đầu.");
            }
            if (string.Equals(session.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Không thể ghi nhận kết quả cho buổi học đã bị hủy.");
            }

            var registration = await _db.ClassRegistrations
                .FirstOrDefaultAsync(r => r.SessionId == input.SessionId.Value && r.MemberId == memberId && r.Status == "BOOKED");
            if (registration is null)
            {
                return BadRequest("Học viên không nằm trong danh sách đăng ký (BOOKED) của buổi học đã chọn.");
            }

            var sessionAttendance = await _db.ClassSessionAttendances
                .FirstOrDefaultAsync(a => a.RegistrationId == registration.Id);
            if (sessionAttendance is not null && string.Equals(sessionAttendance.Status, "ABSENT", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Học viên đang được ghi nhận VẮNG MẶT trong buổi học này nên không thể chấm kết quả tập luyện. Vui lòng cập nhật lại trạng thái điểm danh nếu học viên thực tế có mặt.");
            }

            if (sessionAttendance is null)
            {
                _db.ClassSessionAttendances.Add(new ClassSessionAttendance
                {
                    RegistrationId = registration.Id,
                    SessionId = input.SessionId.Value,
                    MemberId = memberId,
                    Status = "PRESENT",
                    Note = "Tự động đồng bộ Có mặt khi HLV ghi nhận kết quả buổi tập",
                    RecordedBy = CallerId,
                    RecordedAt = DateTime.UtcNow
                });
            }

            if (sessionDay == today)
            {
                await EnsureTodayCenterVisitForAttendedStudentAsync(memberId, session.Class?.Name ?? $"Buổi #{session.Id}", today);
            }

            recordedDate = sessionDay;
            sessionClassId = session.ClassId;
        }

        if (input.PlanId.HasValue)
        {
            var plan = await _db.TrainingPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == input.PlanId.Value);
            if (plan is null)
            {
                return BadRequest("Không tìm thấy giáo án tập luyện đã chọn.");
            }

            bool validPlanScope = string.Equals(plan.MemberId, memberId, StringComparison.Ordinal);
            if (!validPlanScope && plan.PlanType == "CLASS" && plan.ClassId.HasValue)
            {
                validPlanScope = (sessionClassId.HasValue && sessionClassId.Value == plan.ClassId.Value)
                    || await _db.ClassRegistrations.AsNoTracking().AnyAsync(r =>
                        r.MemberId == memberId &&
                        r.Status == "BOOKED" &&
                        r.Session.ClassId == plan.ClassId.Value);
            }

            if (!validPlanScope)
            {
                return BadRequest("Giáo án tập luyện đã chọn không thuộc về học viên hoặc lớp học của học viên này.");
            }
        }

        string coachId = await ResolveEffectiveCoachIdAsync(
            classId: sessionClassId,
            sessionId: input.SessionId,
            memberId: memberId);

        TrainingResult? result = null;
        if (input.SessionId.HasValue)
        {
            result = await _db.TrainingResults
                .FirstOrDefaultAsync(r => r.MemberId == memberId && r.SessionId == input.SessionId.Value);
        }

        if (result is null)
        {
            result = new TrainingResult
            {
                CoachId = coachId,
                MemberId = memberId,
                SessionId = input.SessionId,
                PlanId = input.PlanId,
                RecordedDate = recordedDate,
                CompletedExercises = completedExercises,
                CompletionRate = input.CompletionRate,
                ProgressRating = rating,
                ResultSummary = resultSummary,
                CoachFeedback = coachFeedback,
                HomeworkAssigned = string.IsNullOrWhiteSpace(input.HomeworkAssigned) ? null : input.HomeworkAssigned.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.TrainingResults.Add(result);
        }
        else
        {
            result.CoachId = coachId;
            result.PlanId = input.PlanId;
            result.RecordedDate = recordedDate;
            result.CompletedExercises = completedExercises;
            result.CompletionRate = input.CompletionRate;
            result.ProgressRating = rating;
            result.ResultSummary = resultSummary;
            result.CoachFeedback = coachFeedback;
            result.HomeworkAssigned = string.IsNullOrWhiteSpace(input.HomeworkAssigned) ? null : input.HomeworkAssigned.Trim();
            result.CreatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        string ratingLabel = rating switch
        {
            "EXCEEDED" => "Vượt mục tiêu",
            "ON_TRACK" => "Đạt tiến độ",
            _ => "Cần cố gắng thêm"
        };

        AddAuditLog(
            "CREATE_TRAINING_RESULT",
            "TrainingResult",
            result.Id.ToString(),
            $"Ghi nhận kết quả buổi tập ngày {recordedDate:dd/MM/yyyy} cho học viên {member.FullName ?? member.MemberCode} ({input.CompletionRate}% - {ratingLabel}).");

        _db.Notifications.Add(new Notification
        {
            RecipientId = memberId,
            Type = "TRAINING_RESULT_RECORDED",
            Title = $"Đánh giá buổi tập ngày {recordedDate:dd/MM/yyyy}: {ratingLabel} ({input.CompletionRate}%)",
            Body = $"Nhận xét từ HLV: {coachFeedback}"
                + (!string.IsNullOrWhiteSpace(result.HomeworkAssigned) ? $" · Bài tập về nhà: {result.HomeworkAssigned}" : string.Empty),
            EntityType = "TrainingResult",
            EntityId = memberId,
            EventAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _db.SaveChangesAsync();
        return Ok(new { id = result.Id });
    }

    [HttpPost("coach/homework-notice")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> SendCoachHomeworkOrNoticeAsync([FromBody] SendCoachNoticeRequest input)
    {
        string title = (input.Title ?? string.Empty).Trim();
        string body = (input.Body ?? string.Empty).Trim();
        if (title.Length < 4 || body.Length < 6)
        {
            return BadRequest("Tiêu đề (tối thiểu 4 ký tự) và nội dung thông báo/bài tập (tối thiểu 6 ký tự) là bắt buộc.");
        }

        List<string> recipients = new();
        if (!string.IsNullOrWhiteSpace(input.MemberId))
        {
            string targetMemberId = input.MemberId.Trim();
            var (_, errorMsg, isForbidden) = await ValidateMemberAndCoachScopeAsync(
                targetMemberId,
                checkCoachScope: true);
            if (isForbidden)
            {
                return Forbid();
            }
            if (errorMsg is not null)
            {
                return BadRequest(errorMsg);
            }
            recipients.Add(targetMemberId);
        }
        else if (input.ClassId.HasValue)
        {
            if (User.IsInRole("Coach") && !User.IsInRole("CenterManager"))
            {
                bool teachesClass = await _db.ClassSessions
                    .AnyAsync(s => s.ClassId == input.ClassId.Value && s.CoachId == CallerId);
                if (!teachesClass)
                {
                    return BadRequest("Bạn chỉ được gửi bài tập hoặc thông báo cho các lớp mình phụ trách giảng dạy.");
                }
            }

            recipients = await _db.ClassRegistrations
                .Where(r => r.Session.ClassId == input.ClassId.Value && r.Status == "BOOKED")
                .Select(r => r.MemberId)
                .Distinct()
                .ToListAsync();
        }

        if (recipients.Count == 0)
        {
            return BadRequest("Lớp học hoặc đối tượng đã chọn hiện chưa có học viên đăng ký (BOOKED) để nhận thông báo.");
        }

        DateTime now = DateTime.UtcNow;
        foreach (string recipientId in recipients)
        {
            _db.Notifications.Add(new Notification
            {
                RecipientId = recipientId,
                Type = "COACH_HOMEWORK_NOTICE",
                Title = title,
                Body = body,
                EntityType = "CoachNotice",
                EntityId = CallerId,
                EventAt = now,
                CreatedAt = now,
                IsRead = false
            });
        }

        AddAuditLog(
            "SEND_HOMEWORK_NOTICE",
            "CoachNotice",
            CallerId,
            $"Gửi bài tập về nhà / thông báo \"{title}\" tới {recipients.Count} học viên.");

        await _db.SaveChangesAsync();
        return Ok(new { sentCount = recipients.Count });
    }

    [HttpGet("member/workspace")]
    [Authorize(Roles = "Member,CenterManager")]
    public async Task<IActionResult> GetMemberTrainingWorkspaceAsync([FromQuery] string? memberId = null)
    {
        string targetMemberId;
        if (User.IsInRole("Member"))
        {
            targetMemberId = CallerId;
        }
        else if (!string.IsNullOrWhiteSpace(memberId))
        {
            targetMemberId = memberId.Trim();
        }
        else
        {
            targetMemberId = await _db.Members
                .AsNoTracking()
                .Include(m => m.Account)
                .Where(m => m.Account.DeletedAt == null && m.Account.Status == "Active")
                .OrderBy(m => m.AccountId)
                .Select(m => m.AccountId)
                .FirstOrDefaultAsync() ?? "acc-mem-001";
        }

        memberId = targetMemberId;

        var profile = await _db.MemberTrainingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId);

        var bookedClassIds = await _db.ClassRegistrations
            .AsNoTracking()
            .Where(r => r.MemberId == memberId && r.Status == "BOOKED")
            .Select(r => r.Session.ClassId)
            .Distinct()
            .ToListAsync();

        var plans = await _db.TrainingPlans
            .AsNoTracking()
            .Where(p => p.MemberId == memberId || (p.PlanType == "CLASS" && p.ClassId.HasValue && bookedClassIds.Contains(p.ClassId.Value)))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var results = await _db.TrainingResults
            .AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.RecordedDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        var classAttendances = await _db.ClassSessionAttendances
            .AsNoTracking()
            .Where(a => a.MemberId == memberId)
            .OrderByDescending(a => a.RecordedAt)
            .ToListAsync();

        var sessionIds = classAttendances.Select(a => a.SessionId).Distinct().ToList();
        var sessions = await _db.ClassSessions
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Room)
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var centerVisits = await _db.CenterVisits
            .AsNoTracking()
            .Where(v => v.MemberId == memberId)
            .OrderByDescending(v => v.CheckedInAt)
            .Take(50)
            .ToListAsync();

        var coachIds = plans.Select(p => p.CoachId)
            .Concat(results.Select(r => r.CoachId))
            .Distinct()
            .ToList();
        var coachMap = await _db.Coaches
            .AsNoTracking()
            .Where(c => coachIds.Contains(c.AccountId))
            .ToDictionaryAsync(c => c.AccountId, c => c.FullName ?? c.AccountId);

        var classMap = await _db.Classes
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        return Ok(new
        {
            profile = new
            {
                memberId,
                fitnessGoal = profile?.FitnessGoal ?? "Nâng cao thể lực, cải thiện vóc dáng và sức bền",
                fitnessLevel = profile?.FitnessLevel ?? "BEGINNER",
                currentWeightKg = profile?.CurrentWeightKg,
                targetWeightKg = profile?.TargetWeightKg,
                notes = profile?.Notes
            },
            plans = plans.Select(p => new
            {
                id = p.Id,
                planType = p.PlanType,
                coachName = coachMap.GetValueOrDefault(p.CoachId, "Huấn luyện viên"),
                className = p.ClassId.HasValue ? classMap.GetValueOrDefault(p.ClassId.Value, $"Lớp #{p.ClassId}") : null,
                title = p.Title,
                goal = p.Goal,
                exercises = ParseExercises(p.ExercisesJson),
                homework = p.Homework,
                startDate = p.StartDate.ToString("yyyy-MM-dd"),
                endDate = p.EndDate.ToString("yyyy-MM-dd"),
                createdAt = DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc).ToString("O")
            }),
            results = results.Select(r => new
            {
                id = r.Id,
                coachName = coachMap.GetValueOrDefault(r.CoachId, "Huấn luyện viên"),
                recordedDate = r.RecordedDate.ToString("yyyy-MM-dd"),
                completedExercises = r.CompletedExercises,
                completionRate = r.CompletionRate,
                progressRating = r.ProgressRating,
                resultSummary = r.ResultSummary,
                coachFeedback = r.CoachFeedback,
                homeworkAssigned = r.HomeworkAssigned,
                createdAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc).ToString("O")
            }),
            classAttendanceHistory = classAttendances.Select(a =>
            {
                sessions.TryGetValue(a.SessionId, out var sess);
                return new
                {
                    id = a.Id,
                    sessionId = a.SessionId,
                    className = sess?.Class.Name ?? $"Buổi học #{a.SessionId}",
                    sessionDate = sess?.SessionDate.ToString("yyyy-MM-dd") ?? a.RecordedAt.ToString("yyyy-MM-dd"),
                    timeRange = sess != null ? $"{sess.StartTime:hh\\:mm} - {sess.EndTime:hh\\:mm}" : "",
                    roomName = sess?.Room?.Name ?? "",
                    status = a.Status,
                    note = a.Note,
                    recordedAt = DateTime.SpecifyKind(a.RecordedAt, DateTimeKind.Utc).ToString("O")
                };
            }),
            centerVisitHistory = centerVisits.Select(v => new
            {
                id = v.Id,
                visitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                checkedInAt = DateTime.SpecifyKind(v.CheckedInAt, DateTimeKind.Utc).ToString("O"),
                checkedOutAt = v.CheckedOutAt.HasValue
                    ? DateTime.SpecifyKind(v.CheckedOutAt.Value, DateTimeKind.Utc).ToString("O")
                    : null,
                note = v.Note
            })
        });
    }

    // =====================================================================
    // CỤM 3: QUẢN TRỊ PHÂN QUYỀN ĐỘNG (CENTER MANAGER)
    // =====================================================================

    [HttpGet("permissions")]
    public async Task<IActionResult> GetRolePermissionsAsync()
    {
        var configs = await _db.RolePermissionConfigs
            .AsNoTracking()
            .ToListAsync();

        var result = configs.ToDictionary(
            c => c.FeatureKey,
            c => c.AllowedRolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return Ok(result);
    }

    [HttpPut("permissions")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateRolePermissionAsync([FromBody] UpdateRolePermissionRequest input)
    {
        string featureKey = (input.FeatureKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(featureKey) || input.AllowedRoles is null || input.AllowedRoles.Count == 0)
        {
            return BadRequest("Chức năng phải có ít nhất 1 vai trò được cấp quyền.");
        }

        var normalizedRoles = input.AllowedRoles
            .Select(r => r.Trim().ToUpperInvariant())
            .Where(r => r is "CENTER_MANAGER" or "RECEPTIONIST" or "COACH" or "MEMBER")
            .Distinct()
            .ToList();

        // The guard above counts the raw input, so ["FOO"] slipped through and
        // was stored as an empty role list — a feature nobody may open.
        if (normalizedRoles.Count == 0)
        {
            return BadRequest("Chức năng phải có ít nhất 1 vai trò hợp lệ: CENTER_MANAGER, RECEPTIONIST, COACH hoặc MEMBER.");
        }

        if (featureKey == "permissions" && !normalizedRoles.Contains("CENTER_MANAGER"))
        {
            normalizedRoles.Add("CENTER_MANAGER");
        }

        string csv = string.Join(",", normalizedRoles);
        var existing = await _db.RolePermissionConfigs.FirstOrDefaultAsync(c => c.FeatureKey == featureKey);
        if (existing is null)
        {
            _db.RolePermissionConfigs.Add(new RolePermissionConfig
            {
                FeatureKey = featureKey,
                AllowedRolesCsv = csv,
                UpdatedBy = CallerId,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.AllowedRolesCsv = csv;
            existing.UpdatedBy = CallerId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        AddAuditLog(
            "UPDATE_PERMISSION",
            "RolePermissionConfig",
            featureKey,
            $"Cập nhật phân quyền [{featureKey}] cho các vai trò: {csv}");

        await _db.SaveChangesAsync();
        return Ok(new { featureKey, allowedRoles = normalizedRoles });
    }

    private static List<TrainingExerciseItemDto> ParseExercises(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<TrainingExerciseItemDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<TrainingExerciseItemDto>();
        }
        catch
        {
            return new List<TrainingExerciseItemDto>();
        }
    }

    private async Task<Dictionary<string, string>> ResolveActorNamesAsync(List<string> accountIds)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (accountIds.Count == 0) return result;

        var recs = await _db.Receptionists
            .AsNoTracking()
            .Where(r => accountIds.Contains(r.AccountId))
            .ToListAsync();
        foreach (var r in recs) result[r.AccountId] = r.FullName ?? r.AccountId;

        var mgrs = await _db.CenterManagers
            .AsNoTracking()
            .Where(m => accountIds.Contains(m.AccountId))
            .ToListAsync();
        foreach (var m in mgrs) result[m.AccountId] = m.FullName ?? m.AccountId;

        return result;
    }
}

public class RecordClassAttendanceRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class CheckInMemberRequest
{
    public string MemberId { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class UpdateSubscriptionSuspensionRequest
{
    public bool IsSuspended { get; set; }
    public string? Reason { get; set; }
}

public class CreateSupportRequestDto
{
    public string MemberId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public class UpdateSupportRequestDto
{
    public string Status { get; set; } = string.Empty;
    public string ResolutionNote { get; set; } = string.Empty;
}

public class UpsertTrainingProfileRequest
{
    public string FitnessGoal { get; set; } = string.Empty;
    public string FitnessLevel { get; set; } = "BEGINNER";
    public decimal? CurrentWeightKg { get; set; }
    public decimal? TargetWeightKg { get; set; }
    public string? Notes { get; set; }
}

public class TrainingExerciseItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Sets { get; set; }
    public string Reps { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Note { get; set; }
}

public class CreateTrainingPlanRequest
{
    public string PlanType { get; set; } = "INDIVIDUAL";
    public string? MemberId { get; set; }
    public int? ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public List<TrainingExerciseItemDto> Exercises { get; set; } = new();
    public string? Homework { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class CreateTrainingResultRequest
{
    public string MemberId { get; set; } = string.Empty;
    public int? SessionId { get; set; }
    public int? PlanId { get; set; }
    public string? RecordedDate { get; set; }
    public string CompletedExercises { get; set; } = string.Empty;
    public int CompletionRate { get; set; }
    public string ProgressRating { get; set; } = "ON_TRACK";
    public string ResultSummary { get; set; } = string.Empty;
    public string CoachFeedback { get; set; } = string.Empty;
    public string? HomeworkAssigned { get; set; }
}

public class SendCoachNoticeRequest
{
    public string? MemberId { get; set; }
    public int? ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class UpdateRolePermissionRequest
{
    public string FeatureKey { get; set; } = string.Empty;
    public List<string> AllowedRoles { get; set; } = new();
}
