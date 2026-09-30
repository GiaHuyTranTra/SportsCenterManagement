using System.ComponentModel.DataAnnotations;

namespace APIViewModel.CenterManager;

public class CreateCenterManagerAPIViewModel
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
    [RegularExpression("^[0-9]{10}$")]
    public string Phone { get; set; } = null!;
}
