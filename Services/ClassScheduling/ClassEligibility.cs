using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ScheduleQueryService;

namespace Services.ClassScheduling;

public static class ClassEligibility
{
    public static async Task<int> ValidateAsync(SportsCenterManagementContext context,
        string memberId, DateOnly date, int disciplineId)
    {
        bool active = await context.Members.AnyAsync(member => member.AccountId == memberId &&
            member.Account.Role.Name == "Member" && member.Account.Status == "Active" &&
            member.Account.DeletedAt == null && !member.Account.IsLocked);
        if (!active) throw new BusinessRuleException("ACCOUNT_INACTIVE", "Tài khoản thành viên không hoạt động.");

        List<MemberSubscription> subscriptions = await context.MemberSubscriptions.AsNoTracking()
            .Where(item => item.MemberId == memberId && item.Status == "CONFIRMED" &&
                !item.IsSuspended && item.StartDate <= date && item.EndDate >= date)
            .OrderBy(item => item.Id).ToListAsync();
        if (subscriptions.Count == 0)
            throw new BusinessRuleException("MEMBERSHIP_NOT_VALID_FOR_SESSION", "Gói tập không hợp lệ vào ngày học.");

        List<int> packageIds = subscriptions.Select(item => item.PackageId).Distinct().ToList();
        List<MembershipPackageSport> restrictions = await context.MembershipPackageSports.AsNoTracking()
            .Where(item => packageIds.Contains(item.PackageId)).ToListAsync();
        foreach (MemberSubscription subscription in subscriptions)
        {
            if (!restrictions.Any(item => item.PackageId == subscription.PackageId) ||
                restrictions.Any(item => item.PackageId == subscription.PackageId && item.DisciplineId == disciplineId))
                return subscription.Id;
        }
        throw new BusinessRuleException("PACKAGE_NOT_ELIGIBLE", "Gói tập không cho phép tham gia bộ môn này.");
    }
}
