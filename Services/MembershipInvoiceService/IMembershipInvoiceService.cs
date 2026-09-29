using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;

namespace Services.MembershipInvoiceService;

public enum PayInvoiceResult
{
    Success,
    InvoiceNotFound,
    AlreadyPaid,
    InvalidInvoiceState,
    InvalidSubscriptionState,
    MemberNotFound,
    MemberInactive,
    MemberLocked,
    PackageNotFound,
    PackageInactive,
    InvalidPaymentMethod,
    StaffNotFound,
    StaffInactive,
    StaffLocked,
    StaffRoleNotAllowed,
    DataIntegrityViolation,
    ConcurrencyConflict
}

public enum GetReceiptResult
{
    Success,
    InvoiceNotFound,
    ReceiptNotAvailable,
    StaffNotFound,
    StaffInactive,
    StaffLocked,
    StaffRoleNotAllowed,
    DataIntegrityViolation
}

public interface IMembershipInvoiceService
{
    Task<(PayInvoiceResult Result, MembershipReceiptAPIViewModel? Receipt)> PayInvoiceAsync(
        int invoiceId,
        string staffAccountId,
        PayMembershipInvoiceAPIViewModel request);

    Task<(GetReceiptResult Result, MembershipReceiptAPIViewModel? Receipt)> GetReceiptAsync(
        int invoiceId,
        string staffAccountId);
}
