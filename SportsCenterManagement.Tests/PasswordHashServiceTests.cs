using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class PasswordHashServiceTests
{
    private readonly PasswordHashService _service = new();

    [Fact]
    public void HashPassword_CreatesBcryptHashThatCanBeVerified()
    {
        const string password = "SecurePassword123!";

        string passwordHash = _service.HashPassword(password);

        Assert.NotEqual(password, passwordHash);
        Assert.StartsWith("$2", passwordHash);
        Assert.True(_service.VerifyPassword(password, passwordHash));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        string passwordHash = _service.HashPassword("correct-password");

        Assert.False(_service.VerifyPassword("wrong-password", passwordHash));
    }

    [Fact]
    public void VerifyPassword_WithMalformedHash_ReturnsFalse()
    {
        Assert.False(_service.VerifyPassword("password", "not-a-bcrypt-hash"));
    }
}
