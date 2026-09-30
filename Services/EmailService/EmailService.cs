using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Utils;
using System.Net;
using System.Net.Mail;

namespace Services.EmailService;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordChangeOtpAsync(
        string recipientEmail,
        string otp,
        int expiresInMinutes)
    {
        ValidateConfiguration();

        using MailMessage message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Sports Center password change code",
            Body = "Your password change code is " + otp +
                ". It expires in " + expiresInMinutes + " minutes.",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail));

        using SmtpClient client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_options.Username) &&
            !string.IsNullOrWhiteSpace(_options.Password))
        {
            client.Credentials = new NetworkCredential(
                _options.Username,
                _options.Password);
        }

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Password change OTP email delivery failed.");
            throw;
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            _options.Port <= 0 ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Email SMTP configuration is incomplete.");
        }

        bool hasUsername = !string.IsNullOrWhiteSpace(_options.Username);
        bool hasPassword = !string.IsNullOrWhiteSpace(_options.Password);
        if (hasUsername != hasPassword)
        {
            throw new InvalidOperationException(
                "Email username and password must be configured together.");
        }
    }
}
