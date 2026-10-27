using System.Globalization;
using APIViewModel.Report;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.ScheduleQueryService;

namespace Services.RevenueReportService;

public class RevenueReportService : IRevenueReportService
{
    private readonly SportsCenterManagementContext _context;
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _clock;

    public RevenueReportService(SportsCenterManagementContext context,
        IConfiguration configuration, TimeProvider? clock = null)
    {
        _context = context;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration["BusinessSettings:TimeZoneId"]
            ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured."));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<RevenueReportAPIViewModel> GetRevenueReportAsync(
        DateOnly? from, DateOnly? to, string? groupBy, string? paymentMethod)
    {
        string grouping = string.IsNullOrWhiteSpace(groupBy) ? "DAY" : groupBy.Trim().ToUpperInvariant();
        if (grouping != "DAY" && grouping != "MONTH")
            throw new BusinessRuleException("INVALID_REPORT_GROUPING", "Group by DAY or MONTH.", statusCode: 400);

        string method = string.IsNullOrWhiteSpace(paymentMethod) ? "CASH" : paymentMethod.Trim().ToUpperInvariant();
        if (method != "CASH")
            throw new BusinessRuleException("UNSUPPORTED_REPORT_PAYMENT_METHOD", "Only reconciled CASH payments are supported.", statusCode: 400);

        DateOnly end = to ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _timeZone).DateTime);
        DateOnly start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (end == DateOnly.MaxValue || start > end || end.DayNumber - start.DayNumber > 365)
            throw new BusinessRuleException("INVALID_REPORT_DATE_RANGE", "Choose an ordered range of at most 366 days.", statusCode: 400);

        DateTime startUtc = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), _timeZone);
        DateTime endUtc = TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue), _timeZone);
        List<RevenueAggregate> aggregates = await BuildAggregateQuery(startUtc, endUtc, grouping).ToListAsync();
        Dictionary<DateOnly, RevenueAggregate> lookup = aggregates.ToDictionary(
            aggregate => new DateOnly(aggregate.Year, aggregate.Month, aggregate.Day));

        RevenueReportAPIViewModel result = new()
        {
            From = start, To = end, GroupBy = grouping, TimeZoneId = _timeZone.Id,
            TotalRevenue = aggregates.Sum(aggregate => aggregate.Revenue),
            PaidInvoiceCount = aggregates.Sum(aggregate => aggregate.Count)
        };
        DateOnly period = grouping == "MONTH" ? new DateOnly(start.Year, start.Month, 1) : start;
        while (period <= end)
        {
            lookup.TryGetValue(period, out RevenueAggregate? aggregate);
            result.Items.Add(new RevenueReportBucketAPIViewModel
            {
                Period = period.ToString(grouping == "MONTH" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Revenue = aggregate?.Revenue ?? 0m, InvoiceCount = aggregate?.Count ?? 0
            });
            if (grouping == "MONTH" && period.Year == 9999 && period.Month == 12) break;
            period = grouping == "MONTH" ? period.AddMonths(1) : period.AddDays(1);
        }
        return result;
    }

    internal IQueryable<RevenueAggregate> BuildAggregateQuery(DateTime startUtc, DateTime endUtc, string grouping)
    {
        IQueryable<MembershipInvoice> invoices = _context.MembershipInvoices.AsNoTracking().Where(invoice =>
            invoice.Status == "PAID" && invoice.PaymentMethod == "CASH" &&
            invoice.PaidAt.HasValue && invoice.PaidAt.Value >= startUtc && invoice.PaidAt.Value < endUtc);

        IQueryable<PaymentRow> payments;
        if (_context.Database.IsSqlServer())
        {
            string sqlTimeZone = TimeZoneInfo.TryConvertIanaIdToWindowsId(_timeZone.Id, out string? windowsId)
                ? windowsId! : _timeZone.Id;
            payments = invoices.Select(invoice => new PaymentRow
            {
                LocalPaidAt = EF.Functions.AtTimeZone(
                    EF.Functions.AtTimeZone(invoice.PaidAt!.Value, "UTC"), sqlTimeZone).Date,
                Amount = invoice.Amount
            });
        }
        else
        {
            // SQLite test hosts support fixed offsets; production uses SQL Server AT TIME ZONE.
            if (_timeZone.SupportsDaylightSavingTime)
                throw new InvalidOperationException("This report provider requires a fixed-offset center time zone.");
            double offsetMinutes = _timeZone.BaseUtcOffset.TotalMinutes;
            payments = invoices.Select(invoice => new PaymentRow
            {
                LocalPaidAt = invoice.PaidAt!.Value.AddMinutes(offsetMinutes), Amount = invoice.Amount
            });
        }

        return payments.GroupBy(payment => new
            {
                payment.LocalPaidAt.Year, payment.LocalPaidAt.Month,
                Day = grouping == "DAY" ? payment.LocalPaidAt.Day : 1
            })
            .Select(group => new RevenueAggregate
            {
                Year = group.Key.Year, Month = group.Key.Month, Day = group.Key.Day,
                Revenue = group.Sum(payment => payment.Amount), Count = group.Count()
            });
    }

    private sealed class PaymentRow
    {
        public DateTime LocalPaidAt { get; set; }
        public decimal Amount { get; set; }
    }

    internal sealed class RevenueAggregate
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public decimal Revenue { get; set; }
        public int Count { get; set; }
    }
}
