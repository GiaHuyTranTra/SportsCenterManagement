using System.Globalization;
using System.Text.RegularExpressions;
using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ScheduleQueryService;

namespace Services.MembershipInvoiceService;

public partial class MembershipInvoiceService
{
    public async Task<BankTransferInstructionsAPIViewModel> GetBankTransferInstructionsAsync(int invoiceId, string callerId)
    {
        Account? caller = await _context.Accounts.AsNoTracking().Include(account => account.Role)
            .FirstOrDefaultAsync(account => account.Id == callerId);
        if (caller is null || caller.DeletedAt != null || caller.Status != "Active" || caller.IsLocked ||
            (caller.Role.Name != "Member" && caller.Role.Name != "Receptionist" && caller.Role.Name != "CenterManager"))
            throw new BusinessRuleException("FORBIDDEN", "Account cannot access payment instructions.", statusCode: 403);
        MembershipInvoice? invoice = await _context.MembershipInvoices.AsNoTracking().Include(item => item.Subscription)
            .FirstOrDefaultAsync(item => item.Id == invoiceId && (caller.Role.Name != "Member" || item.MemberId == callerId));
        if (invoice is null)
            throw new BusinessRuleException("INVOICE_NOT_FOUND", "Invoice not found.", statusCode: 404);
        if (invoice.Status != "PENDING_PAYMENT" || invoice.PaymentMethod != "BANK_TRANSFER" ||
            invoice.Subscription.Status != "PENDING_PAYMENT" || invoice.Subscription.MemberId != invoice.MemberId ||
            invoice.Amount <= 0 || decimal.Truncate(invoice.Amount) != invoice.Amount || invoice.PaidAt.HasValue || invoice.PaidBy != null)
            throw new BusinessRuleException("INVALID_INVOICE_STATE", "A pending bank-transfer order is required.", statusCode: 409);
        if (invoice.Subscription.Kind != "REGISTER" && invoice.Subscription.Kind != "RENEW")
            throw new BusinessRuleException("ORDER_KIND_NOT_SUPPORTED", "Create a supported register or renewal order.", statusCode: 409);
        if (invoice.Subscription.IsSuspended || invoice.Amount != invoice.Subscription.PackagePrice ||
            !await _context.MembershipPackages.AsNoTracking().AnyAsync(package =>
                package.Id == invoice.Subscription.PackageId && package.IsActive && package.Price == invoice.Amount &&
                package.DurationMonths == invoice.Subscription.DurationMonths && package.DurationMonths > 0))
            throw new BusinessRuleException("PACKAGE_CHANGED", "Reload packages and create a valid order before transferring.", statusCode: 409);

        string bank = _configuration["BankTransfer:BankId"]?.Trim() ?? "";
        string number = _configuration["BankTransfer:AccountNumber"]?.Trim() ?? "";
        string name = _configuration["BankTransfer:AccountName"]?.Trim() ?? "";
        if (!Regex.IsMatch(bank, "^[0-9]{6}$") || !Regex.IsMatch(number, "^[0-9]{6,19}$") || name.Length < 5 || name.Length > 50)
            throw new BusinessRuleException("BANK_TRANSFER_NOT_CONFIGURED", "Receiving bank details are not configured.", statusCode: 503);
        string content = "SCMS" + invoice.Id.ToString(CultureInfo.InvariantCulture);
        string amount = invoice.Amount.ToString("0", CultureInfo.InvariantCulture);
        return new BankTransferInstructionsAPIViewModel
        {
            InvoiceId = invoice.Id, Amount = invoice.Amount, BankId = bank, AccountNumber = number,
            AccountName = name, TransferContent = content,
            QrImageUrl = $"https://img.vietqr.io/image/{bank}-{number}-compact2.svg?amount={amount}&addInfo={Uri.EscapeDataString(content)}&accountName={Uri.EscapeDataString(name)}"
        };
    }
}
