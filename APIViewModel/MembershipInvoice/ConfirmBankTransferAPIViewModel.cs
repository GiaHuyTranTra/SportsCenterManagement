using System.ComponentModel.DataAnnotations;

namespace APIViewModel.MembershipInvoice;

public class ConfirmBankTransferAPIViewModel
{
    [Required]
    [Range(typeof(decimal), "1", "999999999999999999")]
    public decimal? Amount { get; set; }
    public bool ConfirmReceivedTransfer { get; set; }
}

public class BankTransferInstructionsAPIViewModel
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string BankId { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string TransferContent { get; set; } = null!;
    public string? QrImageUrl { get; set; }
    public bool IsTestMode { get; set; }
    public string Status { get; set; } = "PENDING_PAYMENT";
}
