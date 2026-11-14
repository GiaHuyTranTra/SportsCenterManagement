using APIViewModel.Homework;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace Services.HomeworkAssignmentService;

public sealed class HomeworkAssignmentService : IHomeworkAssignmentService
{
    private readonly SportsCenterManagementContext _context;
    private readonly TimeZoneInfo _businessTimeZone;

    public HomeworkAssignmentService(
        SportsCenterManagementContext context,
        IConfiguration? configuration = null)
    {
        _context = context;
        string timeZoneId = configuration?["BusinessSettings:TimeZoneId"]
            ?? "SE Asia Standard Time";
        try
        {
            _businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }

    public async Task<(AssignHomeworkResult Result, IReadOnlyList<HomeworkAssignmentAPIViewModel> Items)> AssignAsync(
        string coachId,
        CreateHomeworkAssignmentAPIViewModel request)
    {
        string targetType = (request.TargetType ?? string.Empty).Trim().ToUpperInvariant();
        if (!IsValidRequest(targetType, request))
        {
            return (AssignHomeworkResult.InvalidRequest, []);
        }

        (AssignHomeworkResult result, IReadOnlyList<Recipient> recipients) =
            await ResolveRecipientsAsync(coachId, targetType, request);
        if (result != AssignHomeworkResult.Success)
        {
            return (result, []);
        }

        DateTime assignedAt = DateTime.UtcNow;
        List<HomeworkAssignment> assignments = recipients
            .OrderBy(item => item.MemberId, StringComparer.Ordinal)
            .Select(item => new HomeworkAssignment
            {
                CoachId = coachId,
                MemberId = item.MemberId,
                ClassId = item.ClassId,
                SessionId = item.SessionId,
                Title = request.Title.Trim(),
                Instructions = request.Instructions.Trim(),
                SourceType = targetType == "MEMBER" ? "DIRECT" : targetType,
                AssignedAtUtc = assignedAt
            })
            .ToList();

        IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            _context.HomeworkAssignments.AddRange(assignments);
            await _context.SaveChangesAsync();

            foreach (HomeworkAssignment assignment in assignments)
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientId = assignment.MemberId,
                    Type = "COACH_HOMEWORK_NOTICE",
                    Title = assignment.Title,
                    Body = NotificationPreview(assignment.Instructions),
                    EntityType = "HomeworkAssignment",
                    EntityId = assignment.Id.ToString(),
                    EventAt = assignedAt,
                    CreatedAt = assignedAt,
                    IsRead = false
                });
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid().ToString(),
                AccountId = coachId,
                Action = "ASSIGN_HOMEWORK",
                EntityType = "HomeworkAssignment",
                EntityId = assignments.Count == 1 ? assignments[0].Id.ToString() : null,
                Description = $"Assigned homework to target {targetType}; recipients: {assignments.Count}.",
                ClassId = request.ClassId,
                SessionId = request.SessionId,
                CreatedAt = assignedAt
            });
            await _context.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }

        return (AssignHomeworkResult.Success, await MapAsync(assignments));
    }

    public async Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> GetMemberHistoryAsync(string memberId)
    {
        List<HomeworkAssignment> assignments = await _context.HomeworkAssignments
            .AsNoTracking()
            .Where(item => item.MemberId == memberId)
            .OrderByDescending(item => item.AssignedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
        return await MapAsync(assignments);
    }

    public async Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> GetCoachHistoryAsync(string coachId)
    {
        List<HomeworkAssignment> assignments = await _context.HomeworkAssignments
            .AsNoTracking()
            .Where(item => item.CoachId == coachId)
            .OrderByDescending(item => item.AssignedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToListAsync();
        return await MapAsync(assignments);
    }

    private static bool IsValidRequest(
        string targetType,
        CreateHomeworkAssignmentAPIViewModel request)
    {
        int targetCount = (string.IsNullOrWhiteSpace(request.MemberId) ? 0 : 1)
            + (request.ClassId.HasValue ? 1 : 0)
            + (request.SessionId.HasValue ? 1 : 0);
        if (targetCount != 1
            || string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Trim().Length > 200
            || string.IsNullOrWhiteSpace(request.Instructions)
            || request.Instructions.Trim().Length > 2000)
        {
            return false;
        }

        return targetType switch
        {
            "MEMBER" => !string.IsNullOrWhiteSpace(request.MemberId),
            "CLASS" => request.ClassId.HasValue,
            "SESSION" => request.SessionId.HasValue,
            _ => false
        };
    }

    private async Task<(AssignHomeworkResult Result, IReadOnlyList<Recipient> Recipients)> ResolveRecipientsAsync(
        string coachId,
        string targetType,
        CreateHomeworkAssignmentAPIViewModel request)
    {
        if (targetType == "MEMBER")
        {
            string memberId = request.MemberId!;
            bool related = await _context.TrainingPlans.AsNoTracking()
                .AnyAsync(item => item.CoachId == coachId && item.MemberId == memberId)
                || await _context.TrainingResults.AsNoTracking()
                    .AnyAsync(item => item.CoachId == coachId && item.MemberId == memberId)
                || await _context.ClassRegistrations.AsNoTracking()
                    .AnyAsync(item => item.MemberId == memberId
                        && item.Status == "BOOKED"
                        && item.CanceledAt == null
                        && item.Session.CoachId == coachId
                        && item.Session.Status != "CANCELED");
            return related
                ? (AssignHomeworkResult.Success, [new Recipient(memberId, null, null)])
                : (AssignHomeworkResult.TargetNotFoundOrForbidden, []);
        }

        if (targetType == "CLASS")
        {
            int classId = request.ClassId!.Value;
            bool ownsTarget = await _context.ClassSessions.AsNoTracking()
                .AnyAsync(item => item.ClassId == classId
                    && item.CoachId == coachId
                    && item.Status != "CANCELED");
            if (!ownsTarget) return (AssignHomeworkResult.TargetNotFoundOrForbidden, []);

            DateOnly businessToday = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _businessTimeZone));
            DateTime today = businessToday.ToDateTime(TimeOnly.MinValue);
            List<string> memberIds = await _context.ClassRegistrations.AsNoTracking()
                .Where(item => item.Session.ClassId == classId
                    && item.Session.Status != "CANCELED"
                    && item.Session.SessionDate >= today
                    && item.Status == "BOOKED"
                    && item.CanceledAt == null)
                .Select(item => item.MemberId)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync();
            return memberIds.Count == 0
                ? (AssignHomeworkResult.TargetHasNoMembers, [])
                : (AssignHomeworkResult.Success,
                    memberIds.Select(item => new Recipient(item, classId, null)).ToList());
        }

        int sessionId = request.SessionId!.Value;
        ClassSession? session = await _context.ClassSessions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sessionId
                && item.CoachId == coachId
                && item.Status != "CANCELED");
        if (session is null) return (AssignHomeworkResult.TargetNotFoundOrForbidden, []);

        List<string> sessionMemberIds = await _context.ClassRegistrations.AsNoTracking()
            .Where(item => item.SessionId == sessionId
                && item.Status == "BOOKED"
                && item.CanceledAt == null)
            .Select(item => item.MemberId)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync();
        return sessionMemberIds.Count == 0
            ? (AssignHomeworkResult.TargetHasNoMembers, [])
            : (AssignHomeworkResult.Success,
                sessionMemberIds.Select(item => new Recipient(item, session.ClassId, sessionId)).ToList());
    }

    private async Task<IReadOnlyList<HomeworkAssignmentAPIViewModel>> MapAsync(
        IReadOnlyList<HomeworkAssignment> assignments)
    {
        string[] coachIds = assignments.Select(item => item.CoachId).Distinct().ToArray();
        string[] memberIds = assignments.Select(item => item.MemberId).Distinct().ToArray();
        int[] classIds = assignments.Where(item => item.ClassId.HasValue)
            .Select(item => item.ClassId!.Value).Distinct().ToArray();
        int[] sessionIds = assignments.Where(item => item.SessionId.HasValue)
            .Select(item => item.SessionId!.Value).Distinct().ToArray();

        Dictionary<string, string> coaches = await _context.Coaches.AsNoTracking()
            .Where(item => coachIds.Contains(item.AccountId))
            .ToDictionaryAsync(item => item.AccountId, item => item.FullName);
        Dictionary<string, string> members = await _context.Members.AsNoTracking()
            .Where(item => memberIds.Contains(item.AccountId))
            .ToDictionaryAsync(item => item.AccountId, item => item.FullName ?? item.MemberCode);
        Dictionary<int, string> classes = await _context.Classes.AsNoTracking()
            .Where(item => classIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name);
        Dictionary<int, ClassSession> sessions = await _context.ClassSessions.AsNoTracking()
            .Where(item => sessionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);

        return assignments.Select(item => new HomeworkAssignmentAPIViewModel
        {
            Id = item.Id,
            CoachId = item.CoachId,
            CoachName = coaches.GetValueOrDefault(item.CoachId, "Huấn luyện viên"),
            MemberId = item.MemberId,
            MemberName = members.GetValueOrDefault(item.MemberId, "Học viên"),
            ClassId = item.ClassId,
            ClassName = item.ClassId.HasValue ? classes.GetValueOrDefault(item.ClassId.Value) : null,
            SessionId = item.SessionId,
            SessionLabel = item.SessionId.HasValue && sessions.TryGetValue(item.SessionId.Value, out ClassSession? session)
                ? $"{session.SessionDate:yyyy-MM-dd} {session.StartTime:hh\\:mm}–{session.EndTime:hh\\:mm}"
                : null,
            Title = item.Title,
            Instructions = item.Instructions,
            SourceType = item.SourceType,
            AssignedAtUtc = DateTime.SpecifyKind(item.AssignedAtUtc, DateTimeKind.Utc)
        }).ToList();
    }

    private static string NotificationPreview(string instructions) =>
        instructions.Length <= 1000
            ? instructions
            : instructions[..997] + "...";

    private sealed record Recipient(string MemberId, int? ClassId, int? SessionId);
}
