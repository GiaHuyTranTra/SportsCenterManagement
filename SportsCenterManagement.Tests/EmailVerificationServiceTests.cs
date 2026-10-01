using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Services.EmailService;
using Services.EmailVerificationService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class EmailVerificationServiceTests
{
    [Fact]
    public async Task RequestAndVerify_WithDeliveredCode_ConsumesCodeAfterSuccess()
    {
        FakeEmailService email = new FakeEmailService();
        EmailVerificationService service = CreateService(email, Environments.Production);

        (RequestEmailVerificationResult result, int retryAfterSeconds, string? demoCode) =
            await service.RequestCodeAsync("Member@Example.com", "LOGIN");

        Assert.Equal(RequestEmailVerificationResult.Success, result);
        Assert.Equal(0, retryAfterSeconds);
        Assert.Null(demoCode);
        Assert.NotNull(email.LastCode);
        Assert.Equal(
            VerifyEmailCodeResult.Success,
            service.VerifyCode("member@example.com", "LOGIN", email.LastCode));
        Assert.Equal(
            VerifyEmailCodeResult.CodeNotFound,
            service.VerifyCode("member@example.com", "LOGIN", email.LastCode));
    }

    [Fact]
    public async Task RequestCode_WithoutSmtpInDevelopment_ReturnsServerGeneratedDemoCode()
    {
        FakeEmailService email = new FakeEmailService { ShouldFail = true };
        EmailVerificationService service = CreateService(email, Environments.Development);

        (RequestEmailVerificationResult result, int retryAfterSeconds, string? demoCode) =
            await service.RequestCodeAsync("new@example.com", "REGISTER");

        Assert.Equal(RequestEmailVerificationResult.Success, result);
        Assert.Equal(0, retryAfterSeconds);
        Assert.Matches("^[0-9]{6}$", demoCode);
        Assert.Equal(
            VerifyEmailCodeResult.Success,
            service.VerifyCode(
                "new@example.com",
                "REGISTER",
                demoCode,
                consumeOnSuccess: false));
    }

    [Fact]
    public async Task RequestCode_WithoutSmtpInProduction_FailsClosed()
    {
        FakeEmailService email = new FakeEmailService { ShouldFail = true };
        EmailVerificationService service = CreateService(email, Environments.Production);

        (RequestEmailVerificationResult result, int retryAfterSeconds, string? demoCode) =
            await service.RequestCodeAsync("new@example.com", "REGISTER");

        Assert.Equal(RequestEmailVerificationResult.DeliveryFailed, result);
        Assert.Equal(0, retryAfterSeconds);
        Assert.Null(demoCode);
        Assert.Equal(
            VerifyEmailCodeResult.CodeNotFound,
            service.VerifyCode("new@example.com", "REGISTER", "123456"));
    }

    [Fact]
    public async Task RequestCode_DuringCooldown_ReturnsRetryDelay()
    {
        FakeEmailService email = new FakeEmailService();
        EmailVerificationService service = CreateService(email, Environments.Production);
        await service.RequestCodeAsync("member@example.com", "LOGIN");

        (RequestEmailVerificationResult result, int retryAfterSeconds, string? demoCode) =
            await service.RequestCodeAsync("member@example.com", "LOGIN");

        Assert.Equal(RequestEmailVerificationResult.CooldownActive, result);
        Assert.InRange(retryAfterSeconds, 1, 60);
        Assert.Null(demoCode);
    }

    private static EmailVerificationService CreateService(
        FakeEmailService email,
        string environmentName)
    {
        MemoryCache cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        return new EmailVerificationService(
            cache,
            email,
            new FakeHostEnvironment { EnvironmentName = environmentName },
            new PasswordHashService());
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool ShouldFail { get; set; }

        public string? LastCode { get; private set; }

        public Task SendPasswordChangeOtpAsync(
            string recipientEmail,
            string otp,
            int expiresInMinutes)
        {
            return Task.CompletedTask;
        }

        public Task SendEmailVerificationOtpAsync(
            string recipientEmail,
            string otp,
            string purpose,
            int expiresInMinutes)
        {
            LastCode = otp;
            return ShouldFail
                ? Task.FromException(new InvalidOperationException("SMTP unavailable"))
                : Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "SportsCenterManagement.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
