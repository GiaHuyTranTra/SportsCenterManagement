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
        MemberService service = new MemberService(context, passwordHashService);

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(CreateRequest());

        Assert.Equal(CreateManagedMemberResult.Success, result);
        Assert.NotNull(data);
        Assert.StartsWith("MB", data.Member.MemberCode);
        Account savedAccount = await context.Accounts.SingleAsync();
        Assert.True(passwordHashService.VerifyPassword(data.InitialPassword, savedAccount.PasswordHash));
    }

    [Fact]
    public async Task CreateManagedMemberAsync_WithDuplicateEmail_WritesNothing()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("existing-account", "MB001", "existing@example.com", "0900000001", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);
        CreateManagedMemberAPIViewModel request = CreateRequest();
        request.Email = " EXISTING@example.com ";

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(request);

        Assert.Equal(CreateManagedMemberResult.DuplicateEmail, result);
        Assert.Null(data);
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(1, await context.Members.CountAsync());
    }

    [Fact]
    public async Task CreateManagedMemberAsync_WithDuplicatePhone_WritesNothing()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("existing-account", "MB001", "existing@example.com", "0912345678", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(CreateRequest());

        Assert.Equal(CreateManagedMemberResult.DuplicatePhone, result);
        Assert.Null(data);
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(1, await context.Members.CountAsync());
    }

    [Fact]
    public async Task UpdateManagedMemberAsync_NormalizesAccountAndProfileFields()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("member-account", "MB001", "old@example.com", "0900000001", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        (UpdateManagedMemberResult result, MemberDetailAPIViewModel? data) =
            await service.UpdateManagedMemberAsync(
                "member-account",
                new UpdateManagedMemberAPIViewModel
                {
                    FullName = "  Long Updated  ",
                    Email = "  LONG.UPDATED@EXAMPLE.COM  ",
                    Phone = " 0987654321 ",
                    DateOfBirth = new DateOnly(2001, 2, 3),
                    IsActive = false
                });

        Assert.Equal(UpdateManagedMemberResult.Success, result);
        Assert.NotNull(data);
        Assert.Equal("Long Updated", data.FullName);
        Assert.Equal("long.updated@example.com", data.Email);
        Assert.Equal("0987654321", data.Phone);
        Assert.Equal("Inactive", data.Status);
    }

    [Fact]
    public async Task UpdateManagedMemberAsync_WithAnotherAccountsEmail_ReturnsDuplicateEmail()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("member-account", "MB001", "member@example.com", "0900000001", memberRole));
        context.Add(CreateMember("other-account", "MB002", "other@example.com", "0900000002", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        (UpdateManagedMemberResult result, MemberDetailAPIViewModel? data) =
            await service.UpdateManagedMemberAsync(
                "member-account",
                new UpdateManagedMemberAPIViewModel
                {
                    FullName = "Member Name",
                    Email = "OTHER@example.com",
                    Phone = "0900000001",
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    IsActive = true
                });

        Assert.Equal(UpdateManagedMemberResult.DuplicateEmail, result);
        Assert.Null(data);
        Assert.Equal("member@example.com", (await context.Accounts.FindAsync("member-account"))?.Email);
    }

    [Fact]
    public async Task SoftDeleteMemberAsync_RetainsMemberAndMarksAccountDeleted()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("member-account", "MB001", "member@example.com", "0900000001", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        DeleteMemberResult result = await service.SoftDeleteMemberAsync("member-account");

        Assert.Equal(DeleteMemberResult.Success, result);
        Account account = await context.Accounts.SingleAsync();
        Assert.Equal("Inactive", account.Status);
        Assert.NotNull(account.DeletedAt);
        Assert.Equal(1, await context.Members.CountAsync());
    }

    [Fact]
    public async Task MemberQueries_ExcludeSoftDeletedAccounts()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("active-account", "MB001", "active@example.com", "0900000001", memberRole));
        Account deleted = CreateMember("deleted-account", "MB002", "deleted@example.com", "0900000002", memberRole);
        deleted.DeletedAt = DateTime.UtcNow;
        deleted.Status = "Inactive";
        context.Add(deleted);
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        PagedMemberResultAPIViewModel list = await service.GetMembersAsync(1, 20, null, null);
        MemberDetailAPIViewModel? detail = await service.GetMemberByIdAsync("deleted-account");
        List<MemberSearchAPIViewModel> search = await service.QuickSearchMembersAsync("deleted");

        Assert.Single(list.Items);
        Assert.Equal("active-account", list.Items[0].AccountId);
        Assert.Null(detail);
        Assert.Empty(search);
    }

    [Fact]
    public async Task GetMembersAsync_SearchesMemberCodeAndCapsPageSizeAtTwenty()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        for (int index = 1; index <= 25; index++)
        {
            context.Add(CreateMember(
                $"account-{index}",
                $"MB{index:000}",
                $"person{index}@example.com",
                $"09{index:00000000}",
                memberRole));
        }
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        PagedMemberResultAPIViewModel result = await service.GetMembersAsync(1, 50, "MB", null);

        Assert.Equal(25, result.TotalItems);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(20, result.Items.Count);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_CountsInclusiveDaysAndWarnsBelowSeven()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        DateOnly today = fixture.Today;
        Member member = fixture.AddMember("account-1", "MEM001");
        fixture.AddSubscription(member, "Monthly", today, today.AddDays(5), "CONFIRMED");
        fixture.AddSubscription(
            member,
            "Quarterly",
            today.AddDays(6),
            today.AddMonths(3),
            "CONFIRMED");
        await fixture.Context.SaveChangesAsync();

        List<MembershipStatusAPIViewModel> rows =
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL");

        Assert.Equal("ACTIVE", rows[0].Status);
        Assert.Equal(6, rows[0].RemainingDays);
        Assert.True(rows[0].ExpiringSoon);
        Assert.Equal("Quarterly", rows[0].UpcomingPackageName);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_CurrentSuspensionHasPriority()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        Member member = fixture.AddMember("account-1", "MEM001");
        MemberSubscription subscription = fixture.AddSubscription(
            member,
            "Monthly",
            fixture.Today.AddDays(-2),
            fixture.Today.AddDays(10),
            "CONFIRMED");
        subscription.IsSuspended = true;
        subscription.SuspensionReason = "Medical hold";
        await fixture.Context.SaveChangesAsync();

        MembershipStatusAPIViewModel row = Assert.Single(
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("SUSPENDED", row.Status);
        Assert.Equal("Medical hold", row.SuspensionReason);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_UsesLatestExpiredHistory()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        Member member = fixture.AddMember("account-1", "MEM001");
        fixture.AddSubscription(
            member,
            "Old Package",
            fixture.Today.AddMonths(-3),
            fixture.Today.AddMonths(-2),
            "CONFIRMED");
        fixture.AddSubscription(
            member,
            "Latest Package",
            fixture.Today.AddMonths(-1),
            fixture.Today.AddDays(-1),
            "CONFIRMED");
        await fixture.Context.SaveChangesAsync();

        MembershipStatusAPIViewModel row = Assert.Single(
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("EXPIRED", row.Status);
        Assert.Equal("Latest Package", row.PackageName);
        Assert.Equal(fixture.Today.AddDays(-1), row.EndDate);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_FutureConfirmedAccessIsUpcoming()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        Member member = fixture.AddMember("account-1", "MEM001");
        fixture.AddSubscription(
            member,
            "Future Package",
            fixture.Today.AddDays(3),
            fixture.Today.AddMonths(1),
            "CONFIRMED");
        await fixture.Context.SaveChangesAsync();

        MembershipStatusAPIViewModel row = Assert.Single(
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("UPCOMING", row.Status);
        Assert.Equal("Future Package", row.PackageName);
        Assert.Equal(0, row.RemainingDays);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_PendingOnlyOrderIsPendingPayment()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        Member member = fixture.AddMember("account-1", "MEM001");
        fixture.AddSubscription(
            member,
            "Pending Package",
            fixture.Today,
            fixture.Today.AddMonths(1),
            "PENDING_PAYMENT");
        await fixture.Context.SaveChangesAsync();

        MembershipStatusAPIViewModel row = Assert.Single(
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("PENDING_PAYMENT", row.Status);
        Assert.Equal("Pending Package", row.PackageName);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_MemberWithoutSubscriptionIsNone()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        fixture.AddMember("account-1", "MEM001");
        await fixture.Context.SaveChangesAsync();

        MembershipStatusAPIViewModel row = Assert.Single(
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL"));

        Assert.Equal("NONE", row.Status);
        Assert.Null(row.SubscriptionId);
    }

    [Theory]
    [InlineData("MEM001", "account-1")]
    [InlineData("Search Name", "account-1")]
    [InlineData("search@example.com", "account-1")]
    [InlineData("0912345678", "account-1")]
    public async Task GetMembershipStatusesAsync_SearchesMemberIdentity(
        string search,
        string expectedAccountId)
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        fixture.AddMember(
            expectedAccountId,
            "MEM001",
            "Search Name",
            "search@example.com",
            "0912345678");
        fixture.AddMember("account-2", "MEM002", "Other Name", "other@example.com", "0900000002");
        await fixture.Context.SaveChangesAsync();

        MembershipStatusAPIViewModel row = Assert.Single(
            await fixture.Service.GetMembershipStatusesAsync(search, "ALL"));

        Assert.Equal(expectedAccountId, row.AccountId);
    }

    [Theory]
    [InlineData("ALL", 7)]
    [InlineData("ACTIVE", 2)]
    [InlineData("EXPIRING", 1)]
    [InlineData("EXPIRED", 1)]
    [InlineData("SUSPENDED", 1)]
    [InlineData("UPCOMING", 1)]
    [InlineData("PENDING_PAYMENT", 1)]
    [InlineData("NONE", 1)]
    public async Task GetMembershipStatusesAsync_AppliesEveryFilter(
        string filter,
        int expectedCount)
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        Member active = fixture.AddMember("active", "MEM001");
        fixture.AddSubscription(active, "Active", fixture.Today, fixture.Today.AddDays(20), "CONFIRMED");
        Member expiring = fixture.AddMember("expiring", "MEM002");
        fixture.AddSubscription(expiring, "Expiring", fixture.Today, fixture.Today.AddDays(5), "CONFIRMED");
        Member expired = fixture.AddMember("expired", "MEM003");
        fixture.AddSubscription(expired, "Expired", fixture.Today.AddMonths(-1), fixture.Today.AddDays(-1), "CONFIRMED");
        Member suspended = fixture.AddMember("suspended", "MEM004");
        MemberSubscription suspendedSubscription = fixture.AddSubscription(
            suspended,
            "Suspended",
            fixture.Today,
            fixture.Today.AddDays(20),
            "CONFIRMED");
        suspendedSubscription.IsSuspended = true;
        Member upcoming = fixture.AddMember("upcoming", "MEM005");
        fixture.AddSubscription(upcoming, "Upcoming", fixture.Today.AddDays(2), fixture.Today.AddMonths(1), "CONFIRMED");
        Member pending = fixture.AddMember("pending", "MEM006");
        fixture.AddSubscription(pending, "Pending", fixture.Today, fixture.Today.AddMonths(1), "PENDING_PAYMENT");
        fixture.AddMember("none", "MEM007");
        await fixture.Context.SaveChangesAsync();

        List<MembershipStatusAPIViewModel> rows =
            await fixture.Service.GetMembershipStatusesAsync(null, filter);

        Assert.Equal(expectedCount, rows.Count);
    }

    [Fact]
    public async Task GetMembershipStatusesAsync_ExcludesSoftDeletedMembers()
    {
        await using MemberServiceTestFixture fixture = await MemberServiceTestFixture.CreateAsync();
        fixture.AddMember("active", "MEM001");
        Member deletedMember = fixture.AddMember("deleted", "MEM002");
        deletedMember.Account.DeletedAt = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();

        List<MembershipStatusAPIViewModel> rows =
            await fixture.Service.GetMembershipStatusesAsync(null, "ALL");

        Assert.Single(rows);
        Assert.Equal("active", rows[0].AccountId);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static MemberService CreateService(SportsCenterManagementContext context)
    {
        return new MemberService(context, new PasswordHashService());
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

    private static Account CreateMember(
        string accountId,
        string memberCode,
        string email,
        string phone,
        Role role)
    {
        DateTime createdAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        return new Account
        {
            Id = accountId,
            Email = email,
            Phone = phone,
            PasswordHash = "unused",
            Status = "Active",
            CreatedAt = createdAt,
            RoleId = role.Id,
            Role = role,
            Member = new Member
            {
                AccountId = accountId,
                MemberCode = memberCode,
                FullName = "Member Name",
                DateOfBirth = new DateOnly(2000, 1, 1),
                CreatedAt = createdAt
            }
        };
    }

    private sealed class MemberServiceTestFixture : IAsyncDisposable
    {
        private readonly Role _memberRole;

        private MemberServiceTestFixture(
            SportsCenterManagementContext context,
            MemberService service,
            DateOnly today,
            Role memberRole)
        {
            Context = context;
            Service = service;
            Today = today;
            _memberRole = memberRole;
        }

        public SportsCenterManagementContext Context { get; }

        public MemberService Service { get; }

        public DateOnly Today { get; }

        public static async Task<MemberServiceTestFixture> CreateAsync()
        {
            SportsCenterManagementContext context = CreateContext();
            Role memberRole = CreateMemberRole();
            context.Roles.Add(memberRole);
            await context.SaveChangesAsync();
            Dictionary<string, string?> settings = new Dictionary<string, string?>
            {
                ["BusinessSettings:TimeZoneId"] = "SE Asia Standard Time"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateOnly today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
            MemberService service = new MemberService(
                context,
                new PasswordHashService(),
                configuration);
            return new MemberServiceTestFixture(context, service, today, memberRole);
        }

        public Member AddMember(
            string accountId,
            string memberCode,
            string fullName = "Member Name",
            string? email = null,
            string? phone = null)
        {
            Account account = CreateMember(
                accountId,
                memberCode,
                email ?? accountId + "@example.com",
                phone ?? "09" + Context.Accounts.Local.Count.ToString("00000000"),
                _memberRole);
            account.Member!.FullName = fullName;
            Context.Accounts.Add(account);
            return account.Member;
        }

        public MemberSubscription AddSubscription(
            Member member,
            string packageName,
            DateOnly startDate,
            DateOnly endDate,
            string status)
        {
            MemberSubscription subscription = new MemberSubscription
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
            Context.MemberSubscriptions.Add(subscription);
            return subscription;
        }

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
        }
    }
}
