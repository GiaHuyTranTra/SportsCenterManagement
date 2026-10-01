using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using APIViewModel.MemberSubscription;

namespace Services.MemberSubscriptionService;

public enum RegisterSubscriptionResult
{
    Success,
    InvalidPackageId,
    PackageNotFound,
    PackageInactive,
    PendingOrderExists,
    MemberNotFound,
    MemberInactive,
    MemberLocked,
    InvalidPaymentMethod,
    ConcurrencyConflict
}

public enum CounterRegisterResult
{
    Success,
    PendingOrderExists,
    MemberNotFound,
    MemberInactive,
    MemberLocked,
    PackageNotFound,
    PackageInactive,
    InvalidPackageId,
    InvalidPaymentMethod,
    InvalidMemberAccountId,
    StaffNotFound,
    StaffInactive,
    StaffLocked,
    StaffRoleNotAllowed,
    InvoiceNumberCollision,
    DataIntegrityViolation,
    ConcurrencyConflict
}

public enum CounterRegisterMemberResult
{
    Success,
    InvalidData,
    DuplicateEmail,
    DuplicatePhone,
    StaffNotFound,
    StaffInactive,
    StaffLocked,
    StaffRoleNotAllowed,
    PackageNotFound,
    PackageInactive,
    MemberRoleMissing,
    PendingOrderExists,
    PriceChanged,
    InvalidPaymentMethod,
    InvoiceNumberCollision,
    ConcurrencyConflict
}

public interface IMemberSubscriptionService
{
    Task<(RegisterSubscriptionResult Result, MemberSubscriptionDetailAPIViewModel? Data)> RegisterOrRenewAsync(
        string accountId,
        RegisterMemberSubscriptionAPIViewModel request);

    Task<(CounterRegisterResult Result, MembershipReceiptAPIViewModel? Receipt, PendingOrderConflictResponseAPIViewModel? PendingInfo)> CounterRegisterOrRenewAsync(
        string staffAccountId,
        CounterRegisterSubscriptionAPIViewModel request);

    Task<(CounterRegisterMemberResult Result, CounterRegisterMemberResponseAPIViewModel? Data)>
        RegisterMemberAtCounterAsync(
            string staffAccountId,
            CounterRegisterMemberAPIViewModel request);
}
