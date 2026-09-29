using System;
using System.Collections.Generic;

namespace APIViewModel.MemberSubscription;

public class MemberSubscriptionDetailAPIViewModel
{
    public int SubscriptionId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public int PackageId { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal PackagePrice { get; set; }

    public int DurationMonths { get; set; }

    public List<string> Benefits { get; set; } = new List<string>();

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
