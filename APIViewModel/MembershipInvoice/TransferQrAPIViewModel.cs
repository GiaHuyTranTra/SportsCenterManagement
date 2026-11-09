using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipInvoice;

public class TransferQrRequestAPIViewModel
{
    [Required]
    [StringLength(64, MinimumLength = 64)]
    public string Token { get; set; } = string.Empty;

    public bool ConfirmSimulation { get; set; }
}

public class TransferQrSessionAPIViewModel
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public VnpayInvoiceStatusAPIViewModel Invoice { get; set; } = null!;
}
