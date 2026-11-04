using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Fact]
    public async Task VnpayIpnRejectsForgedCallbackWithoutRequiringJwt()
    {
        await using WebApplicationFactory<Program> original = new AuthenticationWebApplicationFactory();
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using WebApplicationFactory<Program> factory = original.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["VnpaySandbox:Enabled"] = "true", ["VnpaySandbox:TmnCode"] = "TESTSHOP",
                    ["VnpaySandbox:HashSecret"] = secret, ["VnpaySandbox:CollectorAccountId"] = "manager-account"
                })));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/payments/vnpay-sandbox/ipn?vnp_ResponseCode=00");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"RspCode\":\"97\"", json);
        Assert.Contains("\"Message\":", json);
        using HttpResponseMessage duplicate = await client.GetAsync("/api/payments/vnpay-sandbox/ipn?vnp_Amount=1&vnp_Amount=2");
        Assert.Contains("\"RspCode\":\"97\"", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VnpayCheckoutRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/api/payments/vnpay-sandbox/1/checkout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
