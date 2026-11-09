using APIViewModel.MembershipInvoice;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Services.MembershipInvoiceService;

namespace SportsCenterManagement.Tests;

public sealed class CashPaymentSqlServerTests
{
    [Flow2SqlServerFact]
    public async Task ConcurrentCollection_ProducesOnePaidTransitionAndOneAudit()
    {
        CashFixture fixture = await SeedAsync();
        Task<PayInvoiceResult> first = PayAsync(fixture);
        Task<PayInvoiceResult> second = PayAsync(fixture);
        PayInvoiceResult[] results = await Task.WhenAll(first, second);
        Assert.Single(results, item => item == PayInvoiceResult.Success);
        Assert.All(results, item => Assert.Contains(item,
            new[] { PayInvoiceResult.Success, PayInvoiceResult.AlreadyPaid, PayInvoiceResult.ConcurrencyConflict }));
        await using SportsCenterManagementContext context = CreateContext();
        MembershipInvoice invoice = await context.MembershipInvoices.Include(item => item.Subscription)
            .SingleAsync(item => item.Id == fixture.InvoiceId);
        Assert.Equal("PAID", invoice.Status);
        Assert.Equal("CONFIRMED", invoice.Subscription.Status);
        Assert.NotNull(invoice.PaidAt);
        Assert.Equal(fixture.StaffId, invoice.PaidBy);
        Assert.Equal(1, await context.AuditLogs.CountAsync(item => item.EntityType == "MembershipInvoice" &&
            item.EntityId == fixture.InvoiceId.ToString() && item.Action == "MEMBERSHIP_INVOICE_PAID"));
    }

    [Flow2SqlServerFact]
    public async Task PaymentVersusCancellation_NeverLeavesMismatchedPair()
    {
        CashFixture fixture = await SeedAsync();
        Task<PayInvoiceResult> payment = PayAsync(fixture);
        Task<CancelInvoiceResult> cancellation = CancelAsync(fixture);
        await Task.WhenAll((Task)payment, cancellation);
        await using SportsCenterManagementContext context = CreateContext();
        MembershipInvoice invoice = await context.MembershipInvoices.Include(item => item.Subscription)
            .SingleAsync(item => item.Id == fixture.InvoiceId);
        Assert.True((invoice.Status == "PAID" && invoice.Subscription.Status == "CONFIRMED") ||
            (invoice.Status == "CANCELED" && invoice.Subscription.Status == "CANCELED"));
        Assert.True((payment.Result == PayInvoiceResult.Success) != (cancellation.Result == CancelInvoiceResult.Success));
        Assert.Equal(invoice.Status == "PAID" ? 1 : 0, await context.AuditLogs.CountAsync(item =>
            item.EntityType == "MembershipInvoice" && item.EntityId == fixture.InvoiceId.ToString() &&
            item.Action == "MEMBERSHIP_INVOICE_PAID"));
    }

