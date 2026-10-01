using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Receptionist;

public class UpdateReceptionistAPIViewModel
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = null!;

    [Required]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone must be a valid 10-digit number starting with 0.")]
    public string Phone { get; set; } = null!;

    [StringLength(100)]
    public string? WorkShift { get; set; }
}
