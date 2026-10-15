namespace Services.EmailVerificationService;

public enum RequestEmailVerificationResult
{
    Success,
    CooldownActive,
    DeliveryFailed
}

public enum VerifyEmailCodeResult
{
    Success,
    CodeRequired,
    CodeNotFound,
    CodeExpired,
    InvalidCode,
    AttemptsExceeded
}

public interface IEmailVerificationService
{
    Task<(
        RequestEmailVerificationResult Result,
        int RetryAfterSeconds,
        string? DemoCode)> RequestCodeAsync(string email, string purpose);

    VerifyEmailCodeResult VerifyCode(
        string email,
        string purpose,
        string? code,
        bool consumeOnSuccess = true);
}
