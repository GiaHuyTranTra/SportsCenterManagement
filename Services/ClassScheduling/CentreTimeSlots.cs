namespace Services.ClassScheduling;

/// <summary>
/// The centre runs on fixed slots — there is no free-form booking. The booking
/// screens only ever offer this list (see CENTER_TIME_SLOTS in
/// src/utils/classSchedule.ts), but nothing enforced it on the server, so the
/// API happily accepted a "session" running 12:00–21:03 and the schedule grid
/// then had no slot to file it under.
/// </summary>
public static class CentreTimeSlots
{
    public sealed record Slot(string Id, string Name, TimeSpan StartTime, TimeSpan EndTime);

    public static readonly IReadOnlyList<Slot> All = new List<Slot>
    {
        new("SLOT_1", "Ca 1 (Sáng sớm)", new TimeSpan(6, 30, 0), new TimeSpan(7, 30, 0)),
        new("SLOT_2", "Ca 2 (Sáng)", new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0)),
        new("SLOT_3", "Ca 3 (Sáng muộn)", new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)),
        new("SLOT_4", "Ca 4 (Trưa - Chiều)", new TimeSpan(14, 0, 0), new TimeSpan(15, 30, 0)),
        new("SLOT_5", "Ca 5 (Chiều muộn)", new TimeSpan(16, 0, 0), new TimeSpan(17, 0, 0)),
        new("SLOT_6", "Ca 6 (Tối)", new TimeSpan(18, 0, 0), new TimeSpan(19, 30, 0)),
        new("SLOT_7", "Ca 7 (Tối muộn)", new TimeSpan(19, 30, 0), new TimeSpan(21, 0, 0))
    };

    public static Slot? Find(TimeSpan startTime, TimeSpan endTime) =>
        All.FirstOrDefault(slot => slot.StartTime == startTime && slot.EndTime == endTime);

    public static bool IsCentreSlot(TimeSpan startTime, TimeSpan endTime) =>
        Find(startTime, endTime) is not null;

    /// <summary>Human-readable slot list, used in validation messages.</summary>
    public static string Describe() =>
        string.Join(", ", All.Select(slot =>
            $"{slot.StartTime:hh\\:mm}–{slot.EndTime:hh\\:mm}"));
}
