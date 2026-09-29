using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class MemberSubscription
{
    public int Id { get; set; }

    public string MemberId { get; set; } = null!;

    public int PackageId { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal PackagePrice { get; set; }

    public int DurationMonths { get; set; }

    public string Benefits { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual MembershipInvoice? MembershipInvoice { get; set; }

    public virtual MembershipPackage Package { get; set; } = null!;
}
