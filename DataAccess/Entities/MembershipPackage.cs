using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class MembershipPackage
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public string Benefits { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<MemberSubscription> MemberSubscriptions { get; set; } = new List<MemberSubscription>();
}
