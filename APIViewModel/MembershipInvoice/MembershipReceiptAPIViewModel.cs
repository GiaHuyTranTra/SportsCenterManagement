using System;
using System.Collections.Generic;

namespace APIViewModel.MembershipInvoice;

public class MembershipReceiptAPIViewModel
{
    public int InvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string InvoiceStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? PaidByStaffId { get; set; }

    public string? PaidByStaffName { get; set; }

    public int SubscriptionId { get; set; }

    public string SubscriptionStatus { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int PackageId { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal PackagePrice { get; set; }

    public int DurationMonths { get; set; }

    public List<string> Benefits { get; set; } = new List<string>();

    public string MemberAccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? MemberFullName { get; set; }

    public string MemberEmail { get; set; } = null!;

    public string? MemberPhone { get; set; }
}
