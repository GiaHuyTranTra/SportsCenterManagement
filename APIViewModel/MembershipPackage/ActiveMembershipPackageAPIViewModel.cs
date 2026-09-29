namespace APIViewModel.MembershipPackage;

public class ActiveMembershipPackageAPIViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public List<string> Benefits { get; set; } = new List<string>();
}
