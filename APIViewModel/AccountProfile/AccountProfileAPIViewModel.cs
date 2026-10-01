using System.ComponentModel.DataAnnotations;

namespace APIViewModel.AccountProfile;

public class AccountProfileAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Specialization { get; set; }

    public string? WorkSchedule { get; set; }

    public string? MemberCode { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class UpdateAccountProfileAPIViewModel
{
    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [RegularExpression("^$|^0[0-9]{9}$")]
    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(200)]
    public string? Specialization { get; set; }

    [MaxLength(300)]
    public string? WorkSchedule { get; set; }
}
