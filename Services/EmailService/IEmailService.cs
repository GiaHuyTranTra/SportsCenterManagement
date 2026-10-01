namespace Services.EmailService;

public interface IEmailService
{
    Task SendPasswordChangeOtpAsync(
        string recipientEmail,
        string otp,
        int expiresInMinutes);

    Task SendEmailVerificationOtpAsync(
        string recipientEmail,
        string otp,
        string purpose,
        int expiresInMinutes);
}
