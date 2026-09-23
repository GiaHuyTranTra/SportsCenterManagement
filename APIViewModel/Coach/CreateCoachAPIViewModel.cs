using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Coach;

public class CreateCoachAPIViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    public string Phone { get; set; } = null!;

    public string? Specialization { get; set; }

    public string? WorkSchedule { get; set; }
}
