using System.Globalization;
using Services.ScheduleQueryService;

namespace Services.ClassScheduling;

/// <summary>
/// Lịch lặp của một lớp học: ngày bắt đầu, ngày kết thúc, các thứ trong tuần và
/// khung giờ. Lớp học được tạo kèm lịch này nên toàn bộ buổi học sinh ra ngay
/// trong lệnh tạo lớp, thay vì để lớp rỗng rồi thêm buổi từng ngày một.
/// </summary>
public sealed record ClassRecurrence(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<DayOfWeek> Days,
    TimeSpan StartTime,
    TimeSpan EndTime)
{
    /// <summary>Giới hạn trần để một lần tạo lớp không sinh ra lịch vô hạn.</summary>
    public const int MaxSessionCount = 200;

    public const int MaxRangeInDays = 366;

    private static readonly string[] DateFormats = { "yyyy-MM-dd" };

    private static readonly string[] TimeFormats = { @"hh\:mm", @"hh\:mm\:ss" };

    public static ClassRecurrence Parse(
        string? startDate,
        string? endDate,
        IEnumerable<int>? daysOfWeek,
        string? startTime,
        string? endTime,
        DateTime today)
    {
        DateTime parsedStart = ParseDate(startDate, "startDate", "Ngày bắt đầu");
        DateTime parsedEnd = ParseDate(endDate, "endDate", "Ngày kết thúc");
        TimeSpan parsedStartTime = ParseTime(startTime, "startTime", "Giờ bắt đầu");
        TimeSpan parsedEndTime = ParseTime(endTime, "endTime", "Giờ kết thúc");

        if (parsedStartTime >= parsedEndTime)
        {
            throw Invalid("endTime", "Giờ kết thúc phải sau giờ bắt đầu.");
        }

        if (parsedStart < today)
        {
            throw Invalid("startDate", "Ngày bắt đầu không được ở quá khứ.");
        }

        if (parsedEnd < parsedStart)
        {
            throw Invalid("endDate", "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        }

        if ((parsedEnd - parsedStart).Days + 1 > MaxRangeInDays)
        {
            throw Invalid("endDate", "Khoảng thời gian của lớp tối đa " + MaxRangeInDays + " ngày.");
        }

        List<DayOfWeek> days = NormalizeDays(daysOfWeek);

        return new ClassRecurrence(parsedStart, parsedEnd, days, parsedStartTime, parsedEndTime);
    }

    /// <summary>Trải lịch lặp thành danh sách ngày học, đã sắp xếp tăng dần.</summary>
    public List<DateTime> ExpandDates()
    {
        List<DateTime> dates = new List<DateTime>();
        for (DateTime cursor = StartDate; cursor <= EndDate; cursor = cursor.AddDays(1))
        {
            if (Days.Contains(cursor.DayOfWeek))
            {
                dates.Add(cursor);
            }
        }

        if (dates.Count == 0)
        {
            throw new BusinessRuleException(
                "SCHEDULE_HAS_NO_SESSION",
                "Khoảng thời gian đã chọn không có buổi học nào, hãy kiểm tra lại các thứ trong tuần.",
                new Dictionary<string, string[]>
                {
                    ["daysOfWeek"] = ["Không có ngày nào trong khoảng từ ngày bắt đầu đến ngày kết thúc."]
                });
        }

        if (dates.Count > MaxSessionCount)
        {
            throw Invalid(
                "endDate",
                "Lịch sinh ra " + dates.Count + " buổi, vượt mức tối đa " + MaxSessionCount + " buổi cho một lần tạo lớp.");
        }

        return dates;
    }

    private static List<DayOfWeek> NormalizeDays(IEnumerable<int>? daysOfWeek)
    {
        List<int> raw = (daysOfWeek ?? Enumerable.Empty<int>()).Distinct().ToList();

        if (raw.Count == 0)
        {
            throw Invalid("daysOfWeek", "Chọn ít nhất một thứ trong tuần cho buổi học.");
        }

        if (raw.Any(day => day < 0 || day > 6))
        {
            throw Invalid("daysOfWeek", "Thứ trong tuần phải nằm trong khoảng 0 (Chủ nhật) đến 6 (Thứ bảy).");
        }

        return raw.Select(day => (DayOfWeek)day).OrderBy(day => (int)day).ToList();
    }

    private static DateTime ParseDate(string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateTime.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            throw Invalid(field, label + " không hợp lệ (định dạng YYYY-MM-DD).");
        }

        return parsed.Date;
    }

    private static TimeSpan ParseTime(string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !TimeSpan.TryParseExact(value.Trim(), TimeFormats, CultureInfo.InvariantCulture, out TimeSpan parsed) ||
            parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
        {
            throw Invalid(field, label + " không hợp lệ (định dạng HH:mm).");
        }

        return parsed;
    }

    private static BusinessRuleException Invalid(string field, string message) =>
        new BusinessRuleException(
            "VALIDATION_ERROR",
            message,
            new Dictionary<string, string[]> { [field] = [message] });
}
