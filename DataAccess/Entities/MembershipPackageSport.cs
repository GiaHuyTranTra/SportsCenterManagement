using System;

namespace DataAccess.Entities;

public partial class MembershipPackageSport
{
    public int PackageId { get; set; }

    public int DisciplineId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MembershipPackage Package { get; set; } = null!;

    public virtual Discipline Discipline { get; set; } = null!;
}
