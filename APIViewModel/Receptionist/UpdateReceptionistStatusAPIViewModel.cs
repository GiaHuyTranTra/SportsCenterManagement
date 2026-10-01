using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Receptionist;

public class UpdateReceptionistStatusAPIViewModel
{
    [Required]
    public string Status { get; set; } = null!;
}
