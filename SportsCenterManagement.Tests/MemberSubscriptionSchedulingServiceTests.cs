using APIViewModel.MemberSubscription;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.EmailService;
using Services.MemberSubscriptionService;
using Services.MembershipTermsService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class MemberSubscriptionServiceTestsScheduling
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public void SamePackage_RenewsAfterLatestEndAtFullPrice()
    {
        MemberSubscription latest = Current(packageId: 1, price: 500_000m);
        MembershipPackage package = Package(1, 500_000m);

        SubscriptionOrderQuote quote = MemberSubscriptionService.ResolveSubscriptionOrderQuote(latest, package, Today);

        Assert.Equal(new SubscriptionOrderQuote("RENEW", latest.EndDate.AddDays(1), 500_000m), quote);
    }

    [Fact]
    public void HigherPrice_UpgradesAfterLatestEndAtFullPrice()
    {
        MemberSubscription latest = Current(packageId: 1, price: 500_000m);
        MembershipPackage package = Package(2, 900_000m);

        SubscriptionOrderQuote quote = MemberSubscriptionService.ResolveSubscriptionOrderQuote(latest, package, Today);

        Assert.Equal(new SubscriptionOrderQuote("UPGRADE", latest.EndDate.AddDays(1), 900_000m), quote);
    }

    [Fact]
    public void LowerPrice_DowngradesAfterLatestEndAtFullPrice()
    {
        MemberSubscription latest = Current(packageId: 1, price: 900_000m);
        MembershipPackage package = Package(2, 500_000m);

        SubscriptionOrderQuote quote = MemberSubscriptionService.ResolveSubscriptionOrderQuote(latest, package, Today);

        Assert.Equal(new SubscriptionOrderQuote("DOWNGRADE", latest.EndDate.AddDays(1), 500_000m), quote);
    }

    [Fact]
    public void DifferentPackageSamePrice_IsChangeAfterLatestEnd()
    {
        MemberSubscription latest = Current(packageId: 1, price: 500_000m);
        MembershipPackage package = Package(2, 500_000m);

        SubscriptionOrderQuote quote = MemberSubscriptionService.ResolveSubscriptionOrderQuote(latest, package, Today);

        Assert.Equal(new SubscriptionOrderQuote("CHANGE", latest.EndDate.AddDays(1), 500_000m), quote);
    }

    [Fact]
    public void ExpiredSamePackage_IsRenewToday_ExpiredDifferentPackageIsRegisterToday()
    {
        MemberSubscription expired = Current(packageId: 1, price: 500_000m);
        expired.EndDate = Today.AddDays(-1);

        Assert.Equal(new SubscriptionOrderQuote("RENEW", Today, 500_000m),
            MemberSubscriptionService.ResolveSubscriptionOrderQuote(expired, Package(1, 500_000m), Today));
        Assert.Equal(new SubscriptionOrderQuote("REGISTER", Today, 900_000m),
            MemberSubscriptionService.ResolveSubscriptionOrderQuote(expired, Package(2, 900_000m), Today));
    }

    [Fact]
    public async Task OrderWithoutCurrentTerms_IsRejectedWithoutWrites()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = new(new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        Seed(context);
        await context.SaveChangesAsync();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BusinessSettings:TimeZoneId"] = "UTC" }).Build();
        MemberSubscriptionService service = new(
            context,
            configuration,
            new PasswordHashService(),
            new NoopEmailService(),
            new MembershipTermsService(context),
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero)));

        (RegisterSubscriptionResult selfResult, _) = await service.RegisterOrRenewAsync(
            "member", new RegisterMemberSubscriptionAPIViewModel { PackageId = 1, PaymentMethod = "CARD" });
        (CounterRegisterResult counterResult, _, _) = await service.CounterRegisterOrRenewAsync(
            "staff", new CounterRegisterSubscriptionAPIViewModel
            { MemberAccountId = "member", PackageId = 1, PaymentMethod = "CASH" });

        Assert.Equal(RegisterSubscriptionResult.TermsAcceptanceRequired, selfResult);
        Assert.Equal(CounterRegisterResult.TermsAcceptanceRequired, counterResult);
        Assert.Empty(context.MemberSubscriptions);
        Assert.Empty(context.MembershipInvoices);
    }

    [Fact]
    public async Task OrderSnapshotsRestrictedDisciplines_AndMarksUnrestrictedPackage()
    {
        await using SchedulingFixture restricted = await SchedulingFixture.CreateAsync();
        Discipline yoga = new() { Id = 10, Name = "Yoga", IsActive = true, CreatedAt = DateTime.UtcNow };
        restricted.Context.MembershipPackageSports.Add(new MembershipPackageSport
        {
            PackageId = 1, DisciplineId = yoga.Id, Discipline = yoga, CreatedAt = DateTime.UtcNow
        });
        AcceptTerms(restricted.Context);
        await restricted.Context.SaveChangesAsync();

        Assert.Equal(RegisterSubscriptionResult.Success, (await restricted.Service.RegisterOrRenewAsync(
            "member", new RegisterMemberSubscriptionAPIViewModel { PackageId = 1, PaymentMethod = "CARD" })).Result);
        MemberSubscription restrictedOrder = await restricted.Context.MemberSubscriptions
            .Include(item => item.SubscriptionDisciplines).SingleAsync();
        Assert.True(restrictedOrder.HasDisciplineRestrictions);
        Assert.Equal("Yoga", Assert.Single(restrictedOrder.SubscriptionDisciplines).DisciplineName);

        await using SchedulingFixture unrestricted = await SchedulingFixture.CreateAsync();
        AcceptTerms(unrestricted.Context);
        await unrestricted.Context.SaveChangesAsync();
        Assert.Equal(RegisterSubscriptionResult.Success, (await unrestricted.Service.RegisterOrRenewAsync(
            "member", new RegisterMemberSubscriptionAPIViewModel { PackageId = 1, PaymentMethod = "CARD" })).Result);
        MemberSubscription unrestrictedOrder = await unrestricted.Context.MemberSubscriptions
            .Include(item => item.SubscriptionDisciplines).SingleAsync();
        Assert.False(unrestrictedOrder.HasDisciplineRestrictions);
        Assert.Empty(unrestrictedOrder.SubscriptionDisciplines);
    }

    [Fact]
    public async Task BusinessDateUsesConfiguredTimeZone()
    {
        await using SchedulingFixture fixture = await SchedulingFixture.CreateAsync(
            "SE Asia Standard Time",
            new DateTimeOffset(2026, 11, 11, 18, 0, 0, TimeSpan.Zero));
        AcceptTerms(fixture.Context);
        await fixture.Context.SaveChangesAsync();

        (_, MemberSubscriptionDetailAPIViewModel? data) = await fixture.Service.RegisterOrRenewAsync(
            "member", new RegisterMemberSubscriptionAPIViewModel { PackageId = 1, PaymentMethod = "CARD" });

        Assert.Equal(new DateOnly(2026, 11, 12), data!.StartDate);
    }

    private static MemberSubscription Current(int packageId, decimal price) => new()
    {
        PackageId = packageId,
        PackagePrice = price,
        StartDate = Today.AddDays(-10),
        EndDate = Today.AddDays(20),
        PackageName = "Current",
        Benefits = "[]",
        DurationMonths = 1,
        Kind = "REGISTER",
        Status = "CONFIRMED",
        MemberId = "member"
    };

    private static MembershipPackage Package(int id, decimal price) => new()
    {
        Id = id,
        Name = "Package " + id,
        Price = price,
        DurationMonths = 1,
        Benefits = "[]",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static void Seed(SportsCenterManagementContext context)
    {
        Role memberRole = new() { Id = 1, Name = "Member" };
        Role staffRole = new() { Id = 2, Name = "Receptionist" };
        Account memberAccount = new()
        {
            Id = "member", Email = "member@example.com", PasswordHash = "unused", Status = "Active",
            Role = memberRole, RoleId = memberRole.Id, CreatedAt = DateTime.UtcNow
        };
        memberAccount.Member = new Member
        {
            AccountId = memberAccount.Id, Account = memberAccount, MemberCode = "MEM001",
            FullName = "Member", CreatedAt = DateTime.UtcNow
        };
        Account staff = new()
        {
            Id = "staff", Email = "staff@example.com", PasswordHash = "unused", Status = "Active",
            Role = staffRole, RoleId = staffRole.Id, CreatedAt = DateTime.UtcNow,
            Receptionist = new Receptionist { AccountId = "staff", FullName = "Staff", CreatedAt = DateTime.UtcNow }
        };
        context.Accounts.AddRange(memberAccount, staff);
        context.MembershipPackages.Add(Package(1, 500_000m));
    }

    private static void AcceptTerms(SportsCenterManagementContext context)
    {
        context.MembershipTermAcceptances.Add(new MembershipTermAcceptance
        {
            MemberId = "member",
            TermsVersion = MembershipTermsCatalog.CurrentVersion,
            ContentHash = MembershipTermsCatalog.CurrentHash,
            AcceptanceChannel = "MEMBER_WEB",
            AcceptedAtUtc = DateTime.UtcNow
        });
    }

    private sealed class SchedulingFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public SportsCenterManagementContext Context { get; }
        public MemberSubscriptionService Service { get; }

        private SchedulingFixture(
            SqliteConnection connection,
            SportsCenterManagementContext context,
            MemberSubscriptionService service)
        {
            _connection = connection;
            Context = context;
            Service = service;
        }

        public static async Task<SchedulingFixture> CreateAsync(
            string timeZoneId = "UTC",
            DateTimeOffset? utcNow = null)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            SportsCenterManagementContext context = new(new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            Seed(context);
            await context.SaveChangesAsync();
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["BusinessSettings:TimeZoneId"] = timeZoneId }).Build();
            MemberSubscriptionService service = new(
                context, configuration, new PasswordHashService(), new NoopEmailService(),
                new MembershipTermsService(context),
                new FixedTimeProvider(utcNow ?? new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero)));
            return new SchedulingFixture(connection, context, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class NoopEmailService : IEmailService
    {
        public bool IsConfigured => false;
        public Task SendPasswordChangeOtpAsync(string recipientEmail, string otp, int expiresInMinutes) => Task.CompletedTask;
        public Task SendMemberAccountWelcomeAsync(string recipientEmail, string fullName, string initialPassword) => Task.CompletedTask;
        public Task SendEmailVerificationOtpAsync(string recipientEmail, string otp, string purpose, int expiresInMinutes) => Task.CompletedTask;
    }
}
