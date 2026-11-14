namespace APIViewModel.Member;

public sealed class CounterCreateMemberResponseAPIViewModel
{
    public MemberDetailAPIViewModel Member { get; set; } = null!;
    public string EmailDelivery { get; set; } = null!;
}
