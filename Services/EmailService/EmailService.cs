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

    public bool IsConfigured
    {
        get
        {
            bool hasUsername = !string.IsNullOrWhiteSpace(_options.Username);
            bool hasPassword = !string.IsNullOrWhiteSpace(_options.Password);
            return !string.IsNullOrWhiteSpace(_options.Host) &&
                _options.Port > 0 &&
                !string.IsNullOrWhiteSpace(_options.FromEmail) &&
                hasUsername == hasPassword;
        }
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

    public async Task SendEmailVerificationOtpAsync(
        string recipientEmail,
        string otp,
        string purpose,
        int expiresInMinutes)
    {
        ValidateConfiguration();

        string action = string.Equals(purpose, "LOGIN", StringComparison.Ordinal)
            ? "sign-in"
            : "registration";
        using MailMessage message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Sports Center email verification code",
            Body = "Your " + action + " verification code is " + otp +
                ". It expires in " + expiresInMinutes + " minutes.",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail));

        using SmtpClient client = CreateSmtpClient();
        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Email verification OTP delivery failed.");
            throw;
        }
    }

    public async Task SendMemberWelcomeAsync(
        string recipientEmail,
        string fullName,
        string initialPassword,
        string packageName,
        decimal amount)
    {
        ValidateConfiguration();

        using MailMessage message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Sports Center membership registration",
            Body = "Hello " + fullName + ",\n\n" +
                "Your account has been created for package " + packageName +
                " with amount " + amount + ".\n" +
                "Initial password: " + initialPassword + "\n\n" +
                "Please change this password after signing in.",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail));

        using SmtpClient client = CreateSmtpClient();
        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Member welcome email delivery failed.");
            throw;
        }
    }

    private SmtpClient CreateSmtpClient()
    {
        SmtpClient client = new SmtpClient(_options.Host, _options.Port)
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

        return client;
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
