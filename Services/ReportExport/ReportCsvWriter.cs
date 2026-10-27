using System.Globalization;
using System.Text;
using APIViewModel.Report;

namespace Services.ReportExport;

public static class ReportCsvWriter
{
    public static byte[] Revenue(RevenueReportAPIViewModel report)
    {
        List<object?[]> rows = new()
        {
            new object?[] { "RowType", "Period", "PaymentMethod", "Currency", "Revenue", "InvoiceCount" }
        };
        foreach (RevenueReportBucketAPIViewModel item in report.Items)
            rows.Add(new object?[] { "PERIOD", item.Period, item.PaymentMethod, report.Currency, item.Revenue, item.InvoiceCount });
        rows.Add(new object?[] { "TOTAL", null, report.PaymentMethod, report.Currency, report.TotalRevenue, report.PaidInvoiceCount });
        return Encode(rows);
    }

    public static byte[] Members(MemberReportAPIViewModel report)
    {
        return Encode(new List<object?[]>
        {
            new object?[] { "From", "To", "AsOfDate", "TimeZone", "TotalMembers", "NewMembers", "ActiveMembers",
                "ExpiredMembers", "SuspendedMembers", "UpcomingMembers", "PendingPaymentMembers", "MembersWithoutSubscription", "InactiveAccounts" },
            new object?[] { report.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                report.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                report.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), report.TimeZoneId,
                report.TotalMembers, report.NewMembers, report.ActiveMembers, report.ExpiredMembers,
                report.SuspendedMembers, report.UpcomingMembers, report.PendingPaymentMembers,
                report.MembersWithoutSubscription, report.InactiveAccounts }
        });
    }

    public static byte[] Classes(ClassReportAPIViewModel report)
    {
        List<object?[]> rows = new()
        {
            new object?[] { "RowType", "ClassId", "ClassName", "SportId", "SportName", "ClassCount",
                "SessionCount", "CanceledSessionCount", "RegistrationCount", "UniqueMemberCount" }
        };
        foreach (ClassReportRowAPIViewModel item in report.Items)
            rows.Add(new object?[] { "CLASS", item.ClassId, item.ClassName, item.SportId, item.SportName, 1,
                item.SessionCount, item.CanceledSessionCount, item.RegistrationCount, item.UniqueMemberCount });
        rows.Add(new object?[] { "TOTAL", null, null, null, null, report.TotalClasses,
            report.TotalSessions, report.CanceledSessions, report.TotalRegistrations, report.UniqueMembers });
        return Encode(rows);
    }

    private static byte[] Encode(IEnumerable<object?[]> rows)
    {
        StringBuilder csv = new();
        foreach (object?[] row in rows)
            csv.AppendJoin(',', row.Select(Escape)).Append("\r\n");
        UTF8Encoding encoding = new(true);
        byte[] preamble = encoding.GetPreamble();
        byte[] content = encoding.GetBytes(csv.ToString());
        byte[] result = new byte[preamble.Length + content.Length];
        preamble.CopyTo(result, 0);
        content.CopyTo(result, preamble.Length);
        return result;
    }

    private static string Escape(object? value)
    {
        string text = value switch
        {
            null => string.Empty,
            decimal amount => amount.ToString("0", CultureInfo.InvariantCulture),
            IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        if (value is string)
        {
            string trimmed = text.TrimStart();
            // Quoting alone does not prevent spreadsheet applications from evaluating a formula.
            if ((trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) ||
                (text.Length > 0 && (text[0] == '\t' || text[0] == '\r' || text[0] == '\n')))
                text = "'" + text;
        }
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
