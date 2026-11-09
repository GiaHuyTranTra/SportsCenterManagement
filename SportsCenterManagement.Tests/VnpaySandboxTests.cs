using System.Security.Cryptography;
using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.MembershipInvoiceService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public class VnpaySandboxTests
{
    [Fact]
    public async Task CheckoutTrimsCopiedCredentialsBeforeSigning()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        MembershipInvoiceService service = Service(fixture, "\r\n" + secret + " \t", " TESTSHOP ");

        VnpayCheckoutAPIViewModel checkout = await service.CreateVnpayCheckoutAsync(invoice.Id, fixture.Member.AccountId, "127.0.0.1");
        string query = new Uri(checkout.PaymentUrl).Query.TrimStart('?');
        string[] parts = query.Split("&vnp_SecureHash=", StringSplitOptions.None);
        Assert.Equal(2, parts.Length);
        string expected = Convert.ToHexString(HMACSHA512.HashData(
            System.Text.Encoding.UTF8.GetBytes(secret), System.Text.Encoding.UTF8.GetBytes(parts[0]))).ToLowerInvariant();
        Assert.Equal(expected, parts[1]);
        Assert.Equal("TESTSHOP", QueryHelpers.ParseQuery(query)["vnp_TmnCode"].ToString());
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
    }

    [Fact]
    public async Task StatusIsReadOnlyAndScopedToCaller()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        fixture.Context.Accounts.Add(new Account
        {
            Id = "foreign-member", Email = "foreign@example.com", PasswordHash = "unused", Status = "Active",
            Role = fixture.Member.Account.Role, CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();
        MembershipInvoiceService service = Service(fixture, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        VnpayInvoiceStatusAPIViewModel status = await service.GetVnpayInvoiceStatusAsync(invoice.Id, fixture.Member.AccountId);
        Assert.Equal("PENDING_PAYMENT", status.Status);
        Assert.Equal("PENDING_PAYMENT", invoice.Subscription.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetVnpayInvoiceStatusAsync(invoice.Id, "foreign-member"));
    }

    [Fact]
    public async Task CheckoutAndSignedIpnSettleOnce()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        MembershipInvoiceService service = Service(fixture, secret);
        VnpayCheckoutAPIViewModel checkout = await service.CreateVnpayCheckoutAsync(invoice.Id, fixture.Member.AccountId, "127.0.0.1");
        Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", checkout.PaymentUrl);
        Dictionary<string, string> outbound = QueryHelpers.ParseQuery(new Uri(checkout.PaymentUrl).Query)
            .ToDictionary(item => item.Key, item => item.Value.ToString());
        Assert.Equal("50000000", outbound["vnp_Amount"]);
        Assert.Contains("invoiceId=" + invoice.Id, outbound["vnp_ReturnUrl"]);
        Assert.Equal(MembershipInvoiceService.VnpaySign(outbound, secret), outbound["vnp_SecureHash"]);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Dictionary<string, string> ipn = Callback(checkout.TransactionReference, secret);
        Assert.Equal("00", (await service.ProcessVnpayIpnAsync(ipn)).RspCode);
        Assert.Equal("02", (await service.ProcessVnpayIpnAsync(ipn)).RspCode);
        Assert.Equal("PAID", invoice.Status);
        Assert.Equal("CONFIRMED", invoice.Subscription.Status);
        Assert.Equal("PAID", (await service.GetVnpayInvoiceStatusAsync(invoice.Id, fixture.Member.AccountId)).Status);
        Assert.Equal(GetReceiptResult.Success, (await service.GetReceiptAsync(invoice.Id, fixture.Member.AccountId)).Result);
        Assert.Single(await fixture.Context.AuditLogs.Where(item => item.Action == "MEMBERSHIP_INVOICE_PAID").ToListAsync());
    }

    [Theory]
    [InlineData("signature", "97")]
    [InlineData("amount", "04")]
    [InlineData("merchant", "97")]
    [InlineData("unknown", "01")]
    [InlineData("failed", "00")]
    [InlineData("status", "00")]
    public async Task InvalidOrFailedIpnLeavesPending(string scenario, string expected)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        MembershipInvoiceService service = Service(fixture, secret);
        VnpayCheckoutAPIViewModel checkout = await service.CreateVnpayCheckoutAsync(invoice.Id, fixture.Member.AccountId, "127.0.0.1");
        Dictionary<string, string> ipn = Callback(checkout.TransactionReference, secret);
        if (scenario == "amount") ipn["vnp_Amount"] = "100";
        if (scenario == "merchant") ipn["vnp_TmnCode"] = "OTHER";
        if (scenario == "unknown") ipn["vnp_TxnRef"] = Guid.NewGuid().ToString("N");
        if (scenario == "failed") ipn["vnp_ResponseCode"] = "24";
        if (scenario == "status") ipn["vnp_TransactionStatus"] = "02";
        ipn["vnp_SecureHash"] = MembershipInvoiceService.VnpaySign(ipn, secret);
        if (scenario == "signature") ipn["vnp_SecureHash"] = "invalid";
        Assert.Equal(expected, (await service.ProcessVnpayIpnAsync(ipn)).RspCode);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Empty(await fixture.Context.AuditLogs.Where(item => item.Action == "MEMBERSHIP_INVOICE_PAID").ToListAsync());
    }

    [Fact]
    public async Task AuditFailureRollsBackIpnSettlement()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        MembershipInvoice invoice = fixture.AddInvoice(500000, "PENDING_PAYMENT", null, "BANK_TRANSFER");
        await fixture.Context.SaveChangesAsync();
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        MembershipInvoiceService service = Service(fixture, secret);
        VnpayCheckoutAPIViewModel checkout = await service.CreateVnpayCheckoutAsync(invoice.Id, fixture.Member.AccountId, "127.0.0.1");
        await fixture.Context.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectVnpayAudit BEFORE INSERT ON AuditLog WHEN NEW.Action = 'MEMBERSHIP_INVOICE_PAID' BEGIN SELECT RAISE(ABORT, 'Injected audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ProcessVnpayIpnAsync(Callback(checkout.TransactionReference, secret)));
        fixture.Context.ChangeTracker.Clear();
        MembershipInvoice saved = await fixture.Context.MembershipInvoices.Include(item => item.Subscription).SingleAsync();
        Assert.Equal("PENDING_PAYMENT", saved.Status);
        Assert.Equal("PENDING_PAYMENT", saved.Subscription.Status);
    }

    private static Dictionary<string, string> Callback(string reference, string secret)
    {
        Dictionary<string, string> values = new()
        {
            ["vnp_TmnCode"] = "TESTSHOP", ["vnp_TxnRef"] = reference, ["vnp_Amount"] = "50000000",
            ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00", ["vnp_TransactionNo"] = "123456"
        };
        values["vnp_SecureHash"] = MembershipInvoiceService.VnpaySign(values, secret);
        return values;
    }

    private static MembershipInvoiceService Service(ReportTestFixture fixture, string secret, string merchant = "TESTSHOP")
    {
        IConfiguration config = new ConfigurationBuilder().AddConfiguration(fixture.Configuration).AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["VnpaySandbox:Enabled"] = "true", ["VnpaySandbox:TmnCode"] = merchant,
                ["VnpaySandbox:HashSecret"] = secret, ["VnpaySandbox:CollectorAccountId"] = fixture.Manager.Id,
                ["VnpaySandbox:ReturnUrl"] = "http://localhost:5173/payments/result"
            }).Build();
        return new(fixture.Context, config);
    }
}
