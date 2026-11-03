using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using APIViewModel.MembershipInvoice;
using Microsoft.AspNetCore.Mvc.Testing;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("manager-account", "manager@example.com", "CenterManager")]
    [InlineData("receptionist-account", "receptionist@example.com", "Receptionist")]
    public async Task CashPayment_HttpCollectionAndRetryReturnSavedReceipt(string actor, string email, string role)
    {
        await using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using WebApplicationFactory<Program> original = new AuthenticationWebApplicationFactory();
        await using WebApplicationFactory<Program> factory = original.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["BusinessSettings:TimeZoneId"] = "SE Asia Standard Time" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<SportsCenterManagementContext>();
                services.RemoveAll<DbContextOptions<SportsCenterManagementContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SportsCenterManagementContext>>();
                services.AddDbContext<SportsCenterManagementContext>(options => options.UseSqlite(connection));
            });
        });
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(actor, email, role, DateTime.UtcNow.AddMinutes(10)));
        int invoiceId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            SportsCenterManagementContext context = scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
            MembershipPackage package = new MembershipPackage { Name = "Cash HTTP package", Price = 500000m,
                DurationMonths = 1, Benefits = "[]", IsActive = true, CreatedAt = DateTime.UtcNow };
            MemberSubscription subscription = new MemberSubscription { MemberId = "account-1", Package = package,
                PackageName = package.Name, PackagePrice = package.Price, Benefits = "[]", DurationMonths = 1,
                Kind = "REGISTER", Status = "PENDING_PAYMENT", CreatedAt = DateTime.UtcNow,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1) };
            MembershipInvoice invoice = new MembershipInvoice { InvoiceNumber = "HTTP-CASH-" + Guid.NewGuid().ToString("N"),
                MemberId = "account-1", Subscription = subscription, Amount = 500000m, PaymentMethod = "CASH",
                Status = "PENDING_PAYMENT", CreatedAt = DateTime.UtcNow, CreatedBy = actor };
            context.MembershipInvoices.Add(invoice);
            await context.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        using HttpResponseMessage mismatch = await client.PostAsJsonAsync($"/api/MembershipInvoice/{invoiceId}/pay",
            new PayMembershipInvoiceAPIViewModel { PaymentMethod = "CASH", Amount = 1m, ConfirmReceivedCash = true });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Contains("PAYMENT_AMOUNT_MISMATCH", await mismatch.Content.ReadAsStringAsync());
        PayMembershipInvoiceAPIViewModel request = new PayMembershipInvoiceAPIViewModel
            { PaymentMethod = "CASH", Amount = 500000m, ConfirmReceivedCash = true };
        using HttpResponseMessage paid = await client.PostAsJsonAsync($"/api/MembershipInvoice/{invoiceId}/pay", request);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        MembershipReceiptAPIViewModel? receipt = await paid.Content.ReadFromJsonAsync<MembershipReceiptAPIViewModel>();
        Assert.NotNull(receipt);
        Assert.Equal("PAID", receipt.InvoiceStatus);
        Assert.Equal("CONFIRMED", receipt.SubscriptionStatus);
        Assert.Equal(actor, receipt.PaidByStaffId);
        using HttpResponseMessage retry = await client.PostAsJsonAsync($"/api/MembershipInvoice/{invoiceId}/pay", request);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        Assert.Contains("INVOICE_ALREADY_PAID", await retry.Content.ReadAsStringAsync());
        MembershipReceiptAPIViewModel? readBack = await client.GetFromJsonAsync<MembershipReceiptAPIViewModel>(
            $"/api/MembershipInvoice/{invoiceId}/receipt");
        Assert.Equal(receipt.PaidAt, readBack!.PaidAt);
        using IServiceScope verifyScope = factory.Services.CreateScope();
        SportsCenterManagementContext verify = verifyScope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();
        Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.Action == "MEMBERSHIP_INVOICE_PAID"));
    }

    [Theory]
    [InlineData(null, null, null, HttpStatusCode.Unauthorized)]
    [InlineData("member-account", "member@example.com", "Member", HttpStatusCode.Forbidden)]
    [InlineData("coach-account", "coach@example.com", "Coach", HttpStatusCode.Forbidden)]
    public async Task CashPayment_DeniesUnprivilegedCallers(string? accountId, string? email, string? role, HttpStatusCode expected)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = accountId is null ? factory.CreateClient() : CreateAuthenticatedClient(factory,
            CreateToken(accountId, email!, role!, DateTime.UtcNow.AddMinutes(10)));
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/MembershipInvoice/1/pay",
            new PayMembershipInvoiceAPIViewModel { PaymentMethod = "CASH", Amount = 500000m, ConfirmReceivedCash = true });
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CashPayment_RequiresAmountInHttpContract(int? amount)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken("manager-account", "manager@example.com", "CenterManager", DateTime.UtcNow.AddMinutes(10)));
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/MembershipInvoice/1/pay",
            new PayMembershipInvoiceAPIViewModel { PaymentMethod = "CASH", Amount = amount, ConfirmReceivedCash = true });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"error\":", body);
        Assert.Contains("\"traceId\":", body);
    }
}
