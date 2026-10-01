using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Coach;

public class UpdateCoachStatusAPIViewModel
{
    [Required]
    [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; } = null!;
}
