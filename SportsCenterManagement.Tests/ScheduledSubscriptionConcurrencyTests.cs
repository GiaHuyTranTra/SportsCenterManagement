using APIViewModel.MemberSubscription;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.EmailService;
using Services.MemberSubscriptionService;
using Services.MembershipTermsService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class ScheduledSubscriptionConcurrencyTests
{
    [Fact]
    public async Task TwoOrderAttempts_CommitOnlyOnePendingOrder()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = new(new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        Role memberRole = new() { Id = 1, Name = "Member" };
        Account account = new()
        {
            Id = "member", Email = "member@example.com", PasswordHash = "unused", Status = "Active",
            Role = memberRole, RoleId = memberRole.Id, CreatedAt = DateTime.UtcNow
        };
        account.Member = new Member
        {
            AccountId = account.Id, Account = account, MemberCode = "MEM001", FullName = "Member",
            CreatedAt = DateTime.UtcNow
        };
        context.Accounts.Add(account);
        context.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1, Name = "Package", Price = 500_000m, DurationMonths = 1, Benefits = "[]",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        context.MembershipTermAcceptances.Add(new MembershipTermAcceptance
        {
            MemberId = account.Id,
            TermsVersion = MembershipTermsCatalog.CurrentVersion,
            ContentHash = MembershipTermsCatalog.CurrentHash,
            AcceptanceChannel = "MEMBER_WEB",
            AcceptedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BusinessSettings:TimeZoneId"] = "UTC" }).Build();
        MemberSubscriptionService service = new(
            context, configuration, new PasswordHashService(), new NoopEmailService(),
            new MembershipTermsService(context));
        RegisterMemberSubscriptionAPIViewModel request = new() { PackageId = 1, PaymentMethod = "CARD" };

        RegisterSubscriptionResult first = (await service.RegisterOrRenewAsync(account.Id, request)).Result;
        RegisterSubscriptionResult second = (await service.RegisterOrRenewAsync(account.Id, request)).Result;

        Assert.Equal(RegisterSubscriptionResult.Success, first);
        Assert.Equal(RegisterSubscriptionResult.PendingOrderExists, second);
        Assert.Equal(1, await context.MemberSubscriptions.CountAsync(item => item.Status == "PENDING_PAYMENT"));
        Assert.Single(context.MembershipInvoices);
    }

    private sealed class NoopEmailService : IEmailService
    {
        public bool IsConfigured => false;
        public Task SendPasswordChangeOtpAsync(string recipientEmail, string otp, int expiresInMinutes) => Task.CompletedTask;
        public Task SendMemberAccountWelcomeAsync(string recipientEmail, string fullName, string initialPassword) => Task.CompletedTask;
        public Task SendEmailVerificationOtpAsync(string recipientEmail, string otp, string purpose, int expiresInMinutes) => Task.CompletedTask;
    }
}
