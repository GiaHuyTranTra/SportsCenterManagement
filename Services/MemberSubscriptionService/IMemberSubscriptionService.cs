using System.Threading.Tasks;
using APIViewModel.MembershipInvoice;
using APIViewModel.Member;
using APIViewModel.MemberSubscription;

namespace Services.MemberSubscriptionService;

public enum RegisterSubscriptionResult
{
    Success,
    InvalidPackageId,
    PackageNotFound,
    PackageInactive,
    PendingOrderExists,
    SubscriptionSuspended,
    UpcomingSubscriptionExists,
    MemberNotFound,
    MemberInactive,
    MemberLocked,
    TermsAcceptanceRequired,
    InvalidPaymentMethod,
    ConcurrencyConflict
}

public enum CounterRegisterResult
{
    Success,
    PendingOrderExists,
    SubscriptionSuspended,
    UpcomingSubscriptionExists,
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
    TermsAcceptanceRequired,
    InvoiceNumberCollision,
    DataIntegrityViolation,
    ConcurrencyConflict
}

public enum CounterCreateMemberResult
{
    Success,
    InvalidData,
    DuplicateEmail,
    DuplicatePhone,
    StaffNotFound,
    StaffInactive,
    StaffLocked,
    StaffRoleNotAllowed,
    MemberRoleMissing,
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

    Task<(CounterCreateMemberResult Result, CounterCreateMemberResponseAPIViewModel? Data)>
        CreateMemberAccountAtCounterAsync(
            string staffAccountId,
            CounterCreateMemberAPIViewModel request);
}
