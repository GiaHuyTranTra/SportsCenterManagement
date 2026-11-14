namespace Services.Utils;

/// <summary>
/// Single place for the date-of-birth window the centre accepts.
///
/// The front end already refuses anything outside 10–100 years
/// (src/utils/validation.ts), but nothing enforced it server side, so rows such
/// as a member born in the year 0200 reached the database through the API.
/// </summary>
public static class BirthDateRules
{
    public const int MinimumAgeYears = 10;
    public const int MaximumAgeYears = 100;

    private const string CentreTimeZoneId = "SE Asia Standard Time";

    /// <summary>
    /// Today on the centre's own calendar. Using UTC here would reject a
    /// birth date of "today" for the first seven hours of every Vietnamese day.
    /// </summary>
    public static DateOnly CentreToday()
    {
        try
        {
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(CentreTimeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    /// <summary>
    /// True when <paramref name="dateOfBirth"/> is a plausible birth date: in
    /// the past, and an age between <see cref="MinimumAgeYears"/> and
    /// <see cref="MaximumAgeYears"/> inclusive.
    /// </summary>
    public static bool IsAcceptable(DateOnly dateOfBirth)
    {
        DateOnly today = CentreToday();
        if (dateOfBirth > today)
        {
            return false;
        }

        DateOnly youngestAllowed = today.AddYears(-MinimumAgeYears);
        DateOnly oldestAllowed = today.AddYears(-MaximumAgeYears);
        return dateOfBirth <= youngestAllowed && dateOfBirth >= oldestAllowed;
    }
}
