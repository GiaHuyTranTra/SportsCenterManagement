using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipPackage;

public class MembershipPackageInputAPIViewModel
{
    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationMonths { get; set; }

    [Required]
    public List<string>? Benefits { get; set; }
}
