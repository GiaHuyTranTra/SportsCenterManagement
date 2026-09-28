namespace Services.Interfaces;

public interface IEmailService
{
	Task SendDefaultPasswordEmailAsync(string toEmail, string fullName, string defaultPassword);
}