    [Flow2SqlServerFact]
    public async Task FailureAfterSave_RollsBackPaidPairAndAudit()
    {
        CashFixture fixture = await SeedAsync();
        await using SportsCenterManagementContext context = CreateContext(new RejectPaymentSaveInterceptor());
        MembershipInvoiceService service = Service(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayInvoiceAsync(fixture.InvoiceId, fixture.StaffId,
            new PayMembershipInvoiceAPIViewModel { PaymentMethod = "CASH", Amount = 500000m, ConfirmReceivedCash = true }));
        await using SportsCenterManagementContext verify = CreateContext();
        MembershipInvoice invoice = await verify.MembershipInvoices.Include(item => item.Subscription)
            .SingleAsync(item => item.Id == fixture.InvoiceId);
        Assert.Equal("PENDING_PAYMENT", invoice.Status);
        Assert.Equal("PENDING_PAYMENT", invoice.Subscription.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Null(invoice.PaidBy);
        Assert.False(await verify.AuditLogs.AnyAsync(item => item.EntityType == "MembershipInvoice" &&
            item.EntityId == fixture.InvoiceId.ToString() && item.Action == "MEMBERSHIP_INVOICE_PAID"));
    }

    private static async Task<PayInvoiceResult> PayAsync(CashFixture fixture)
    {
        await using SportsCenterManagementContext context = CreateContext();
        (PayInvoiceResult result, MembershipReceiptAPIViewModel? receipt) = await Service(context).PayInvoiceAsync(
            fixture.InvoiceId, fixture.StaffId,
            new PayMembershipInvoiceAPIViewModel { PaymentMethod = "CASH", Amount = 500000m, ConfirmReceivedCash = true });
        return result;
    }

    private static async Task<CancelInvoiceResult> CancelAsync(CashFixture fixture)
    {
        await using SportsCenterManagementContext context = CreateContext();
        return await Service(context).CancelPendingInvoiceAsync(fixture.InvoiceId, fixture.StaffId);
    }

    private static MembershipInvoiceService Service(SportsCenterManagementContext context) => new MembershipInvoiceService(
        context, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["BusinessSettings:TimeZoneId"] = "SE Asia Standard Time" }).Build());

    private static SportsCenterManagementContext CreateContext(SaveChangesInterceptor? interceptor = null)
    {
        string connection = Environment.GetEnvironmentVariable("SCMS_SQLSERVER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("SQL test connection is missing.");
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connection);
        if (!builder.InitialCatalog.StartsWith("SCMS_Flow2_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Use a dedicated SCMS_Flow2_Test_* database.");
        DbContextOptionsBuilder<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>().UseSqlServer(connection);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new SportsCenterManagementContext(options.Options);
    }

    private static async Task<CashFixture> SeedAsync()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role staffRole = await context.Roles.SingleAsync(item => item.Name == "Receptionist");
        Role memberRole = await context.Roles.SingleAsync(item => item.Name == "Member");
        string suffix = Guid.NewGuid().ToString("N");
        Account staff = new Account { Id = "cash-staff-" + suffix, Email = suffix + "staff@example.com",
            PasswordHash = Guid.NewGuid().ToString("N"), RoleId = staffRole.Id, Status = "Active", CreatedAt = DateTime.UtcNow };
        Account memberAccount = new Account { Id = "cash-member-" + suffix, Email = suffix + "member@example.com",
            PasswordHash = Guid.NewGuid().ToString("N"), RoleId = memberRole.Id, Status = "Active", CreatedAt = DateTime.UtcNow };
        Member member = new Member { Account = memberAccount, AccountId = memberAccount.Id,
            MemberCode = "C" + suffix[..24], FullName = "Cash test member", CreatedAt = DateTime.UtcNow };
        MembershipPackage package = new MembershipPackage { Name = "Cash test " + suffix, Price = 500000m,
            DurationMonths = 1, Benefits = "[]", IsActive = true, CreatedAt = DateTime.UtcNow };
        MemberSubscription subscription = new MemberSubscription { Member = member, Package = package,
            PackageName = package.Name, PackagePrice = package.Price, DurationMonths = 1, Benefits = "[]",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1),
            Kind = "REGISTER", Status = "PENDING_PAYMENT", CreatedAt = DateTime.UtcNow };
        MembershipInvoice invoice = new MembershipInvoice { InvoiceNumber = "CASH-" + suffix,
            Member = member, Subscription = subscription, Amount = 500000m, PaymentMethod = "CASH",
            Status = "PENDING_PAYMENT", CreatedByNavigation = staff, CreatedAt = DateTime.UtcNow };
        context.MembershipInvoices.Add(invoice);
        await context.SaveChangesAsync();
        return new CashFixture(invoice.Id, staff.Id);
    }

    private sealed record CashFixture(int InvoiceId, string StaffId);

    private sealed class RejectPaymentSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure after payment save, before transaction commit.");
    }
}
