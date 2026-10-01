using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Services.EmailService;
using Services.PasswordHashService;
using System.Globalization;
using System.Security.Cryptography;

namespace Services.EmailVerificationService;

public class EmailVerificationService : IEmailVerificationService
{
    private const int CooldownSeconds = 60;
    private const int ExpirationMinutes = 5;
    private const int MaximumAttempts = 5;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly IHostEnvironment _environment;
    private readonly IPasswordHashService _passwordHashService;

    public EmailVerificationService(
        IMemoryCache cache,
        IEmailService emailService,
        IHostEnvironment environment,
        IPasswordHashService passwordHashService)
    {
        _cache = cache;
        _emailService = emailService;
        _environment = environment;
        _passwordHashService = passwordHashService;
    }

    public async Task<(
        RequestEmailVerificationResult Result,
        int RetryAfterSeconds,
        string? DemoCode)> RequestCodeAsync(string email, string purpose)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        string normalizedPurpose = purpose.Trim().ToUpperInvariant();
        string cacheKey = CreateCacheKey(normalizedEmail, normalizedPurpose);
        DateTime nowUtc = DateTime.UtcNow;

        if (_cache.TryGetValue(cacheKey, out EmailVerificationCacheEntry? existing) &&
            existing is not null)
        {
            DateTime cooldownEndsAt = existing.CreatedAtUtc.AddSeconds(CooldownSeconds);
            if (cooldownEndsAt > nowUtc)
            {
                int retryAfterSeconds = Math.Max(
                    1,
                    (int)Math.Ceiling((cooldownEndsAt - nowUtc).TotalSeconds));
                return (RequestEmailVerificationResult.CooldownActive, retryAfterSeconds, null);
            }
        }

        string rawCode = RandomNumberGenerator
            .GetInt32(0, 1000000)
            .ToString("D6", CultureInfo.InvariantCulture);
        EmailVerificationCacheEntry entry = new EmailVerificationCacheEntry
        {
            CodeHash = _passwordHashService.HashPassword(rawCode),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddMinutes(ExpirationMinutes),
            FailedAttempts = 0
        };
        _cache.Set(cacheKey, entry, entry.ExpiresAtUtc);

        try
        {
            await _emailService.SendEmailVerificationOtpAsync(
                normalizedEmail,
                rawCode,
                normalizedPurpose,
                ExpirationMinutes);
            return (RequestEmailVerificationResult.Success, 0, _environment.IsDevelopment() ? rawCode : null);
        }
        catch (Exception)
        {
            if (_environment.IsDevelopment())
            {
                return (RequestEmailVerificationResult.Success, 0, rawCode);
            }

            _cache.Remove(cacheKey);
            return (RequestEmailVerificationResult.DeliveryFailed, 0, null);
        }
    }

    public VerifyEmailCodeResult VerifyCode(
        string email,
        string purpose,
        string? code,
        bool consumeOnSuccess = true)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return VerifyEmailCodeResult.CodeRequired;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        string normalizedPurpose = purpose.Trim().ToUpperInvariant();
        string cacheKey = CreateCacheKey(normalizedEmail, normalizedPurpose);
        if (!_cache.TryGetValue(cacheKey, out EmailVerificationCacheEntry? entry) ||
            entry is null)
        {
            return VerifyEmailCodeResult.CodeNotFound;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _cache.Remove(cacheKey);
            return VerifyEmailCodeResult.CodeExpired;
        }

        if (entry.FailedAttempts >= MaximumAttempts)
        {
            _cache.Remove(cacheKey);
            return VerifyEmailCodeResult.AttemptsExceeded;
        }

        if (!_passwordHashService.VerifyPassword(code.Trim(), entry.CodeHash))
        {
            entry.FailedAttempts++;
            if (entry.FailedAttempts >= MaximumAttempts)
            {
                _cache.Remove(cacheKey);
                return VerifyEmailCodeResult.AttemptsExceeded;
            }

            _cache.Set(cacheKey, entry, entry.ExpiresAtUtc);
            return VerifyEmailCodeResult.InvalidCode;
        }

        if (consumeOnSuccess)
        {
            _cache.Remove(cacheKey);
        }
        return VerifyEmailCodeResult.Success;
    }

    private static string CreateCacheKey(string email, string purpose)
    {
        return "email-verification:" + purpose + ":" + email;
    }

    private sealed class EmailVerificationCacheEntry
    {
        public string CodeHash { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public int FailedAttempts { get; set; }
    }
}
