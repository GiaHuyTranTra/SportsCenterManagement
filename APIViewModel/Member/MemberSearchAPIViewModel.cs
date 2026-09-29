namespace APIViewModel.Member;

public class MemberSearchAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;
}
