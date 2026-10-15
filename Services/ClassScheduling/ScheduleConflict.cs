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
}
