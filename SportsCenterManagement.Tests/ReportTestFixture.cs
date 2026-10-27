using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SportsCenterManagement.Tests;

internal sealed class ReportTestFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public SportsCenterManagementContext Context { get; }
    public IConfiguration Configuration { get; }
    public Member Member { get; }
    public Account Manager { get; }
    public MembershipPackage Package { get; }

    private ReportTestFixture(SqliteConnection connection, SportsCenterManagementContext context,
        Account manager, Member member, MembershipPackage package)
    {
        _connection = connection;
        Context = context;
        Manager = manager;
        Member = member;
        Package = package;
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BusinessSettings:TimeZoneId"] = "SE Asia Standard Time"
        }).Build();
    }

    public static async Task<ReportTestFixture> CreateAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>().UseSqlite(connection).Options;
        SportsCenterManagementContext context = new(options);
        await context.Database.EnsureCreatedAsync();
        DateTime created = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        Role managerRole = new() { Id = 1, Name = "CenterManager" };
        Role memberRole = new() { Id = 3, Name = "Member" };
        Account manager = new()
        {
            Id = "report-manager", Email = "report.manager@example.com", PasswordHash = "unused",
            Status = "Active", Role = managerRole, CreatedAt = created
        };
        Member member = new()
        {
            AccountId = "report-member", MemberCode = "REPORT001", CreatedAt = created,
            Account = new Account
            {
                Id = "report-member", Email = "report.member@example.com", PasswordHash = "unused",
                Status = "Active", Role = memberRole, CreatedAt = created
            }
        };
        MembershipPackage package = new()
        {
            Name = "Report monthly", Price = 500000m, Benefits = "[]", DurationMonths = 1,
            IsActive = true, CreatedAt = created
        };
        context.Accounts.Add(manager);
        context.Members.Add(member);
        context.MembershipPackages.Add(package);
        await context.SaveChangesAsync();
        return new(connection, context, manager, member, package);
    }

    public MembershipInvoice AddInvoice(decimal amount, string status, DateTime? paidAt,
        string method = "CASH", string kind = "REGISTER")
    {
        MemberSubscription subscription = new()
        {
            Member = Member, Package = Package, PackageName = Package.Name,
            PackagePrice = Package.Price, DurationMonths = 1, Benefits = "[]",
            StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31), Kind = kind,
            Status = status == "PAID" ? "CONFIRMED" : status,
            CreatedAt = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        MembershipInvoice invoice = new()
        {
            InvoiceNumber = "REPORT-" + Guid.NewGuid().ToString("N"), Amount = amount, Status = status,
            PaidAt = paidAt, PaymentMethod = method, Member = Member, Subscription = subscription,
            CreatedByNavigation = Manager, CreatedAt = subscription.CreatedAt,
            PaidByNavigation = status == "PAID" ? Manager : null
        };
        Context.MembershipInvoices.Add(invoice);
        return invoice;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
