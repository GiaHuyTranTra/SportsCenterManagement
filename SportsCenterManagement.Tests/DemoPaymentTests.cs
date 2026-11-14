using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Services.MembershipInvoiceService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public class DemoPaymentTests
{
    [Theory]
    [InlineData("Production", "true")]
    [InlineData("Staging", "true")]
    [InlineData("Development", "false")]
    public async Task SimulationRequiresDevelopmentAndExplicitOptIn(string environment, string enabled)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture, environment, enabled);
        Assert.False(service.IsDemoPaymentEnabled());
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, true));
        Assert.Equal("DEMO_PAYMENT_DISABLED", error.Code);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SimulationUsesServerAmountAndPersistsDemoLabelAcrossReadsAndRetries()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture);
        VnpayInvoiceStatusAPIViewModel status = await service.ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, true);
        Assert.Equal("PAID", status.Status);
        Assert.Equal(500000m, status.Amount);
        Assert.True(status.IsDemo);
        Assert.Equal("CONFIRMED", invoice.Subscription.Status);
        Assert.True((await service.ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, true)).IsDemo);

        fixture.Context.ChangeTracker.Clear();
        Assert.True((await service.GetVnpayInvoiceStatusAsync(invoice.Id, fixture.Member.AccountId)).IsDemo);
        (GetReceiptResult result, MembershipReceiptAPIViewModel? receipt) = await service.GetReceiptAsync(invoice.Id, fixture.Member.AccountId);
        Assert.Equal(GetReceiptResult.Success, result);
        Assert.True(receipt!.IsDemo);
        Assert.True(Assert.Single(await service.GetInvoicesAsync(fixture.Member.AccountId)).IsDemo);
        AuditLog audit = Assert.Single(await fixture.Context.AuditLogs.ToListAsync());
        Assert.Equal("DEMO_MEMBERSHIP_INVOICE_PAID", audit.Action);
        Assert.Contains("SIMULATED PAYMENT", audit.Description);
    }

    [Fact]
    public async Task MemberCannotSimulateAnotherMembersInvoice()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        fixture.Context.Accounts.Add(new Account
        {
            Id = "other-demo-member", Email = "other.demo@example.com", PasswordHash = "unused",
            Status = "Active", CreatedAt = DateTime.UtcNow, Role = fixture.Member.Account.Role
        });
        await fixture.Context.SaveChangesAsync();
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(fixture).ConfirmDemoPaymentAsync(invoice.Id, "other-demo-member", true));
        Assert.Equal("INVOICE_NOT_FOUND", error.Code);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
    }

    [Fact]
    public async Task SimulationRequiresConfirmationAndUsesCapturedPackagePrice()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture);
        BusinessRuleException confirmation = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, false));
        Assert.Equal("DEMO_CONFIRMATION_REQUIRED", confirmation.Code);
        fixture.Package.Price = 700000;
        await fixture.Context.SaveChangesAsync();
        VnpayInvoiceStatusAPIViewModel status =
            await service.ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, true);
        Assert.Equal("PAID", status.Status);
        Assert.Equal(500000m, invoice.Amount);
        Assert.Equal(500000m, invoice.Subscription.PackagePrice);
    }

    [Theory]
    [InlineData("CANCELED", "BANK_TRANSFER")]
    [InlineData("PENDING_PAYMENT", "CASH")]
    [InlineData("PAID", "BANK_TRANSFER")]
    public async Task SimulationDoesNotConvertCanceledCashOrProviderPaidInvoices(string state, string method)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, state, state == "PAID" ? DateTime.UtcNow : null, method);
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(fixture).ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, true));
        Assert.False((await Service(fixture).GetVnpayInvoiceStatusAsync(invoice.Id, fixture.Member.AccountId)).IsDemo);
        Assert.Equal(state, invoice.Status);
    }

    [Fact]
    public async Task AuditFailureRollsBackSimulationAndActivation()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectDemoAudit BEFORE INSERT ON AuditLog WHEN NEW.Action = 'DEMO_MEMBERSHIP_INVOICE_PAID' BEGIN SELECT RAISE(ABORT, 'Injected demo audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(fixture).ConfirmDemoPaymentAsync(invoice.Id, fixture.Member.AccountId, true));
        fixture.Context.ChangeTracker.Clear();
        MembershipInvoice saved = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        Assert.Equal("PENDING_PAYMENT", saved.Status);
        Assert.Equal("PENDING_PAYMENT", saved.Subscription.Status);
    }

    [Fact]
    public async Task QrSessionCanPayOnlyItsInvoiceWithoutPhoneLoginAndRetriesAreIdempotent()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        using MemoryCache cache = new(new MemoryCacheOptions());
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture, cache: cache);
        TransferQrSessionAPIViewModel qr = await service.CreateTransferQrAsync(invoice.Id, fixture.Member.AccountId);
        Assert.Equal("TITAN ARENA", qr.RecipientName);
        Assert.Equal(64, qr.Token.Length);
        Assert.InRange(qr.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));
        Assert.Equal("PENDING_PAYMENT", (await service.GetTransferQrStatusAsync(qr.Token)).Status);
        Assert.Equal(500000m, qr.Invoice.Amount);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Equal("PAID", (await service.ConfirmTransferQrAsync(qr.Token, true)).Status);
        Assert.Equal("PAID", (await service.ConfirmTransferQrAsync(qr.Token, true)).Status);
        Assert.Equal("PAID", (await service.GetTransferQrStatusAsync(qr.Token)).Status);
        Assert.Equal("CONFIRMED", invoice.Subscription.Status);
        Assert.Single(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task QrRejectsInvalidEvictedAndDisabledSessions()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        using MemoryCache cache = new(new MemoryCacheOptions());
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture, cache: cache);
        TransferQrSessionAPIViewModel qr = await service.CreateTransferQrAsync(invoice.Id, fixture.Member.AccountId);
        string wrongToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        BusinessRuleException invalid = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmTransferQrAsync(wrongToken, true));
        Assert.Equal("TRANSFER_QR_EXPIRED", invalid.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmTransferQrAsync(qr.Token, false));
        MembershipInvoiceService production = Service(fixture, "Production", cache: cache);
        await Assert.ThrowsAsync<BusinessRuleException>(() => production.CreateTransferQrAsync(invoice.Id, fixture.Member.AccountId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => production.ConfirmTransferQrAsync(qr.Token, true));
        await Assert.ThrowsAsync<BusinessRuleException>(() => production.GetTransferQrStatusAsync(qr.Token));
        cache.Compact(1);
        BusinessRuleException expired = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetTransferQrStatusAsync(qr.Token));
        Assert.Equal("TRANSFER_QR_EXPIRED", expired.Code);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task QrRechecksOwnerAndCanceledInvoiceAtConfirmation()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        using MemoryCache cache = new(new MemoryCacheOptions());
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture, cache: cache);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateTransferQrAsync(invoice.Id, "someone-else"));
        TransferQrSessionAPIViewModel qr = await service.CreateTransferQrAsync(invoice.Id, fixture.Member.AccountId);
        fixture.Member.Account.IsLocked = true;
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmTransferQrAsync(qr.Token, true));
        fixture.Member.Account.IsLocked = false;
        invoice.Status = "CANCELED";
        await fixture.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmTransferQrAsync(qr.Token, true));
        Assert.Null(invoice.PaidAt);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    private static MembershipInvoiceService Service(ReportTestFixture fixture, string environment = "Development", string enabled = "true", IMemoryCache? cache = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddConfiguration(fixture.Configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoPayments:Enabled"] = enabled, ["DemoPayments:CollectorAccountId"] = fixture.Manager.Id
            }).Build();
        return new MembershipInvoiceService(fixture.Context, configuration, new DemoEnvironment { EnvironmentName = environment }, cache);
    }

    private sealed class DemoEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "DemoPaymentTests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
