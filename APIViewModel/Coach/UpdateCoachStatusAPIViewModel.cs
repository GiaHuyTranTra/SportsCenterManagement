using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Coach;

public class UpdateCoachStatusAPIViewModel
{
    [Required]
    public string Status { get; set; } = null!;
}
