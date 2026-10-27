using APIViewModel.Report;

namespace Services.RevenueReportService;

public interface IRevenueReportService
{
    Task<RevenueReportAPIViewModel> GetRevenueReportAsync(
        DateOnly? from, DateOnly? to, string? groupBy, string? paymentMethod);
}
