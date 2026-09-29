using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipInvoice;

public class PayMembershipInvoiceAPIViewModel
{
    [Required]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = null!;
}
