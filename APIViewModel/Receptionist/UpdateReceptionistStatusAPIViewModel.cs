using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Receptionist;

public class UpdateReceptionistStatusAPIViewModel
{
    [Required]
    [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Status must be either 'Active' or 'Inactive'.")]
    public string Status { get; set; } = null!;
}
