using APIViewModel.MembershipInvoice;
using Microsoft.Extensions.Hosting;
using Services.ScheduleQueryService;

namespace Services.MembershipInvoiceService;

public partial class MembershipInvoiceService
{
    private const string DemoPaymentAuditAction = "DEMO_MEMBERSHIP_INVOICE_PAID";

    public bool IsDemoPaymentEnabled() => _environment?.IsDevelopment() == true &&
        string.Equals(_configuration["DemoPayments:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

    public async Task<VnpayInvoiceStatusAPIViewModel> ConfirmDemoPaymentAsync(int invoiceId, string callerId, bool confirmSimulation)
    {
        // A configuration flag alone must never enable simulated settlement in production.
        if (!IsDemoPaymentEnabled())
            throw new BusinessRuleException("DEMO_PAYMENT_DISABLED", "Payment simulation is disabled.", statusCode: 404);
        if (!confirmSimulation)
            throw new BusinessRuleException("DEMO_CONFIRMATION_REQUIRED", "Explicit simulation confirmation is required.", statusCode: 400);

        VnpayInvoiceStatusAPIViewModel status = await GetVnpayInvoiceStatusAsync(invoiceId, callerId);
        if (status.Status == "PAID" && status.IsDemo) return status;
        if (status.Status != "PENDING_PAYMENT" || status.PaymentMethod != "BANK_TRANSFER")
            throw new BusinessRuleException("DEMO_PAYMENT_REJECTED", "A pending transfer invoice is required.", statusCode: 409);

        string collector = (_configuration["DemoPayments:CollectorAccountId"] ??
            _configuration["VnpaySandbox:CollectorAccountId"] ?? "").Trim();
        if (collector.Length == 0)
            throw new BusinessRuleException("DEMO_PAYMENT_NOT_CONFIGURED", "Configure a test integration account.", statusCode: 503);

        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) = await ConfirmPaymentAsync(invoiceId, collector,
            new PayMembershipInvoiceAPIViewModel
            {
                PaymentMethod = "BANK_TRANSFER", Amount = status.Amount, ConfirmReceivedCash = true
            }, "BANK_TRANSFER", "demo-payment-" + invoiceId, isDemo: true);
        if (result != PayInvoiceResult.Success && result != PayInvoiceResult.AlreadyPaid)
            throw new BusinessRuleException("DEMO_PAYMENT_REJECTED", "Invoice could not be simulated; reload its current state.", statusCode: 409);

        VnpayInvoiceStatusAPIViewModel confirmed = await GetVnpayInvoiceStatusAsync(invoiceId, callerId);
        if (!confirmed.IsDemo)
            throw new BusinessRuleException("DEMO_PAYMENT_REJECTED", "Invoice was settled through another payment method.", statusCode: 409);
        return confirmed;
    }
}
