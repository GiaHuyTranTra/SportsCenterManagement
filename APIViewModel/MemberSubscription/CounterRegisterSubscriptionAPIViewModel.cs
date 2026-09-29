using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MemberSubscription;

public class CounterRegisterSubscriptionAPIViewModel
{
    [Required]
    public string MemberAccountId { get; set; } = null!;

    [Required]
    [Range(1, int.MaxValue)]
    public int? PackageId { get; set; }

    [Required]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = null!;
}
