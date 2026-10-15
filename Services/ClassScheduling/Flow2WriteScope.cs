using System.Data;
using System.Runtime.CompilerServices;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Services.ScheduleQueryService;

namespace Services.ClassScheduling;

public sealed class Flow2WriteScope : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<SportsCenterManagementContext, Flow2WriteScope> Active = new();
    private readonly SportsCenterManagementContext _context;
    private bool _disposed;
    private bool _committed;

    private Flow2WriteScope(SportsCenterManagementContext context, IDbContextTransaction? transaction)
    {
        _context = context;
        Transaction = transaction;
    }

    public Guid OperationId { get; } = Guid.NewGuid();
    public string? ActorAccountId { get; set; }
    public IDbContextTransaction? Transaction { get; }

    public static Flow2WriteScope? Current(SportsCenterManagementContext context) =>
        Active.TryGetValue(context, out Flow2WriteScope? scope) ? scope : null;

    public static async Task<Flow2WriteScope> BeginAsync(SportsCenterManagementContext context)
    {
        if (Current(context) is not null || context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("A Flow 2 command must own its transaction.");
        }

        IDbContextTransaction? transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead)
            : null;
        try
        {
            if (context.Database.IsSqlServer())
            {
                // All class commands share this database lock, including validation reads.
                await context.Database.ExecuteSqlRawAsync("""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource=N'SCMS:Flow2:Write',
                        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                    IF @result < 0 THROW 51010, 'Flow 2 write lock unavailable.', 1;
                    """);
            }
            Flow2WriteScope scope = new Flow2WriteScope(context, transaction);
            Active.Add(context, scope);
            return scope;
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task CommitAsync()
    {
        if (Transaction is not null) await Transaction.CommitAsync();
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Active.Remove(_context);
        if (Transaction is not null) await Transaction.DisposeAsync();
        if (!_committed) _context.ChangeTracker.Clear();
    }
}
