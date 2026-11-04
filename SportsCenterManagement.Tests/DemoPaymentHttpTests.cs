using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SportsCenterManagement.Tests;

public partial class AuthenticationIntegrationTests
{
    [Fact]
    public async Task DemoPaymentRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = new AuthenticationWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/payments/demo/1/confirm", new { confirmSimulation = true });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
