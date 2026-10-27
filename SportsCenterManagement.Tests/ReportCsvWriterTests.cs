using System.Globalization;
using System.Text;
using APIViewModel.Report;
using Services.ReportExport;

namespace SportsCenterManagement.Tests;

public class ReportCsvWriterTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+SUM(1)")]
    [InlineData("-1+1")]
    [InlineData("@SUM(1)")]
    [InlineData("  =1+1")]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    [InlineData("\n=1+1")]
    public void Classes_NeutralizesFormulaCells(string className)
    {
        ClassReportAPIViewModel report = ClassReport(className);

        string csv = Encoding.UTF8.GetString(ReportCsvWriter.Classes(report));

        Assert.Contains("\"'" + className.Replace("\"", "\"\"") + "\"", csv);
    }

    [Fact]
    public void Classes_PreservesVietnameseQuotesAndEmbeddedLineBreaks()
    {
        ClassReportAPIViewModel report = ClassReport("Lớp, \"Yoga\"\r\nBuổi sáng");
        byte[] bytes = ReportCsvWriter.Classes(report);
        string csv = Encoding.UTF8.GetString(bytes);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        Assert.Contains("\"Lớp, \"\"Yoga\"\"\r\nBuổi sáng\"", csv);
        Assert.Contains("\"TOTAL\",\"\",\"\",\"\",\"\",\"1\",\"2\",\"0\",\"3\",\"2\"\r\n", csv);
    }

    [Fact]
    public void Revenue_UsesInvariantVndAndRetainsTotals()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            RevenueReportAPIViewModel report = new()
            {
                TotalRevenue = 1234567m, PaidInvoiceCount = 2,
                Items = new() { new() { Period = "2026-10", Revenue = 1234567m, InvoiceCount = 2 } }
            };
            string csv = Encoding.UTF8.GetString(ReportCsvWriter.Revenue(report));
            Assert.Contains("\"PERIOD\",\"2026-10\",\"CASH\",\"VND\",\"1234567\",\"2\"", csv);
            Assert.Contains("\"TOTAL\",\"\",\"CASH\",\"VND\",\"1234567\",\"2\"", csv);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Members_ExportsOnlyAggregateFields()
    {
        MemberReportAPIViewModel report = new()
        {
            To = new(2026, 10, 27), AsOfDate = new(2026, 10, 27), TimeZoneId = "SE Asia Standard Time",
            TotalMembers = 5, NewMembers = 2, ActiveMembers = 3, ExpiredMembers = 1
        };
        string csv = Encoding.UTF8.GetString(ReportCsvWriter.Members(report));

        Assert.Contains("\"2026-10-27\"", csv);
        Assert.DoesNotContain("email", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", csv, StringComparison.OrdinalIgnoreCase);
    }

    private static ClassReportAPIViewModel ClassReport(string name)
    {
        return new()
        {
            TotalClasses = 1, TotalSessions = 2, TotalRegistrations = 3, UniqueMembers = 2,
            Items = new() { new() { ClassId = 1, ClassName = name, SportId = 1, SportName = "Thể thao",
                SessionCount = 2, RegistrationCount = 3, UniqueMemberCount = 2 } }
        };
    }
}
