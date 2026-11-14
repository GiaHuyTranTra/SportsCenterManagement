using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ScheduleQueryService;

namespace Services.ClassScheduling;

public static class ScheduleConflict
{
    public static async Task ValidateAsync(SportsCenterManagementContext context, int? excludeId,
        string coachId, int roomId, DateTime date, TimeSpan start, TimeSpan end)
    {
        IQueryable<ClassSession> conflicts = context.ClassSessions.AsNoTracking().Where(item =>
            (!excludeId.HasValue || item.Id != excludeId.Value) && item.Status == "SCHEDULED" &&
            item.SessionDate == date && item.StartTime < end && item.EndTime > start);
        if (await conflicts.AnyAsync(item => item.CoachId == coachId))
            throw new BusinessRuleException("COACH_SCHEDULE_CONFLICT", "Huấn luyện viên bị trùng lịch.",
                new Dictionary<string, string[]> { ["coachId"] = ["Trùng lịch với buổi khác."] });
        if (await conflicts.AnyAsync(item => item.RoomId == roomId))
            throw new BusinessRuleException("ROOM_SCHEDULE_CONFLICT", "Phòng tập bị trùng lịch.",
                new Dictionary<string, string[]> { ["roomId"] = ["Trùng lịch với buổi khác."] });
    }

    /// <summary>
    /// Kiểm tra cả loạt ngày của một lịch lặp trong một lượt truy vấn. Dùng khi
    /// tạo lớp kèm lịch: báo trùng kèm ngày cụ thể để người dùng sửa đúng chỗ,
    /// thay vì tạo một nửa số buổi rồi mới dừng.
    /// </summary>
    public static async Task ValidateRangeAsync(SportsCenterManagementContext context,
        string coachId, int roomId, IReadOnlyList<DateTime> dates, TimeSpan start, TimeSpan end)
    {
        if (dates.Count == 0) return;

        DateTime first = dates[0];
        DateTime last = dates[dates.Count - 1];
        HashSet<DateTime> wanted = dates.ToHashSet();

        var overlapping = await context.ClassSessions.AsNoTracking()
            .Where(item => item.Status == "SCHEDULED" &&
                item.SessionDate >= first && item.SessionDate <= last &&
                item.StartTime < end && item.EndTime > start &&
                (item.CoachId == coachId || item.RoomId == roomId))
            .Select(item => new { item.SessionDate, item.CoachId, item.RoomId })
            .ToListAsync();

        DateTime? coachClash = overlapping
            .Where(item => item.CoachId == coachId && wanted.Contains(item.SessionDate))
            .Select(item => (DateTime?)item.SessionDate)
            .OrderBy(date => date)
            .FirstOrDefault();

        if (coachClash.HasValue)
            throw new BusinessRuleException("COACH_SCHEDULE_CONFLICT",
                "Giáo viên đã có buổi dạy khác vào " + coachClash.Value.ToString("yyyy-MM-dd") + " trong khung giờ này.",
                new Dictionary<string, string[]>
                {
                    ["coachId"] = ["Trùng lịch ngày " + coachClash.Value.ToString("yyyy-MM-dd") + "."]
                });

        DateTime? roomClash = overlapping
            .Where(item => item.RoomId == roomId && wanted.Contains(item.SessionDate))
            .Select(item => (DateTime?)item.SessionDate)
            .OrderBy(date => date)
            .FirstOrDefault();

        if (roomClash.HasValue)
            throw new BusinessRuleException("ROOM_SCHEDULE_CONFLICT",
                "Phòng tập đã có buổi khác vào " + roomClash.Value.ToString("yyyy-MM-dd") + " trong khung giờ này.",
                new Dictionary<string, string[]>
                {
                    ["roomId"] = ["Trùng lịch ngày " + roomClash.Value.ToString("yyyy-MM-dd") + "."]
                });
    }
}
