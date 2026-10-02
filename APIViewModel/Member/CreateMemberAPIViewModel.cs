using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Member;

public class CreateMemberAPIViewModel
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
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone must be a valid 10-digit number starting with 0.")]
    public string Phone { get; set; } = null!;

    [Required]
    public string MemberCode { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }
}
