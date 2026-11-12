namespace Services.EmailService;

public interface IEmailService
{
    bool IsConfigured { get; }

    Task SendPasswordChangeOtpAsync(string recipientEmail, string otp, int expiresInMinutes);

    Task SendMemberAccountWelcomeAsync(string recipientEmail, string fullName, string initialPassword);

    Task SendEmailVerificationOtpAsync(string recipientEmail, string otp, string purpose, int expiresInMinutes);
}
