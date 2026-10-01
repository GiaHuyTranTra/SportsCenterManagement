namespace APIViewModel.Member;

public class CreateManagedMemberResponseAPIViewModel
{
    public MemberDetailAPIViewModel Member { get; set; } = null!;

    public string InitialPassword { get; set; } = null!;
}
