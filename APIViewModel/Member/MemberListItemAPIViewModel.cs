using System;

namespace APIViewModel.Member;

public class MemberListItemAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public DateTime CreatedAt { get; set; }
}
