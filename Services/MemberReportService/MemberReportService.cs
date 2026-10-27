using APIViewModel.Report;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Services.MemberReportService;

public class MemberReportService : IMemberReportService
{
    private readonly SportsCenterManagementContext _context;
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _clock;

    public MemberReportService(
        SportsCenterManagementContext context, IConfiguration configuration, TimeProvider? clock = null)
    {
        _context = context;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            configuration["BusinessSettings:TimeZoneId"]
                ?? throw new InvalidOperationException("BusinessSettings:TimeZoneId is not configured."));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<MemberReportAPIViewModel> GetMemberReportAsync(DateOnly? from, DateOnly? to)
    {
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _timeZone).DateTime);
        DateOnly asOf = to ?? today;
        if (asOf >= DateOnly.MaxValue || (from.HasValue && from.Value > asOf))
        {
            throw new ArgumentException("The report date range is invalid.");
        }

        DateTime endUtc = ToUtc(asOf.AddDays(1));
        DateTime? startUtc = from.HasValue ? ToUtc(from.Value) : null;
        IQueryable<Member> members = _context.Members.AsNoTracking().Where(member =>
            member.Account.Role.Name == "Member" && member.Account.DeletedAt == null && member.CreatedAt < endUtc);

        // Match membership-status precedence: current, upcoming, expired, pending, none.
        IQueryable<MemberSnapshot> snapshots = members.Select(member => new MemberSnapshot
        {
            AccountEnabled = member.Account.Status == "Active" && !member.Account.IsLocked,
            CurrentSuspended = member.MemberSubscriptions
                .Where(subscription => subscription.CreatedAt < endUtc && subscription.Status == "CONFIRMED" &&
                    subscription.StartDate <= asOf && subscription.EndDate >= asOf)
                .OrderByDescending(subscription => subscription.StartDate)
                .ThenByDescending(subscription => subscription.Id)
                .Select(subscription => (bool?)subscription.IsSuspended).FirstOrDefault(),
            HasUpcoming = member.MemberSubscriptions.Any(subscription =>
                subscription.CreatedAt < endUtc && subscription.Status == "CONFIRMED" && subscription.StartDate > asOf),
            HasExpired = member.MemberSubscriptions.Any(subscription =>
                subscription.CreatedAt < endUtc && subscription.Status == "CONFIRMED" && subscription.EndDate < asOf),
            HasPending = member.MemberSubscriptions.Any(subscription =>
                subscription.CreatedAt < endUtc && subscription.Status == "PENDING_PAYMENT")
        });

        return new MemberReportAPIViewModel
        {
            From = from, To = asOf, AsOfDate = asOf, TimeZoneId = _timeZone.Id,
            TotalMembers = await members.CountAsync(),
            NewMembers = await members.CountAsync(member => !startUtc.HasValue || member.CreatedAt >= startUtc.Value),
            ActiveMembers = await snapshots.CountAsync(member => member.AccountEnabled && member.CurrentSuspended == false),
            SuspendedMembers = await snapshots.CountAsync(member => member.CurrentSuspended == true),
            UpcomingMembers = await snapshots.CountAsync(member => member.CurrentSuspended == null && member.HasUpcoming),
            ExpiredMembers = await snapshots.CountAsync(member => member.CurrentSuspended == null && !member.HasUpcoming && member.HasExpired),
            PendingPaymentMembers = await snapshots.CountAsync(member => member.CurrentSuspended == null && !member.HasUpcoming && !member.HasExpired && member.HasPending),
            MembersWithoutSubscription = await snapshots.CountAsync(member => member.CurrentSuspended == null && !member.HasUpcoming && !member.HasExpired && !member.HasPending),
            InactiveAccounts = await snapshots.CountAsync(member => !member.AccountEnabled)
        };
    }

    private DateTime ToUtc(DateOnly date)
    {
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _timeZone);
    }

    private sealed class MemberSnapshot
    {
        public bool AccountEnabled { get; set; }
        public bool? CurrentSuspended { get; set; }
        public bool HasUpcoming { get; set; }
        public bool HasExpired { get; set; }
        public bool HasPending { get; set; }
    }
}
