using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations;

public class EmailService : IEmailService
{
	private readonly ILogger<EmailService> _logger;

	public EmailService(ILogger<EmailService> logger) => _logger = logger;

	public Task SendDefaultPasswordEmailAsync(string toEmail, string fullName, string defaultPassword)
	{
		_logger.LogInformation("Email sent to {ToEmail} ({FullName}); temporary password: {Password}", toEmail, fullName, defaultPassword);
		return Task.CompletedTask;
	}
}
