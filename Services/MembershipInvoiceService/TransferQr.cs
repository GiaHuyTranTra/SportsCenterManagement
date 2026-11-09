using System.Security.Cryptography;
using System.Text;
using APIViewModel.MembershipInvoice;
using Microsoft.Extensions.Caching.Memory;
using Services.ScheduleQueryService;

namespace Services.MembershipInvoiceService;

public partial class MembershipInvoiceService
{
    private sealed record TransferQrSession(int InvoiceId, string CallerId, DateTimeOffset ExpiresAt);

    public async Task<TransferQrSessionAPIViewModel> CreateTransferQrAsync(int invoiceId, string callerId)
    {
        RequireTransferQrEnabled();
        VnpayInvoiceStatusAPIViewModel invoice = await GetVnpayInvoiceStatusAsync(invoiceId, callerId);
        if (invoice.Status != "PENDING_PAYMENT" || invoice.PaymentMethod != "BANK_TRANSFER")
            throw new BusinessRuleException("DEMO_PAYMENT_REJECTED", "A pending transfer invoice is required.", statusCode: 409);

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        TransferQrSession session = new(invoiceId, callerId, expiresAt);
        // The QR grants access only to this development invoice, never to the account's login session.
        _paymentCache!.Set(TransferQrCacheKey(token), session, expiresAt);
        return new TransferQrSessionAPIViewModel
        {
            Token = token, ExpiresAt = expiresAt, RecipientName = "TITAN ARENA", Invoice = invoice
        };
    }

    public Task<VnpayInvoiceStatusAPIViewModel> GetTransferQrStatusAsync(string token)
    {
        TransferQrSession session = ReadTransferQrSession(token);
        return GetVnpayInvoiceStatusAsync(session.InvoiceId, session.CallerId);
    }

    public Task<VnpayInvoiceStatusAPIViewModel> ConfirmTransferQrAsync(string token, bool confirmSimulation)
    {
        TransferQrSession session = ReadTransferQrSession(token);
        return ConfirmDemoPaymentAsync(session.InvoiceId, session.CallerId, confirmSimulation);
    }

    private TransferQrSession ReadTransferQrSession(string token)
    {
        RequireTransferQrEnabled();
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 ||
            !_paymentCache!.TryGetValue(TransferQrCacheKey(token), out TransferQrSession? session) ||
            session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new BusinessRuleException("TRANSFER_QR_EXPIRED", "QR is invalid or expired. Request a new QR.", statusCode: 410);
        return session;
    }

    private void RequireTransferQrEnabled()
    {
        if (!IsDemoPaymentEnabled())
            throw new BusinessRuleException("DEMO_PAYMENT_DISABLED", "Payment simulation is disabled.", statusCode: 404);
        if (_paymentCache is null)
            throw new BusinessRuleException("DEMO_PAYMENT_NOT_CONFIGURED", "Payment session cache is not configured.", statusCode: 503);
    }

    private static string TransferQrCacheKey(string token) =>
        "transfer-qr:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
