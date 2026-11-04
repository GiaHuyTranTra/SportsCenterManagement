using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ScheduleQueryService;

namespace Services.MembershipInvoiceService;

public partial class MembershipInvoiceService
{
    private (string Merchant, string Secret, string Actor) VnpayConfiguration()
    {
        string merchant = _configuration["VnpaySandbox:TmnCode"] ?? "";
        string secret = _configuration["VnpaySandbox:HashSecret"] ?? "";
        string actor = _configuration["VnpaySandbox:CollectorAccountId"] ?? "";
        if (_configuration["VnpaySandbox:Enabled"] != "true" || merchant.Length == 0 || secret.Length < 32 || actor.Length == 0)
            throw new BusinessRuleException("VNPAY_NOT_CONFIGURED", "Configure VNPAY sandbox credentials and integration account.", statusCode: 503);
        return (merchant, secret, actor);
    }

    public async Task<VnpayCheckoutAPIViewModel> CreateVnpayCheckoutAsync(int invoiceId, string callerId, string clientIp)
    {
        (string merchant, string secret, string actor) = VnpayConfiguration();
        Account? caller = await _context.Accounts.AsNoTracking().Include(item => item.Role).FirstOrDefaultAsync(item => item.Id == callerId);
        if (caller is null || caller.DeletedAt != null || caller.Status != "Active" || caller.IsLocked ||
            (caller.Role.Name != "Member" && caller.Role.Name != "Receptionist" && caller.Role.Name != "CenterManager"))
            throw new BusinessRuleException("FORBIDDEN", "Account cannot create checkout.", statusCode: 403);
        MembershipInvoice? invoice = await _context.MembershipInvoices.AsNoTracking().Include(item => item.Subscription)
            .FirstOrDefaultAsync(item => item.Id == invoiceId && (caller.Role.Name != "Member" || item.MemberId == callerId));
        if (invoice is null) throw new BusinessRuleException("INVOICE_NOT_FOUND", "Invoice not found.", statusCode: 404);
        if (invoice.Status != "PENDING_PAYMENT" || invoice.PaymentMethod != "BANK_TRANSFER" || invoice.Amount <= 0 ||
            invoice.Amount != decimal.Truncate(invoice.Amount) || invoice.Amount > 9999999999m ||
            invoice.Subscription.Status != "PENDING_PAYMENT" || invoice.Subscription.MemberId != invoice.MemberId ||
            invoice.Subscription.IsSuspended || (invoice.Subscription.Kind != "REGISTER" && invoice.Subscription.Kind != "RENEW"))
            throw new BusinessRuleException("INVALID_INVOICE_STATE", "A supported pending transfer invoice is required.", statusCode: 409);
        if (invoice.Amount != invoice.Subscription.PackagePrice || invoice.PaidAt != null || invoice.PaidBy != null ||
            !await _context.MembershipPackages.AsNoTracking().AnyAsync(package => package.Id == invoice.Subscription.PackageId &&
                package.IsActive && package.Price == invoice.Amount && package.DurationMonths == invoice.Subscription.DurationMonths && package.DurationMonths > 0))
            throw new BusinessRuleException("PACKAGE_CHANGED", "Create a valid current package order before checkout.", statusCode: 409);
        string returnUrl = _configuration["VnpaySandbox:ReturnUrl"] ?? "";
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new BusinessRuleException("VNPAY_NOT_CONFIGURED", "Configure an HTTPS or local development return URL.", statusCode: 503);
        DateTime utc = DateTime.UtcNow;
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        string reference = Guid.NewGuid().ToString("N");
        string amount = (invoice.Amount * 100m).ToString("0", CultureInfo.InvariantCulture);
        SortedDictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = merchant,
            ["vnp_Amount"] = amount, ["vnp_CurrCode"] = "VND", ["vnp_TxnRef"] = reference,
            ["vnp_OrderInfo"] = "Thanh toan SCMS " + invoice.Id.ToString(CultureInfo.InvariantCulture),
            ["vnp_OrderType"] = "other", ["vnp_Locale"] = "vn", ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_IpAddr"] = clientIp, ["vnp_BankCode"] = "VNBANK",
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };
        _context.AuditLogs.Add(new AuditLog
        {
            Id = "vnpay-attempt-" + reference, AccountId = callerId, Action = "VNPAY_SANDBOX_CHECKOUT",
            EntityType = "MembershipInvoice", EntityId = invoice.Id.ToString(CultureInfo.InvariantCulture),
            Description = amount, CreatedAt = utc
        });
        await _context.SaveChangesAsync();
        return new VnpayCheckoutAPIViewModel
        {
            InvoiceId = invoice.Id, TransactionReference = reference,
            PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?" + VnpayCanonical(values) + "&vnp_SecureHash=" + VnpaySign(values, secret)
        };
    }

    internal static string VnpayCanonical(IEnumerable<KeyValuePair<string, string>> values) => string.Join("&", values
        .Where(item => item.Key.StartsWith("vnp_", StringComparison.Ordinal) && item.Key != "vnp_SecureHash" && item.Key != "vnp_SecureHashType" && !string.IsNullOrEmpty(item.Value))
        .OrderBy(item => item.Key, StringComparer.Ordinal)
        .Select(item => WebUtility.UrlEncode(item.Key) + "=" + WebUtility.UrlEncode(item.Value)));

    internal static string VnpaySign(IEnumerable<KeyValuePair<string, string>> values, string secret) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(VnpayCanonical(values)))).ToLowerInvariant();

    public async Task<VnpayIpnAPIViewModel> ProcessVnpayIpnAsync(IReadOnlyDictionary<string, string> query)
    {
        (string merchant, string secret, string actor) = VnpayConfiguration();
        string Value(string name) => query.TryGetValue(name, out string? value) ? value : "";
        string hash = Value("vnp_SecureHash");
        if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{128}$") ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), Convert.FromHexString(VnpaySign(query, secret))) ||
            Value("vnp_TmnCode") != merchant) return Ipn("97", "Invalid signature or merchant");
        string reference = Value("vnp_TxnRef");
        if (!Regex.IsMatch(reference, "^[a-f0-9]{32}$")) return Ipn("01", "Unknown transaction");
        AuditLog? attempt = await _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == "vnpay-attempt-" + reference);
        if (attempt is null || !int.TryParse(attempt.EntityId, out int invoiceId)) return Ipn("01", "Unknown transaction");
        if (!decimal.TryParse(Value("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out decimal scaled) ||
            scaled <= 0 || Value("vnp_Amount") != attempt.Description) return Ipn("04", "Invalid amount");
        MembershipInvoice? invoice = await _context.MembershipInvoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoiceId);
        if (invoice is null) return Ipn("01", "Invoice not found");
        if (invoice.Amount * 100m != scaled || invoice.PaymentMethod != "BANK_TRANSFER") return Ipn("04", "Invoice amount or method mismatch");
        if (Value("vnp_ResponseCode") != "00" || Value("vnp_TransactionStatus") != "00") return Ipn("00", "Unsuccessful transaction acknowledged; invoice unchanged");
        string transaction = Value("vnp_TransactionNo");
        if (!Regex.IsMatch(transaction, "^[0-9]{1,20}$") || transaction.All(character => character == '0')) return Ipn("99", "Invalid transaction number");
        string eventId = "vnpay-sandbox-" + merchant + "-" + transaction;
        AuditLog? prior = await _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == eventId);
        if (prior is not null) return prior.EntityId == attempt.EntityId ? Ipn("02", "Already confirmed") : Ipn("99", "Transaction already used");
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) = await ConfirmPaymentAsync(invoiceId, actor,
            new PayMembershipInvoiceAPIViewModel { PaymentMethod = "BANK_TRANSFER", Amount = scaled / 100m, ConfirmReceivedCash = true }, "BANK_TRANSFER", eventId);
        return result switch
        {
            PayInvoiceResult.Success => Ipn("00", "Confirm success"),
            PayInvoiceResult.AlreadyPaid => Ipn("02", "Invoice already confirmed"),
            PayInvoiceResult.AmountMismatch => Ipn("04", "Invalid amount"),
            PayInvoiceResult.InvoiceNotFound => Ipn("01", "Invoice not found"),
            _ => Ipn("99", "Payment not applied; operator review required")
        };
    }

    private static VnpayIpnAPIViewModel Ipn(string code, string message) => new() { RspCode = code, Message = message };
}
