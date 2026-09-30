namespace Services.Utils;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = null!;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromEmail { get; set; } = null!;

    public string FromName { get; set; } = "Sports Center Management";
}
