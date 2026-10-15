using System.Globalization;
using System.Text;

namespace Services.ClassScheduling;

public static class Flow2Name
{
    public static string Normalize(string value)
    {
        StringBuilder result = new StringBuilder();
        bool pendingSpace = false;
        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }
            if (pendingSpace) result.Append(' ');
            pendingSpace = false;
            result.Append(character is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(character));
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
