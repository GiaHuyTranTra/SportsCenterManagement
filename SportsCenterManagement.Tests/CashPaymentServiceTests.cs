using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.MembershipInvoiceService;

namespace SportsCenterManagement.Tests;

public sealed class CashPaymentServiceTests
{
    [Fact]
    public async Task ConfirmCash_StoresPaidPairActorAuditAndReceipt_AndRetryDoesNotCollectAgain()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null);
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = new MembershipInvoiceService(fixture.Context, fixture.Configuration);

        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) =
            await service.PayInvoiceAsync(invoice.Id, fixture.Manager.Id, Request());
        Assert.Equal(PayInvoiceResult.Success, result);
        Assert.NotNull(receipt);
        Assert.Equal(500000m, receipt.Amount);
        Assert.Equal("PAID", receipt.InvoiceStatus);
        Assert.Equal("CONFIRMED", receipt.SubscriptionStatus);
        Assert.Equal(fixture.Manager.Id, receipt.PaidByStaffId);
        Assert.NotNull(receipt.PaidAt);
        DateTime? paidAt = receipt.PaidAt;
        DateOnly startDate = receipt.StartDate;
        Assert.Equal(startDate.AddMonths(1).AddDays(-1), receipt.EndDate);
        AuditLog audit = Assert.Single(await fixture.Context.AuditLogs.ToListAsync());
        Assert.Equal("MEMBERSHIP_INVOICE_PAID", audit.Action);
        Assert.Equal(fixture.Manager.Id, audit.AccountId);
        Assert.Equal(invoice.Id.ToString(), audit.EntityId);

        (PayInvoiceResult retry, MembershipReceiptAPIViewModel? retryReceipt) =
            await service.PayInvoiceAsync(invoice.Id, fixture.Manager.Id, Request());
        Assert.Equal(PayInvoiceResult.AlreadyPaid, retry);
        Assert.Null(retryReceipt);
        Assert.Equal(paidAt, invoice.PaidAt);
        Assert.Equal(startDate, invoice.Subscription.StartDate);
        Assert.Single(await fixture.Context.AuditLogs.ToListAsync());
        (GetReceiptResult readResult, MembershipReceiptAPIViewModel? savedReceipt) =
            await service.GetReceiptAsync(invoice.Id, fixture.Manager.Id);
        Assert.Equal(GetReceiptResult.Success, readResult);
        Assert.Equal(paidAt, savedReceipt!.PaidAt);
    }

    [Theory]
    [InlineData("amount", PayInvoiceResult.AmountMismatch)]
    [InlineData("fraction", PayInvoiceResult.InvalidConfirmation)]
    [InlineData("missing", PayInvoiceResult.InvalidConfirmation)]
    [InlineData("unconfirmed", PayInvoiceResult.InvalidConfirmation)]
    [InlineData("cardRequest", PayInvoiceResult.InvalidPaymentMethod)]
    [InlineData("cardInvoice", PayInvoiceResult.InvalidPaymentMethod)]
    [InlineData("bankInvoice", PayInvoiceResult.InvalidPaymentMethod)]
    [InlineData("canceled", PayInvoiceResult.InvalidInvoiceState)]
    [InlineData("price", PayInvoiceResult.PackageChanged)]
    [InlineData("duration", PayInvoiceResult.PackageChanged)]
    [InlineData("inactivePackage", PayInvoiceResult.PackageInactive)]
    [InlineData("inactiveMember", PayInvoiceResult.MemberInactive)]
    [InlineData("deletedMember", PayInvoiceResult.MemberInactive)]
    [InlineData("lockedMember", PayInvoiceResult.MemberLocked)]
    [InlineData("suspended", PayInvoiceResult.InvalidSubscriptionState)]
    [InlineData("mismatchedSnapshot", PayInvoiceResult.DataIntegrityViolation)]
    [InlineData("paidMetadata", PayInvoiceResult.DataIntegrityViolation)]
    [InlineData("staleUpgrade", PayInvoiceResult.QuoteExpired)]
    [InlineData("upgrade", PayInvoiceResult.UnsupportedOrderKind)]
    [InlineData("unknownKind", PayInvoiceResult.UnsupportedOrderKind)]
    public async Task InvalidCollection_PreservesInvoiceSubscriptionAndAudit(string scenario, PayInvoiceResult expected)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null);
        PayMembershipInvoiceAPIViewModel request = Request();
        switch (scenario)
        {
            case "amount": request.Amount = 499999m; break;
            case "fraction": request.Amount = 500000.5m; break;
            case "missing": request.Amount = null; break;
            case "unconfirmed": request.ConfirmReceivedCash = false; break;
            case "cardRequest": request.PaymentMethod = "CARD"; break;
            case "cardInvoice": invoice.PaymentMethod = "CARD"; break;
            case "bankInvoice": invoice.PaymentMethod = "BANK_TRANSFER"; break;
            case "canceled": invoice.Status = "CANCELED"; invoice.Subscription.Status = "CANCELED"; break;
            case "price": fixture.Package.Price++; break;
            case "duration": fixture.Package.DurationMonths = 3; break;
            case "inactivePackage": fixture.Package.IsActive = false; break;
            case "inactiveMember": fixture.Member.Account.Status = "Inactive"; break;
            case "deletedMember": fixture.Member.Account.DeletedAt = DateTime.UtcNow; break;
            case "lockedMember": fixture.Member.Account.IsLocked = true; break;
            case "suspended": invoice.Subscription.IsSuspended = true; break;
            case "mismatchedSnapshot": invoice.Subscription.PackagePrice = 1m; break;
            case "paidMetadata": invoice.PaidAt = DateTime.UtcNow; break;
            case "staleUpgrade": invoice.Subscription.Kind = "UPGRADE"; invoice.CreatedAt = DateTime.UtcNow.AddDays(-2); break;
            case "upgrade": invoice.Subscription.Kind = "UPGRADE"; invoice.CreatedAt = DateTime.UtcNow; break;
            case "unknownKind": invoice.Subscription.Kind = "UNKNOWN"; break;
        }
        await fixture.Context.SaveChangesAsync();
        string previousStatus = invoice.Status;
        string previousSubscriptionStatus = invoice.Subscription.Status;
        DateOnly previousStart = invoice.Subscription.StartDate;
        DateTime? previousPaidAt = invoice.PaidAt;
        MembershipInvoiceService service = new MembershipInvoiceService(fixture.Context, fixture.Configuration);

        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) =
            await service.PayInvoiceAsync(invoice.Id, fixture.Manager.Id, request);
        Assert.Equal(expected, result);
        Assert.Null(receipt);
        fixture.Context.ChangeTracker.Clear();
        MembershipInvoice saved = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        Assert.Equal(previousStatus, saved.Status);
        Assert.Equal(previousSubscriptionStatus, saved.Subscription.Status);
        Assert.Equal(previousStart, saved.Subscription.StartDate);
        Assert.Equal(previousPaidAt, saved.PaidAt);
        Assert.Null(saved.PaidBy);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("member", PayInvoiceResult.StaffRoleNotAllowed)]
    [InlineData("inactive", PayInvoiceResult.StaffInactive)]
    [InlineData("locked", PayInvoiceResult.StaffLocked)]
    [InlineData("deleted", PayInvoiceResult.StaffInactive)]
    [InlineData("missing", PayInvoiceResult.StaffNotFound)]
    public async Task ActorIsVerifiedFromDatabase(string scenario, PayInvoiceResult expected)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null);
        if (scenario == "inactive") fixture.Manager.Status = "Inactive";
        if (scenario == "locked") fixture.Manager.IsLocked = true;
        if (scenario == "deleted") fixture.Manager.DeletedAt = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();
        string actor = scenario == "member" ? fixture.Member.AccountId : scenario == "missing" ? "not-found" : fixture.Manager.Id;
        MembershipInvoiceService service = new MembershipInvoiceService(fixture.Context, fixture.Configuration);
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) =
            await service.PayInvoiceAsync(invoice.Id, actor, Request());
        Assert.Equal(expected, result);
        Assert.Null(receipt);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task RenewalStartsAfterLatestConfirmedPeriod()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice paid = fixture.AddInvoice(500000m, "PAID", DateTime.UtcNow);
        DateOnly futureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
        paid.Subscription.EndDate = futureEnd;
        MembershipInvoice pending = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null);
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = new MembershipInvoiceService(fixture.Context, fixture.Configuration);
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) =
            await service.PayInvoiceAsync(pending.Id, fixture.Manager.Id, Request());
        Assert.Equal(PayInvoiceResult.Success, result);
        Assert.Equal("RENEW", receipt!.Kind);
        Assert.Equal(futureEnd.AddDays(1), receipt.StartDate);
    }

    [Fact]
    public async Task AuditInsertFailureRollsBackPaymentAndSubscription()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000m, "PENDING_PAYMENT", null);
        await fixture.Context.SaveChangesAsync();
        await fixture.Context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectPaymentAudit BEFORE INSERT ON AuditLog BEGIN SELECT RAISE(ABORT, 'Injected audit failure'); END;");
        MembershipInvoiceService service = new MembershipInvoiceService(fixture.Context, fixture.Configuration);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.PayInvoiceAsync(invoice.Id, fixture.Manager.Id, Request()));
        fixture.Context.ChangeTracker.Clear();
        MembershipInvoice saved = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        Assert.Equal("PENDING_PAYMENT", saved.Status);
        Assert.Equal("PENDING_PAYMENT", saved.Subscription.Status);
        Assert.Null(saved.PaidAt);
        Assert.Null(saved.PaidBy);
        Assert.Empty(await fixture.Context.AuditLogs.ToListAsync());
    }

    private static PayMembershipInvoiceAPIViewModel Request() => new PayMembershipInvoiceAPIViewModel
    {
        PaymentMethod = "CASH", Amount = 500000m, ConfirmReceivedCash = true
    };
}
