using System.Collections.Generic;
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

public enum CancelInvoiceResult
{
    Success,
    InvoiceNotFound,
    InvalidInvoiceState,
    CallerNotFound,
    CallerInactive,
    CallerLocked,
    CallerRoleNotAllowed,
    ConcurrencyConflict
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

    Task<CancelInvoiceResult> CancelPendingInvoiceAsync(
        int invoiceId,
        string callerAccountId);

    Task<List<MembershipReceiptAPIViewModel>> GetInvoicesAsync(
        string staffAccountId,
        string? memberId = null,
        string? status = null,
        string? search = null,
        string? paymentMethod = null);
}
