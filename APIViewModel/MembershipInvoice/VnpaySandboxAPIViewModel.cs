using System.Text.Json.Serialization;

namespace APIViewModel.MembershipInvoice;

public class VnpayCheckoutAPIViewModel
{
    public int InvoiceId { get; set; }
    public string TransactionReference { get; set; } = null!;
    public string PaymentUrl { get; set; } = null!;
    public bool IsTestMode { get; set; } = true;
}

public class VnpayIpnAPIViewModel
{
    [JsonPropertyName("RspCode")]
    public string RspCode { get; set; } = null!;
    [JsonPropertyName("Message")]
    public string Message { get; set; } = null!;
}
