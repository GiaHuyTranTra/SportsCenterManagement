using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Fact]
    public async Task TransferQrCreationRequiresLogin()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/api/payments/demo/1/qr", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("confirm")]
    public async Task PhonePaymentRejectsMissingQrCapability(string action)
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/payments/demo/qr/" + action, new { token = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DemoPaymentRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/payments/demo/1/confirm", new { confirmSimulation = true });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
