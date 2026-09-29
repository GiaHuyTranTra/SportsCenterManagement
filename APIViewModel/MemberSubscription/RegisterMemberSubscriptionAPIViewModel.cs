using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MemberSubscription;

public class RegisterMemberSubscriptionAPIViewModel
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? PackageId { get; set; }

    [Required]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = null!;
}
