using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Member
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual ICollection<MemberSubscription> MemberSubscriptions { get; set; } = new List<MemberSubscription>();

    public virtual ICollection<MembershipInvoice> MembershipInvoices { get; set; } = new List<MembershipInvoice>();
}
