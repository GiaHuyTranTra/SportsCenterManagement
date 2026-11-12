using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Services.MembershipTermsService;
using System.Text.Json;

namespace SportsCenterManagement.Tests;

public sealed class MembershipTermsServiceTests
{
    [Fact]
    public async Task CurrentTerms_ReturnsCatalogAndNotAcceptedBeforeRecordExists()
    {
        await using TermsFixture fixture = await TermsFixture.CreateAsync();

        var current = await fixture.Service.GetCurrentAsync(fixture.Member.AccountId);

        Assert.Equal("membership-terms-v1", current.Version);
        Assert.Equal(64, current.ContentHash.Length);
        Assert.NotEmpty(current.Sections);
        Assert.Equal(
            new[] { "PACKAGE_NON_CANCELABLE", "NO_MID_TERM_CHANGE", "SCHEDULED_NEXT_PERIOD", "CENTER_RULES_AND_SAFETY" },
            current.RequiredAcknowledgements.Select(item => item.Id));
        Assert.False(current.Accepted);
        Assert.Null(current.AcceptedAtUtc);
    }

    [Fact]
    public async Task AcceptCurrent_UsesMemberIdentityAndIsIdempotent()
    {
        await using TermsFixture fixture = await TermsFixture.CreateAsync();

        AcceptMembershipTermsResult first = await fixture.Service.AcceptCurrentAsync(
            fixture.Member.AccountId,
            MembershipTermsCatalog.CurrentVersion,
            MembershipTermsCatalog.CurrentHash);
        AcceptMembershipTermsResult second = await fixture.Service.AcceptCurrentAsync(
            fixture.Member.AccountId,
            MembershipTermsCatalog.CurrentVersion,
            MembershipTermsCatalog.CurrentHash);

        Assert.Equal(AcceptMembershipTermsResult.Success, first);
        Assert.Equal(AcceptMembershipTermsResult.Success, second);
        MembershipTermAcceptance saved = Assert.Single(await fixture.Context.MembershipTermAcceptances.ToListAsync());
        Assert.Equal(fixture.Member.AccountId, saved.MemberId);
        Assert.Equal("MEMBER_WEB", saved.AcceptanceChannel);
        Assert.Equal(MembershipTermsCatalog.CurrentHash, saved.ContentHash);
    }

    [Theory]
    [InlineData("membership-terms-old", null)]
    [InlineData(null, "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public async Task AcceptCurrent_RejectsWrongVersionOrHashWithoutWriting(string? version, string? hash)
    {
        await using TermsFixture fixture = await TermsFixture.CreateAsync();

        AcceptMembershipTermsResult result = await fixture.Service.AcceptCurrentAsync(
            fixture.Member.AccountId,
            version ?? MembershipTermsCatalog.CurrentVersion,
            hash ?? MembershipTermsCatalog.CurrentHash);

        Assert.Equal(AcceptMembershipTermsResult.VersionMismatch, result);
        Assert.Empty(await fixture.Context.MembershipTermAcceptances.ToListAsync());
    }

    [Theory]
    [InlineData("Inactive", false, false)]
    [InlineData("Active", true, false)]
    [InlineData("Active", false, true)]
    public async Task AcceptCurrent_RejectsIneligibleMember(string status, bool locked, bool deleted)
    {
        await using TermsFixture fixture = await TermsFixture.CreateAsync();
        fixture.Member.Account.Status = status;
        fixture.Member.Account.IsLocked = locked;
        fixture.Member.Account.DeletedAt = deleted ? DateTime.UtcNow : null;
        await fixture.Context.SaveChangesAsync();

        AcceptMembershipTermsResult result = await fixture.Service.AcceptCurrentAsync(
            fixture.Member.AccountId,
            MembershipTermsCatalog.CurrentVersion,
            MembershipTermsCatalog.CurrentHash);

        Assert.Equal(AcceptMembershipTermsResult.MemberNotEligible, result);
        Assert.Empty(await fixture.Context.MembershipTermAcceptances.ToListAsync());
    }

    [Fact]
    public async Task StaffStatus_ReturnsOnlyAcceptanceSummary()
    {
        await using TermsFixture fixture = await TermsFixture.CreateAsync();
        await fixture.Service.AcceptCurrentAsync(
            fixture.Member.AccountId,
            MembershipTermsCatalog.CurrentVersion,
            MembershipTermsCatalog.CurrentHash);

        var status = await fixture.Service.GetMemberStatusAsync(fixture.Member.AccountId);
        string json = JsonSerializer.Serialize(status);

        Assert.True(status.Accepted);
        Assert.Equal(MembershipTermsCatalog.CurrentVersion, status.AcceptedVersion);
        Assert.DoesNotContain("sections", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contentHash", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TermsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TermsFixture(
            SqliteConnection connection,
            SportsCenterManagementContext context,
            Member member,
            MembershipTermsService service)
        {
            _connection = connection;
            Context = context;
            Member = member;
            Service = service;
        }

        public SportsCenterManagementContext Context { get; }
        public Member Member { get; }
        public MembershipTermsService Service { get; }

        public static async Task<TermsFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            SportsCenterManagementContext context = new(
                new DbContextOptionsBuilder<SportsCenterManagementContext>()
                    .UseSqlite(connection)
                    .Options);
            await context.Database.EnsureCreatedAsync();
            Role role = new() { Id = 70, Name = "Member" };
            Account account = new()
            {
                Id = "terms-member",
                Email = "terms.member@example.test",
                PasswordHash = "unused",
                Status = "Active",
                RoleId = role.Id,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };
            Member member = new()
            {
                AccountId = account.Id,
                Account = account,
                MemberCode = "TERMS001",
                FullName = "Terms Member",
                CreatedAt = DateTime.UtcNow
            };
            account.Member = member;
            context.AddRange(role, account);
            await context.SaveChangesAsync();
            return new TermsFixture(connection, context, member, new MembershipTermsService(context));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
