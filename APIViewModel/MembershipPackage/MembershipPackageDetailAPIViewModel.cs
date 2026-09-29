namespace APIViewModel.MembershipPackage;

public class MembershipPackageDetailAPIViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    public List<string> Benefits { get; set; } = new List<string>();

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
