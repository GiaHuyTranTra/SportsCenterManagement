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
    public string Phone { get; set; } = null!;

    [Required]
    public string MemberCode { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }
}
