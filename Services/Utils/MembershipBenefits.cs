using System.Text.Json;

namespace Services.Utils;

public static class MembershipBenefits
{
    public static List<string> Read(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>(); }
        catch (JsonException)
        {
            // Older local seeds stored plain text; preserve it without changing persisted records.
            return new List<string> { value.Trim() };
        }
    }
}
