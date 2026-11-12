using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.Tests;

public sealed class MembershipTermsPersistenceTests
{
    [Fact]
    public async Task Acceptance_UniquePerMemberAndVersion()
    {
        await using PersistenceFixture fixture = await PersistenceFixture.CreateAsync();
        fixture.Context.MembershipTermAcceptances.AddRange(
            Acceptance(fixture.Member.AccountId, "membership-terms-v1"),
            Acceptance(fixture.Member.AccountId, "membership-terms-v1"));

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task SubscriptionDiscipline_DeletesWithSubscription()
    {
        await using PersistenceFixture fixture = await PersistenceFixture.CreateAsync();
        MemberSubscription subscription = fixture.Subscription(hasRestrictions: true);
        subscription.SubscriptionDisciplines.Add(new MemberSubscriptionDiscipline
        {
            DisciplineId = 7,
            DisciplineName = "Yoga"
        });
        fixture.Context.MemberSubscriptions.Add(subscription);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.MemberSubscriptions.Remove(subscription);
        await fixture.Context.SaveChangesAsync();

        Assert.Empty(await fixture.Context.MemberSubscriptionDisciplines.ToListAsync());
    }

    [Fact]
    public async Task UnrestrictedSubscription_IsExplicitEvenWithNoDisciplineRows()
    {
        await using PersistenceFixture fixture = await PersistenceFixture.CreateAsync();
        MemberSubscription unrestricted = fixture.Subscription(hasRestrictions: false);
        MemberSubscription restricted = fixture.Subscription(hasRestrictions: true);
        unrestricted.Status = "CONFIRMED";
        restricted.Status = "CONFIRMED";
        restricted.SubscriptionDisciplines.Add(new MemberSubscriptionDiscipline
        {
            DisciplineId = 9,
            DisciplineName = "Swimming"
        });
        fixture.Context.MemberSubscriptions.AddRange(unrestricted, restricted);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        List<MemberSubscription> saved = await fixture.Context.MemberSubscriptions
            .Include(item => item.SubscriptionDisciplines)
            .OrderBy(item => item.Id)
            .ToListAsync();

        Assert.False(saved[0].HasDisciplineRestrictions);
        Assert.Empty(saved[0].SubscriptionDisciplines);
        Assert.True(saved[1].HasDisciplineRestrictions);
        Assert.Single(saved[1].SubscriptionDisciplines);
    }

    private static MembershipTermAcceptance Acceptance(string memberId, string version) => new()
    {
        MemberId = memberId,
        TermsVersion = version,
        ContentHash = new string('a', 64),
        AcceptanceChannel = "MEMBER_WEB",
        AcceptedAtUtc = DateTime.UtcNow
    };

    private sealed class PersistenceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PersistenceFixture(
            SqliteConnection connection,
            SportsCenterManagementContext context,
            Member member,
            MembershipPackage package)
        {
            _connection = connection;
            Context = context;
            Member = member;
            Package = package;
        }

        public SportsCenterManagementContext Context { get; }
        public Member Member { get; }
        public MembershipPackage Package { get; }

        public static async Task<PersistenceFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            SportsCenterManagementContext context = new(
                new DbContextOptionsBuilder<SportsCenterManagementContext>()
                    .UseSqlite(connection)
                    .Options);
            await context.Database.EnsureCreatedAsync();

            Role role = new() { Id = 30, Name = "Member" };
            Account account = new()
            {
                Id = "member-terms-1",
                Email = "member.terms@example.test",
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
                MemberCode = "MEMTERMS1",
                FullName = "Member Terms",
                CreatedAt = DateTime.UtcNow
            };
            account.Member = member;
            MembershipPackage package = new()
            {
                Id = 30,
                Name = "Snapshot Package",
                Price = 500000m,
                DurationMonths = 1,
                Benefits = "[]",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.AddRange(role, account, package);
            await context.SaveChangesAsync();
            return new PersistenceFixture(connection, context, member, package);
        }

        public MemberSubscription Subscription(bool hasRestrictions) => new()
        {
            MemberId = Member.AccountId,
            Member = Member,
            PackageId = Package.Id,
            Package = Package,
            PackageName = Package.Name,
            PackagePrice = Package.Price,
            DurationMonths = Package.DurationMonths,
            Benefits = Package.Benefits,
            StartDate = new DateOnly(2026, 11, 12),
            EndDate = new DateOnly(2026, 12, 11),
            Kind = "REGISTER",
            Status = "PENDING_PAYMENT",
            HasDisciplineRestrictions = hasRestrictions,
            CreatedAt = DateTime.UtcNow
        };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
