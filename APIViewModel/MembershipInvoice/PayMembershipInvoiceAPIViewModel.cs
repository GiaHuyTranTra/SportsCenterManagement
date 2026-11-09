using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipInvoice;

public class PayMembershipInvoiceAPIViewModel
{
    [Required]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = null!;

    [Required]
    [Range(typeof(decimal), "1", "999999999999999999")]
    public decimal? Amount { get; set; }

    public bool ConfirmReceivedCash { get; set; }
}
