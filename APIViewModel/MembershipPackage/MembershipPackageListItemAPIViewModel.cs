namespace APIViewModel.MembershipPackage;

public class MembershipPackageListItemAPIViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public List<string> Benefits { get; set; } = new List<string>();

    public int SubscriberCount { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
