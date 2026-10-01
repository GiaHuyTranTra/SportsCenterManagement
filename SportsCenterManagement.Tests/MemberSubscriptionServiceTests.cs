using APIViewModel.MemberSubscription;
using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.EmailService;
using Services.MemberSubscriptionService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class MemberSubscriptionServiceTests
{
    [Fact]
    public async Task RegisterMemberAtCounterAsync_CreatesPendingOrderWithServerCredentials()
    {
        await using CounterRegistrationFixture fixture =
            await CounterRegistrationFixture.CreateAsync();

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Manager.Id,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.Success, result);
        Assert.NotNull(data);
        Assert.Equal("PENDING_PAYMENT", data.Receipt.SubscriptionStatus);
        Assert.Equal("PENDING_PAYMENT", data.Receipt.InvoiceStatus);
        Assert.Null(data.Receipt.PaidAt);
        Assert.Null(data.Receipt.PaidByStaffId);
        Assert.StartsWith("MB", data.Member.MemberCode);

        Account account = await fixture.Context.Accounts
            .SingleAsync(candidate => candidate.Id == data.Member.AccountId);
        Assert.True(new PasswordHashService().VerifyPassword(
            data.InitialPassword,
            account.PasswordHash));
        Assert.Single(await fixture.Context.Members.ToListAsync());
        Assert.Single(await fixture.Context.MemberSubscriptions.ToListAsync());
        Assert.Single(await fixture.Context.MembershipInvoices.ToListAsync());
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WhenInvoiceInsertFails_RollsBackMember()
    {
        await using CounterRegistrationFixture fixture =
            await CounterRegistrationFixture.CreateAsync();
        await fixture.Context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_invoice BEFORE INSERT ON MembershipInvoice " +
            "BEGIN SELECT RAISE(ABORT, 'forced invoice failure'); END;");

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Manager.Id,
                fixture.ValidRequest()));

        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(1, await fixture.Context.Accounts.CountAsync());
        Assert.Empty(await fixture.Context.Members.ToListAsync());
        Assert.Empty(await fixture.Context.MemberSubscriptions.ToListAsync());
        Assert.Empty(await fixture.Context.MembershipInvoices.ToListAsync());
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WhenWelcomeEmailFails_KeepsPendingOrder()
    {
        await using CounterRegistrationFixture fixture =
            await CounterRegistrationFixture.CreateAsync();
        fixture.Email.ThrowOnWelcome = true;

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Manager.Id,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.Success, result);
        Assert.Equal("FAILED", data?.EmailDelivery);
        Assert.Equal("PENDING_PAYMENT", data?.Receipt.SubscriptionStatus);
        Assert.Single(await fixture.Context.Members.ToListAsync());
    }

    [Fact]
    public async Task CounterRegisterOrRenewAsync_CreatesPendingOrderBeforePayment()
    {
        await using CounterRegistrationFixture fixture =
            await CounterRegistrationFixture.CreateAsync();
        Role memberRole = await fixture.Context.Roles
            .SingleAsync(role => role.Name == "Member");
        DateTime createdAt = DateTime.UtcNow;
        Account memberAccount = new Account
        {
            Id = "existing-member",
            Email = "existing.member@example.com",
            Phone = "0900000002",
            PasswordHash = "unused",
            Status = "Active",
            CreatedAt = createdAt,
            RoleId = memberRole.Id,
            Role = memberRole
        };
        memberAccount.Member = new Member
        {
            AccountId = memberAccount.Id,
            MemberCode = "MEM001",
            FullName = "Existing Member",
            CreatedAt = createdAt,
            Account = memberAccount
        };
        fixture.Context.Accounts.Add(memberAccount);
        await fixture.Context.SaveChangesAsync();
        CounterRegisterSubscriptionAPIViewModel request = new CounterRegisterSubscriptionAPIViewModel
        {
            MemberAccountId = memberAccount.Id,
            PackageId = fixture.Package.Id,
            PaymentMethod = "CASH"
        };

        (CounterRegisterResult result, MembershipReceiptAPIViewModel? receipt, PendingOrderConflictResponseAPIViewModel? pendingInfo) =
            await fixture.Service.CounterRegisterOrRenewAsync(fixture.Manager.Id, request);

        Assert.Equal(CounterRegisterResult.Success, result);
        Assert.Null(pendingInfo);
        Assert.Equal("PENDING_PAYMENT", receipt?.SubscriptionStatus);
        Assert.Equal("PENDING_PAYMENT", receipt?.InvoiceStatus);
        Assert.Null(receipt?.PaidAt);
        Assert.Null(receipt?.PaidByStaffId);
        MembershipInvoice invoice = await fixture.Context.MembershipInvoices.SingleAsync();
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Null(invoice.PaidBy);
    }

    private sealed class CounterRegistrationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CounterRegistrationFixture(
            SqliteConnection connection,
            SportsCenterManagementContext context,
            MemberSubscriptionService service,
            FakeEmailService email,
            Account manager,
            MembershipPackage package)
        {
            _connection = connection;
            Context = context;
            Service = service;
            Email = email;
            Manager = manager;
            Package = package;
        }

        public SportsCenterManagementContext Context { get; }

        public MemberSubscriptionService Service { get; }

        public FakeEmailService Email { get; }

        public Account Manager { get; }

        public MembershipPackage Package { get; }

        public static async Task<CounterRegistrationFixture> CreateAsync()
        {
            SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<SportsCenterManagementContext> options =
                new DbContextOptionsBuilder<SportsCenterManagementContext>()
                    .UseSqlite(connection)
                    .Options;
            SportsCenterManagementContext context = new SportsCenterManagementContext(options);
            await context.Database.EnsureCreatedAsync();

            DateTime createdAt = DateTime.UtcNow;
            Role managerRole = new Role { Id = 1, Name = "CenterManager" };
            Role memberRole = new Role { Id = 3, Name = "Member" };
            Account manager = new Account
            {
                Id = "manager-account",
                Email = "manager@example.com",
                Phone = "0900000001",
                PasswordHash = "unused",
                Status = "Active",
                CreatedAt = createdAt,
                RoleId = managerRole.Id,
                Role = managerRole
            };
            MembershipPackage package = new MembershipPackage
            {
                Id = 1,
                Name = "Monthly",
                Price = 500000m,
                DurationMonths = 1,
                Benefits = "[\"Gym access\"]",
                IsActive = true,
                CreatedAt = createdAt
            };

            context.Roles.AddRange(managerRole, memberRole);
            context.Accounts.Add(manager);
            context.MembershipPackages.Add(package);
            await context.SaveChangesAsync();

            Dictionary<string, string?> settings = new Dictionary<string, string?>
            {
                ["BusinessSettings:TimeZoneId"] = "UTC"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            FakeEmailService email = new FakeEmailService();
            MemberSubscriptionService service = new MemberSubscriptionService(
                context,
                configuration,
                new PasswordHashService(),
                email);

            return new CounterRegistrationFixture(
                connection,
                context,
                service,
                email,
                manager,
                package);
        }

        public CounterRegisterMemberAPIViewModel ValidRequest()
        {
            return new CounterRegisterMemberAPIViewModel
            {
                FullName = "Counter Member",
                Email = "counter.member@example.com",
                Phone = "0912345678",
                DateOfBirth = new DateOnly(2000, 1, 2),
                PackageId = Package.Id,
                ExpectedPrice = Package.Price,
                PaymentMethod = "CASH"
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool IsConfigured { get; set; } = true;

        public bool ThrowOnWelcome { get; set; }

        public Task SendPasswordChangeOtpAsync(
            string recipientEmail,
            string otp,
            int expiresInMinutes)
        {
            return Task.CompletedTask;
        }

        public Task SendEmailVerificationOtpAsync(
            string recipientEmail,
            string otp,
            string purpose,
            int expiresInMinutes)
        {
            return Task.CompletedTask;
        }

        public Task SendMemberWelcomeAsync(
            string recipientEmail,
            string fullName,
            string initialPassword,
            string packageName,
            decimal amount)
        {
            return ThrowOnWelcome
                ? Task.FromException(new InvalidOperationException("SMTP unavailable"))
                : Task.CompletedTask;
        }
    }
}
