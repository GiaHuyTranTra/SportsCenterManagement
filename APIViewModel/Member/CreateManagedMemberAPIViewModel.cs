using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Member;

public class CreateManagedMemberAPIViewModel
{
    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = null!;

    [Required]
    [RegularExpression("^0[0-9]{9}$")]
    public string Phone { get; set; } = null!;

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    [Required]
    public bool? IsActive { get; set; }
}
