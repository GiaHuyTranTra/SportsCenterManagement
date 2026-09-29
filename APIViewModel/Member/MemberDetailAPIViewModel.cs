using System;

namespace APIViewModel.Member;

public class MemberDetailAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
