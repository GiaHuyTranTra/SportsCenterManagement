namespace Services.EmailService;

public interface IEmailService
{
    bool IsConfigured { get; }

    Task SendPasswordChangeOtpAsync(
        string recipientEmail,
        string otp,
        int expiresInMinutes);

    Task SendEmailVerificationOtpAsync(
        string recipientEmail,
        string otp,
        string purpose,
        int expiresInMinutes);

    Task SendMemberWelcomeAsync(
        string recipientEmail,
        string fullName,
        string initialPassword,
        string packageName,
        decimal amount);
}
