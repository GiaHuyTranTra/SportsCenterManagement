using APIViewModel.MemberSubscription;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.EmailService;
using Services.MemberSubscriptionService;
using Services.PasswordHashService;
using System.Text.Json;

namespace SportsCenterManagement.Tests;

public class MemberSubscriptionServiceTests
{
    [Fact]
    public async Task CounterRegistration_DoesNotReturnCredentialsEvenWhenEmailFails()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        fixture.Email.ThrowOnWelcome = true;
        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(fixture.Receptionist.Id, fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.Success, result);
        string json = JsonSerializer.Serialize(data);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FAILED", data?.EmailDelivery);
    }

    [Fact]
    public async Task CounterRegistration_InvoiceInsertFailureRollsBackAccountMemberAndSubscription()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectInvoice BEFORE INSERT ON MembershipInvoice BEGIN SELECT RAISE(ABORT, 'Injected invoice failure'); END;");

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Service.RegisterMemberAtCounterAsync(
            fixture.Receptionist.Id, fixture.ValidRequest()));

        fixture.Context.ChangeTracker.Clear();
        await AssertNoRegistrationWritesAsync(fixture);
        Assert.Equal(0, fixture.Email.WelcomeMessagesSent);
    }

    [Fact]
    public async Task InvoiceSearch_AppliesFiltersAndStablePaginationWithoutLeakingOwnership()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, fixture.ValidRequest());
        CounterRegisterMemberAPIViewModel second = fixture.ValidRequest();
        second.Email = "second.member@example.com";
        second.Phone = "0912345679";
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, second);
        List<MembershipInvoice> invoices = await fixture.Context.MembershipInvoices.OrderBy(invoice => invoice.Id).ToListAsync();
        foreach (MembershipInvoice invoice in invoices)
        {
            invoice.CreatedAt = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        }
        await fixture.Context.SaveChangesAsync();
        Services.MembershipInvoiceService.MembershipInvoiceService service = CreateInvoiceService(fixture);

        APIViewModel.MembershipInvoice.PagedMembershipInvoiceAPIViewModel firstPage = await service.SearchInvoicesAsync(
            fixture.Manager.Id, null, " pending_payment ", "Counter Member", " cash ", 1, 1);
        APIViewModel.MembershipInvoice.PagedMembershipInvoiceAPIViewModel secondPage = await service.SearchInvoicesAsync(
            fixture.Manager.Id, null, null, null, null, 2, 1);

        Assert.Equal(2, firstPage.TotalItems);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(invoices[1].Id, Assert.Single(firstPage.Items).InvoiceId);
        Assert.Equal(invoices[0].Id, Assert.Single(secondPage.Items).InvoiceId);
        Assert.Empty((await service.SearchInvoicesAsync("member-account", invoices[0].MemberId, null, null, null, 1, 20)).Items);
        Assert.Empty((await service.SearchInvoicesAsync("coach-account", null, null, null, null, 1, 20)).Items);
    }

    [Fact]
    public async Task InvoiceHistory_ListsEveryStateAndOnlyProvidesPaidOwnedReceipt()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, fixture.ValidRequest());
        MembershipInvoice invoice = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        Services.MembershipInvoiceService.MembershipInvoiceService service = CreateInvoiceService(fixture);

        Assert.Equal(Services.MembershipInvoiceService.GetReceiptResult.ReceiptNotAvailable,
            (await service.GetReceiptAsync(invoice.Id, invoice.MemberId)).Result);
        invoice.Status = "PAID";
        invoice.Subscription.Status = "CONFIRMED";
        invoice.PaidAt = DateTime.UtcNow;
        invoice.PaidBy = fixture.Receptionist.Id;
        await fixture.Context.SaveChangesAsync();

        Assert.Equal(Services.MembershipInvoiceService.GetReceiptResult.Success,
            (await service.GetReceiptAsync(invoice.Id, invoice.MemberId)).Result);
        Assert.Single((await service.SearchInvoicesAsync(fixture.Manager.Id, null, "PAID", invoice.InvoiceNumber, "CASH", 1, 20)).Items);
        Assert.Equal(Services.MembershipInvoiceService.GetReceiptResult.InvoiceNotFound,
            (await service.GetReceiptAsync(invoice.Id, "member-account")).Result);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_EmailFailureKeepsCommittedPendingOrder()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();
        fixture.Email.ThrowOnWelcome = true;

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Manager.Id,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.Success, result);
        Assert.Equal("FAILED", data?.EmailDelivery);
        Assert.Single(await fixture.Context.Members.ToListAsync());
        Assert.Equal("PENDING_PAYMENT", data?.Receipt.SubscriptionStatus);
        Assert.Equal("PENDING_PAYMENT", data?.Receipt.InvoiceStatus);
        Assert.Equal("PENDING_PAYMENT", (await fixture.Context.MemberSubscriptions.SingleAsync()).Status);
        Assert.Equal("PENDING_PAYMENT", (await fixture.Context.MembershipInvoices.SingleAsync()).Status);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WithStalePrice_WritesNothing()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();
        CounterRegisterMemberAPIViewModel request = fixture.ValidRequest();
        request.ExpectedPrice = request.ExpectedPrice + 1m;

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, request);

        Assert.Equal(CounterRegisterMemberResult.PriceChanged, result);
        Assert.Null(data);
        await AssertNoRegistrationWritesAsync(fixture);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WithDuplicateEmail_WritesNothing()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();
        CounterRegisterMemberAPIViewModel request = fixture.ValidRequest();
        request.Email = " MANAGER@example.com ";

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, request);

        Assert.Equal(CounterRegisterMemberResult.DuplicateEmail, result);
        Assert.Null(data);
        await AssertNoRegistrationWritesAsync(fixture);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WithDuplicatePhone_WritesNothing()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();
        CounterRegisterMemberAPIViewModel request = fixture.ValidRequest();
        request.Phone = fixture.Manager.Phone!;

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, request);

        Assert.Equal(CounterRegisterMemberResult.DuplicatePhone, result);
        Assert.Null(data);
        await AssertNoRegistrationWritesAsync(fixture);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WithInactivePackage_WritesNothing()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();
        fixture.Package.IsActive = false;
        await fixture.Context.SaveChangesAsync();

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Manager.Id,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.PackageInactive, result);
        Assert.Null(data);
        await AssertNoRegistrationWritesAsync(fixture);
    }

    [Theory]
    [InlineData("coach-account")]
    [InlineData("member-account")]
    public async Task RegisterMemberAtCounterAsync_WithDisallowedStaffRole_WritesNothing(
        string staffAccountId)
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                staffAccountId,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.StaffRoleNotAllowed, result);
        Assert.Null(data);
        await AssertNoRegistrationWritesAsync(fixture);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WithUnconfiguredEmail_CommitsAndReportsNotConfigured()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();
        fixture.Email.IsConfigured = false;

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Manager.Id,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.Success, result);
        Assert.Equal("NOT_CONFIGURED", data?.EmailDelivery);
        Assert.Single(await fixture.Context.MembershipInvoices.ToListAsync());
        Assert.Equal(0, fixture.Email.WelcomeMessagesSent);
    }

    [Fact]
    public async Task RegisterMemberAtCounterAsync_WithConfiguredEmail_ReportsSentWithoutPasswordHash()
    {
        await using MemberSubscriptionServiceTestFixture fixture =
            await MemberSubscriptionServiceTestFixture.CreateAsync();

        (CounterRegisterMemberResult result, CounterRegisterMemberResponseAPIViewModel? data) =
            await fixture.Service.RegisterMemberAtCounterAsync(
                fixture.Receptionist.Id,
                fixture.ValidRequest());

        Assert.Equal(CounterRegisterMemberResult.Success, result);
        Assert.Equal("SENT", data?.EmailDelivery);
        Assert.Equal(1, fixture.Email.WelcomeMessagesSent);
        string responseJson = JsonSerializer.Serialize(data);
        Assert.DoesNotContain("passwordHash", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelInvoice_CancelsPendingPairOnce_AndDoesNotReactivateOnRetry()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, fixture.ValidRequest());
        MembershipInvoice invoice = await fixture.Context.MembershipInvoices.SingleAsync();
        Services.MembershipInvoiceService.MembershipInvoiceService service = CreateInvoiceService(fixture);

        Assert.Equal(Services.MembershipInvoiceService.CancelInvoiceResult.Success,
            await service.CancelPendingInvoiceAsync(invoice.Id, fixture.Manager.Id));
        Assert.Equal(Services.MembershipInvoiceService.CancelInvoiceResult.InvalidInvoiceState,
            await service.CancelPendingInvoiceAsync(invoice.Id, fixture.Manager.Id));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal("CANCELED", (await fixture.Context.MembershipInvoices.SingleAsync()).Status);
        Assert.Equal("CANCELED", (await fixture.Context.MemberSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelInvoice_MemberCannotCancelAnotherMembersInvoice()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, fixture.ValidRequest());
        MembershipInvoice invoice = await fixture.Context.MembershipInvoices.SingleAsync();
        Services.MembershipInvoiceService.MembershipInvoiceService service = CreateInvoiceService(fixture);

        Assert.Equal(Services.MembershipInvoiceService.CancelInvoiceResult.InvoiceNotFound,
            await service.CancelPendingInvoiceAsync(invoice.Id, "member-account"));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal("PENDING_PAYMENT", (await fixture.Context.MembershipInvoices.SingleAsync()).Status);
        Assert.Equal("PENDING_PAYMENT", (await fixture.Context.MemberSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelInvoice_DoesNotCancelPaidInvoice()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, fixture.ValidRequest());
        MembershipInvoice invoice = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        invoice.Status = "PAID";
        invoice.Subscription.Status = "CONFIRMED";
        await fixture.Context.SaveChangesAsync();
        Services.MembershipInvoiceService.MembershipInvoiceService service = CreateInvoiceService(fixture);

        Assert.Equal(Services.MembershipInvoiceService.CancelInvoiceResult.InvalidInvoiceState,
            await service.CancelPendingInvoiceAsync(invoice.Id, fixture.Manager.Id));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal("PAID", (await fixture.Context.MembershipInvoices.SingleAsync()).Status);
        Assert.Equal("CONFIRMED", (await fixture.Context.MemberSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task MemberInvoiceQueries_IgnoreForeignMemberFilter_AndHideForeignReceipt()
    {
        await using MemberSubscriptionServiceTestFixture fixture = await MemberSubscriptionServiceTestFixture.CreateAsync();
        await fixture.Service.RegisterMemberAtCounterAsync(fixture.Manager.Id, fixture.ValidRequest());
        MembershipInvoice invoice = await fixture.Context.MembershipInvoices.SingleAsync();
        Services.MembershipInvoiceService.MembershipInvoiceService service = CreateInvoiceService(fixture);

        Assert.Empty(await service.GetInvoicesAsync("member-account", invoice.MemberId));
        (Services.MembershipInvoiceService.GetReceiptResult result, APIViewModel.MembershipInvoice.MembershipReceiptAPIViewModel? receipt) =
            await service.GetReceiptAsync(invoice.Id, "member-account");
        Assert.Equal(Services.MembershipInvoiceService.GetReceiptResult.InvoiceNotFound, result);
        Assert.Null(receipt);
    }

    private static Services.MembershipInvoiceService.MembershipInvoiceService CreateInvoiceService(
        MemberSubscriptionServiceTestFixture fixture)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        return new Services.MembershipInvoiceService.MembershipInvoiceService(fixture.Context, configuration);
    }
    private static async Task AssertNoRegistrationWritesAsync(
        MemberSubscriptionServiceTestFixture fixture)
    {
        Assert.Equal(4, await fixture.Context.Accounts.CountAsync());
        Assert.Empty(await fixture.Context.Members.ToListAsync());
        Assert.Empty(await fixture.Context.MemberSubscriptions.ToListAsync());
        Assert.Empty(await fixture.Context.MembershipInvoices.ToListAsync());
    }

    private sealed class MemberSubscriptionServiceTestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private MemberSubscriptionServiceTestFixture(
            SqliteConnection connection,
            SportsCenterManagementContext context,
            MemberSubscriptionService service,
            FakeEmailService email,
            Account manager,
            Account receptionist,
            MembershipPackage package)
        {
            _connection = connection;
            Context = context;
            Service = service;
            Email = email;
            Manager = manager;
            Receptionist = receptionist;
            Package = package;
        }

        public SportsCenterManagementContext Context { get; }

        public MemberSubscriptionService Service { get; }

        public FakeEmailService Email { get; }

        public Account Manager { get; }

        public Account Receptionist { get; }

        public MembershipPackage Package { get; }

        public static async Task<MemberSubscriptionServiceTestFixture> CreateAsync()
        {
            SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<SportsCenterManagementContext> options =
                new DbContextOptionsBuilder<SportsCenterManagementContext>()
                    .UseSqlite(connection)
                    .Options;
            SportsCenterManagementContext context = new SportsCenterManagementContext(options);
            await context.Database.EnsureCreatedAsync();

            DateTime createdAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
            Role managerRole = new Role { Id = 1, Name = "CenterManager" };
            Role coachRole = new Role { Id = 2, Name = "Coach" };
            Role memberRole = new Role { Id = 3, Name = "Member" };
            Role receptionistRole = new Role { Id = 4, Name = "Receptionist" };
            Account manager = CreateStaffAccount(
                "manager-account",
                "manager@example.com",
                "0900000001",
                managerRole,
                createdAt);
            manager.CenterManager = new CenterManager
            {
                AccountId = manager.Id,
                FullName = "Counter Manager",
                CreatedAt = createdAt
            };
            Account receptionist = CreateStaffAccount(
                "receptionist-account",
                "receptionist@example.com",
                "0900000002",
                receptionistRole,
                createdAt);
            receptionist.Receptionist = new Receptionist
            {
                AccountId = receptionist.Id,
                FullName = "Counter Receptionist",
                CreatedAt = createdAt
            };
            Account coach = CreateStaffAccount(
                "coach-account",
                "coach@example.com",
                "0900000003",
                coachRole,
                createdAt);
            Account member = CreateStaffAccount(
                "member-account",
                "member@example.com",
                "0900000004",
                memberRole,
                createdAt);
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

            context.Accounts.AddRange(manager, receptionist, coach, member);
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

            return new MemberSubscriptionServiceTestFixture(
                connection,
                context,
                service,
                email,
                manager,
                receptionist,
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

        private static Account CreateStaffAccount(
            string id,
            string email,
            string phone,
            Role role,
            DateTime createdAt)
        {
            return new Account
            {
                Id = id,
                Email = email,
                Phone = phone,
                PasswordHash = "unused",
                Status = "Active",
                CreatedAt = createdAt,
                RoleId = role.Id,
                Role = role
            };
        }
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool IsConfigured { get; set; } = true;

        public bool ThrowOnWelcome { get; set; }

        public int WelcomeMessagesSent { get; private set; }

        public Task SendPasswordChangeOtpAsync(
            string recipientEmail,
            string otp,
            int expiresInMinutes)
        {
            return Task.CompletedTask;
        }

        public Task SendEmailVerificationOtpAsync(string recipientEmail, string otp, string purpose, int expiresInMinutes) => Task.CompletedTask;

        public Task SendMemberWelcomeAsync(
            string recipientEmail,
            string fullName,
            string initialPassword,
            string packageName,
            decimal amount)
        {
            if (ThrowOnWelcome)
            {
                throw new InvalidOperationException("Welcome email delivery failed.");
            }

            WelcomeMessagesSent++;
            return Task.CompletedTask;
        }
    }
}
