using APIViewModel.Member;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.MemberService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class MemberServiceTests
{
    [Fact]
    public async Task CreateManagedMemberAsync_GeneratesCodeAndHashedPassword()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Roles.Add(CreateMemberRole());
        await context.SaveChangesAsync();
        PasswordHashService passwordHashService = new PasswordHashService();
        MemberService service = CreateService(context, passwordHashService);

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(CreateRequest());

        Assert.Equal(CreateManagedMemberResult.Success, result);
        Assert.NotNull(data);
        Assert.StartsWith("MB", data.Member.MemberCode);
        Account savedAccount = await context.Accounts.SingleAsync();
        Assert.True(passwordHashService.VerifyPassword(
            data.InitialPassword,
            savedAccount.PasswordHash));
    }

    [Fact]
    public async Task SoftDeleteMemberAsync_RetainsMemberAndHidesItFromQueries()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        Account account = CreateMember("member-account", "MB001", memberRole);
        context.Add(account);
        await context.SaveChangesAsync();
        MemberService service = CreateService(context, new PasswordHashService());

        DeleteMemberResult result = await service.SoftDeleteMemberAsync(account.Id);
        PagedMemberResultAPIViewModel list = await service.GetMembersAsync(1, 20, null, null);

        Assert.Equal(DeleteMemberResult.Success, result);
        Assert.Equal("Inactive", account.Status);
        Assert.NotNull(account.DeletedAt);
        Assert.Equal(1, await context.Members.CountAsync());
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_ReportsSuspensionAndUpcomingPackage()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        Account account = CreateMember("member-account", "MB001", memberRole);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        MemberSubscription current = CreateSubscription(
            account.Member!,
            "Monthly",
            today.AddDays(-2),
            today.AddDays(5),
            "CONFIRMED");
        current.IsSuspended = true;
        current.SuspensionReason = "Medical hold";
        MemberSubscription upcoming = CreateSubscription(
            account.Member!,
            "Quarterly",
            today.AddDays(6),
            today.AddMonths(3),
            "CONFIRMED");
        context.Add(account);
        context.MemberSubscriptions.AddRange(current, upcoming);
        await context.SaveChangesAsync();
        MemberService service = CreateService(context, new PasswordHashService());

        MembershipStatusAPIViewModel row = Assert.Single(
            await service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("SUSPENDED", row.Status);
        Assert.Equal(6, row.RemainingDays);
        Assert.True(row.ExpiringSoon);
        Assert.Equal("Medical hold", row.SuspensionReason);
        Assert.Equal("Quarterly", row.UpcomingPackageName);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_ReportsPendingAndExcludesDeletedMembers()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        Account pending = CreateMember("pending-account", "MB001", memberRole);
        Account deleted = CreateMember("deleted-account", "MB002", memberRole);
        deleted.DeletedAt = DateTime.UtcNow;
        deleted.Status = "Inactive";
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        context.AddRange(pending, deleted);
        context.MemberSubscriptions.Add(CreateSubscription(
            pending.Member!,
            "Pending Package",
            today,
            today.AddMonths(1),
            "PENDING_PAYMENT"));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context, new PasswordHashService());

        MembershipStatusAPIViewModel row = Assert.Single(
            await service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("pending-account", row.AccountId);
        Assert.Equal("PENDING_PAYMENT", row.Status);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static MemberService CreateService(
        SportsCenterManagementContext context,
        PasswordHashService passwordHashService)
    {
        Dictionary<string, string?> settings = new Dictionary<string, string?>
        {
            ["BusinessSettings:TimeZoneId"] = "UTC"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new MemberService(context, passwordHashService, configuration);
    }

    private static Role CreateMemberRole()
    {
        return new Role { Id = 3, Name = "Member" };
    }

    private static CreateManagedMemberAPIViewModel CreateRequest()
    {
        return new CreateManagedMemberAPIViewModel
        {
            FullName = "Long Test Member",
            Email = "long.member@example.com",
            Phone = "0912345678",
            DateOfBirth = new DateOnly(2000, 1, 2),
            IsActive = true
        };
    }

    private static Account CreateMember(string accountId, string memberCode, Role role)
    {
        DateTime createdAt = DateTime.UtcNow;
        Account account = new Account
        {
            Id = accountId,
            Email = accountId + "@example.com",
            Phone = "09" + Math.Abs(accountId.GetHashCode()).ToString("00000000")[..8],
            PasswordHash = "unused",
            Status = "Active",
            CreatedAt = createdAt,
            RoleId = role.Id,
            Role = role
        };
        account.Member = new Member
        {
            AccountId = accountId,
            MemberCode = memberCode,
            FullName = "Member Name",
            DateOfBirth = new DateOnly(2000, 1, 1),
            CreatedAt = createdAt,
            Account = account
        };
        return account;
    }

    private static MemberSubscription CreateSubscription(
        Member member,
        string packageName,
        DateOnly startDate,
        DateOnly endDate,
        string status)
    {
        return new MemberSubscription
        {
            MemberId = member.AccountId,
            PackageId = 1,
            PackageName = packageName,
            PackagePrice = 500000m,
            DurationMonths = 1,
            Benefits = "[]",
            StartDate = startDate,
            EndDate = endDate,
            Kind = "REGISTER",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Member = member
        };
    }
}
