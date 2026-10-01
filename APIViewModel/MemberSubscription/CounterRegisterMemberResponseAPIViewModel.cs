using APIViewModel.Member;
using APIViewModel.MembershipInvoice;

namespace APIViewModel.MemberSubscription;

public class CounterRegisterMemberResponseAPIViewModel
{
    public MemberDetailAPIViewModel Member { get; set; } = null!;

    public MembershipReceiptAPIViewModel Receipt { get; set; } = null!;

    public string InitialPassword { get; set; } = null!;

    public string EmailDelivery { get; set; } = null!;
}
