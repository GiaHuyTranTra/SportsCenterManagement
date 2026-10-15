using System.Collections.Generic;

namespace APIViewModel.MembershipPackageSport;

public class PackageSportsViewModel
{
    public int PackageId { get; set; }

    public List<int> SportIds { get; set; } = new List<int>();

    public bool AllowsAllSports { get; set; }
}

public class UpdatePackageSportsAPIViewModel
{
    public List<int> SportIds { get; set; } = new List<int>();
}
