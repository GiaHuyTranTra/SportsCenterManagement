using System.Data.Common;
using System.Globalization;
using APIViewModel.Report;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.RevenueReportService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public class RevenueReportServiceTests
{
    [Theory]
    [InlineData("DAY")]
    [InlineData("MONTH")]
    public void Revenue_TranslatesProductionTimezoneAndAggregationToSqlServer(string grouping)
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>().UseSqlServer().Options;
        using SportsCenterManagementContext context = new(options);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BusinessSettings:TimeZoneId"] = "SE Asia Standard Time" }).Build();
        RevenueReportService service = new(context, configuration);

        string sql = service.BuildAggregateQuery(new(2026, 10, 1), new(2026, 10, 28), grouping).ToQueryString();

        Assert.Contains("AT TIME ZONE", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("SUM(", sql);
        Assert.Contains("[PaidAt]", sql);
    }

    [Fact]
    public async Task Revenue_OnlyCountsPaidCashByPaidAtAndActualCollectedAmount()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        DateTime paidAt = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);
        fixture.AddInvoice(500000, "PAID", paidAt);
        fixture.AddInvoice(200000, "PAID", paidAt, kind: "RENEW");
        fixture.AddInvoice(75000, "PAID", paidAt, kind: "UPGRADE");
        fixture.AddInvoice(999999, "CANCELED", paidAt);
        fixture.AddInvoice(999999, "PENDING_PAYMENT", null);
        fixture.AddInvoice(999999, "PAID", paidAt, "CARD");
        fixture.AddInvoice(999999, "PAID", paidAt, "BANK_TRANSFER");
        fixture.AddInvoice(999999, "PAID", null);
        fixture.AddInvoice(999999, "PAID", paidAt.AddDays(-2));
        await fixture.Context.SaveChangesAsync();
        RevenueReportService service = new(fixture.Context, fixture.Configuration);

        RevenueReportAPIViewModel report = await service.GetRevenueReportAsync(new(2026, 10, 27), new(2026, 10, 27), "DAY", null);

        Assert.Equal(775000m, report.TotalRevenue);
        Assert.Equal(3, report.PaidInvoiceCount);
        Assert.Equal(report.TotalRevenue, Assert.Single(report.Items).Revenue);
        await using DbCommand command = fixture.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(Amount), 0) FROM MembershipInvoice WHERE Status = 'PAID' AND PaymentMethod = 'CASH' AND PaidAt >= @start AND PaidAt < @end";
        command.Parameters.Add(new SqliteParameter("@start", new DateTime(2026, 10, 26, 17, 0, 0, DateTimeKind.Utc)));
        command.Parameters.Add(new SqliteParameter("@end", new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc)));
        decimal sqlRevenue = Convert.ToDecimal(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Assert.Equal(sqlRevenue, report.TotalRevenue);
    }

    [Fact]
    public async Task Revenue_GroupsByCenterDayAcrossYearBoundaryAndIncludesEmptyDays()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        fixture.AddInvoice(10, "PAID", new(2026, 12, 31, 16, 59, 59, DateTimeKind.Utc));
        fixture.AddInvoice(20, "PAID", new(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc));
        fixture.AddInvoice(40, "PAID", new(2027, 1, 2, 17, 0, 0, DateTimeKind.Utc));
        await fixture.Context.SaveChangesAsync();
        RevenueReportService service = new(fixture.Context, fixture.Configuration);

        RevenueReportAPIViewModel daily = await service.GetRevenueReportAsync(new(2026, 12, 31), new(2027, 1, 2), " day ", " cash ");
        RevenueReportAPIViewModel monthly = await service.GetRevenueReportAsync(new(2026, 12, 31), new(2027, 1, 2), "MONTH", null);

        Assert.Equal(30, daily.TotalRevenue);
        Assert.Equal(new[] { "2026-12-31", "2027-01-01", "2027-01-02" }, daily.Items.Select(item => item.Period));
        Assert.Equal(new decimal[] { 10, 20, 0 }, daily.Items.Select(item => item.Revenue));
        Assert.Equal(new[] { "2026-12", "2027-01" }, monthly.Items.Select(item => item.Period));
        Assert.Equal(daily.TotalRevenue, monthly.TotalRevenue);
        Assert.Equal(daily.PaidInvoiceCount, monthly.PaidInvoiceCount);
    }

    [Theory]
    [InlineData("WEEK", "CASH", "INVALID_REPORT_GROUPING")]
    [InlineData("DAY", "CARD", "UNSUPPORTED_REPORT_PAYMENT_METHOD")]
    [InlineData("DAY", "BANK_TRANSFER", "UNSUPPORTED_REPORT_PAYMENT_METHOD")]
    public async Task Revenue_RejectsUnsupportedFilters(string grouping, string method, string code)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        RevenueReportService service = new(fixture.Context, fixture.Configuration);

        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GetRevenueReportAsync(new(2026, 10, 1), new(2026, 10, 27), grouping, method));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task Revenue_RejectsReversedAndExcessiveRanges()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        RevenueReportService service = new(fixture.Context, fixture.Configuration);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetRevenueReportAsync(new(2026, 10, 28), new(2026, 10, 27), null, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetRevenueReportAsync(new(2025, 1, 1), new(2026, 10, 27), null, null));
        RevenueReportAPIViewModel empty = await service.GetRevenueReportAsync(null, new(2026, 10, 27), null, null);
        Assert.Equal(new DateOnly(2026, 10, 1), empty.From);
        Assert.Equal(0, empty.TotalRevenue);
    }
}
