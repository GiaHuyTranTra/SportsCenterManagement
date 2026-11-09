using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using APIViewModel.MembershipInvoice;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("account-1", "Member")]
    [InlineData("coach-account", "Coach")]
    public async Task BankTransfer_MemberAndCoachCannotConfirm(string accountId, string role)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = CreateAuthenticatedClient(factory,
            CreateToken(accountId, accountId + "@example.com", role, DateTime.UtcNow.AddMinutes(10)));
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/MembershipInvoice/1/confirm-bank-transfer",
            new ConfirmBankTransferAPIViewModel { Amount = 500000, ConfirmReceivedTransfer = true });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BankTransfer_AnonymousCannotReadQr()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/MembershipInvoice/1/bank-transfer");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
