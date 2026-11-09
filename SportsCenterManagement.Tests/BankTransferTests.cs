using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.MembershipInvoiceService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public class BankTransferTests
{
    [Fact]
    public async Task ForeignMemberCannotReadTransferDetails()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        fixture.Context.Accounts.Add(new Account
        {
            Id = "other-member", Email = "other@example.com", PasswordHash = "unused", Status = "Active",
            Role = fixture.Member.Account.Role, CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(fixture).GetBankTransferInstructionsAsync(invoice.Id, "other-member"));
        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task InstructionsUseServerAmountAndNeverActivateSubscription()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture);
        BankTransferInstructionsAPIViewModel qr = await service.GetBankTransferInstructionsAsync(invoice.Id, fixture.Member.AccountId);
        Assert.Equal(invoice.Amount, qr.Amount);
        Assert.Contains("amount=500000&", qr.QrImageUrl);
        Assert.Contains("addInfo=SCMS" + invoice.Id, qr.QrImageUrl);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Equal("PENDING_PAYMENT", invoice.Subscription.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
        BusinessRuleException denied = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetBankTransferInstructionsAsync(invoice.Id, "unknown-member"));
        Assert.Equal(403, denied.StatusCode);
    }

    [Fact]
    public async Task ConfirmationStoresPaidPairAndActorOnce()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture);
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) = await service.ConfirmBankTransferAsync(invoice.Id, fixture.Manager.Id, Request());
        Assert.Equal(PayInvoiceResult.Success, result);
        Assert.Equal("BANK_TRANSFER", receipt!.PaymentMethod);
        Assert.Equal("CONFIRMED", receipt.SubscriptionStatus);
        Assert.Equal(fixture.Manager.Id, receipt.PaidByStaffId);
        Assert.NotNull(receipt.PaidAt);
        Assert.Equal(PayInvoiceResult.AlreadyPaid, (await service.ConfirmBankTransferAsync(invoice.Id, fixture.Manager.Id, Request())).Result);
        Assert.Single(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("amount", PayInvoiceResult.AmountMismatch)]
    [InlineData("unconfirmed", PayInvoiceResult.InvalidConfirmation)]
    [InlineData("cash", PayInvoiceResult.InvalidPaymentMethod)]
    [InlineData("member", PayInvoiceResult.StaffRoleNotAllowed)]
    [InlineData("upgrade", PayInvoiceResult.QuoteExpired)]
    public async Task InvalidConfirmationDoesNotChangeData(string scenario, PayInvoiceResult expected)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null, scenario == "cash" ? "CASH" : "BANK_TRANSFER");
        if (scenario == "upgrade") { invoice.Subscription.Kind = "UPGRADE"; invoice.CreatedAt = DateTime.UtcNow.AddDays(-2); }
        await fixture.Context.SaveChangesAsync();
        ConfirmBankTransferAPIViewModel request = Request();
        if (scenario == "amount") request.Amount = 1;
        if (scenario == "unconfirmed") request.ConfirmReceivedTransfer = false;
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) = await Service(fixture).ConfirmBankTransferAsync(
            invoice.Id, scenario == "member" ? fixture.Member.AccountId : fixture.Manager.Id, request);
        Assert.Equal(expected, result);
        Assert.Null(receipt);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task AuditFailureRollsBackBankPayment()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectBankAudit BEFORE INSERT ON AuditLog BEGIN SELECT RAISE(ABORT, 'Injected audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(fixture).ConfirmBankTransferAsync(invoice.Id, fixture.Manager.Id, Request()));
        fixture.Context.ChangeTracker.Clear();
        MembershipInvoice saved = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        Assert.Equal("PENDING_PAYMENT", saved.Status);
        Assert.Equal("PENDING_PAYMENT", saved.Subscription.Status);
        Assert.Null(saved.PaidAt);
    }

    [Fact]
    public async Task MissingBankConfigurationReturnsActionableError()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = new(fixture.Context, fixture.Configuration);
        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetBankTransferInstructionsAsync(invoice.Id, fixture.Member.AccountId));
        Assert.Equal("BANK_TRANSFER_NOT_CONFIGURED", error.Code);
        Assert.Equal(503, error.StatusCode);
    }

    private static ConfirmBankTransferAPIViewModel Request() => new() { Amount = 500000, ConfirmReceivedTransfer = true };

    private static MembershipInvoiceService Service(ReportTestFixture fixture)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddConfiguration(fixture.Configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BankTransfer:BankId"] = new string('1', 6),
                ["BankTransfer:AccountNumber"] = new string('1', 10),
                ["BankTransfer:AccountName"] = "TEST RECEIVER"
            }).Build();
        return new(fixture.Context, configuration);
    }
}
