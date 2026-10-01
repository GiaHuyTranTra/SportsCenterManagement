using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Coach;

public class CreateManagedCoachAPIViewModel
{
    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required]
    [RegularExpression("^[0-9]{10}$")]
    public string Phone { get; set; } = null!;

    [MaxLength(500)]
    public string? WorkSchedule { get; set; }

    [Required]
    [MinLength(1)]
    public List<int> DisciplineIds { get; set; } = new List<int>();
}
