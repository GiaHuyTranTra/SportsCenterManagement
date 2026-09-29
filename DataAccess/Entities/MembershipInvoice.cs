using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class MembershipInvoice
{
    public int Id { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public int SubscriptionId { get; set; }

    public string MemberId { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public string? PaidBy { get; set; }

    public virtual Account CreatedByNavigation { get; set; } = null!;

    public virtual Member Member { get; set; } = null!;

    public virtual Account? PaidByNavigation { get; set; }

    public virtual MemberSubscription Subscription { get; set; } = null!;
}
