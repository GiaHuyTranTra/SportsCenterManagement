using System;

namespace APIViewModel.MemberSubscription;

public class PendingOrderConflictResponseAPIViewModel
{
    public string Message { get; set; } = "Member already has a pending order awaiting payment.";

    public int PendingSubscriptionId { get; set; }

    public int PendingInvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public string PackageName { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}
