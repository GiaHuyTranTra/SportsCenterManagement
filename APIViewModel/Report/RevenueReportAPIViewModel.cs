namespace APIViewModel.Report;

public class RevenueReportAPIViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string TimeZoneId { get; set; } = null!;
    public string GroupBy { get; set; } = null!;
    public string PaymentMethod { get; set; } = "CASH";
    public string Currency { get; set; } = "VND";
    public decimal TotalRevenue { get; set; }
    public int PaidInvoiceCount { get; set; }
    public List<RevenueReportBucketAPIViewModel> Items { get; set; } = new();
}

public class RevenueReportBucketAPIViewModel
{
    public string Period { get; set; } = null!;
    public string PaymentMethod { get; set; } = "CASH";
    public decimal Revenue { get; set; }
    public int InvoiceCount { get; set; }
}
