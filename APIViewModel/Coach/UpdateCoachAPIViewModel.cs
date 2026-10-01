using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Coach;

public class UpdateCoachAPIViewModel
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = null!;

    [Required]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone must be a valid 10-digit number starting with 0.")]
    public string Phone { get; set; } = null!;

    [StringLength(200)]
    public string? Specialization { get; set; }

    [StringLength(300)]
    public string? WorkSchedule { get; set; }
}
