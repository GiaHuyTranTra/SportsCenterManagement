namespace Services.EmailService;

public interface IEmailService
{
    Task SendPasswordChangeOtpAsync(
        string recipientEmail,
        string otp,
        int expiresInMinutes);
}
