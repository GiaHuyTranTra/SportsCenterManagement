using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.BackgroundJobs;

/// <summary>
/// Marks a SCHEDULED class session as COMPLETED once its end time has passed.
///
/// COMPLETED is a valid status everywhere — the check constraint allows it and
/// every schedule screen offers a "Đã hoàn thành" filter — but nothing in the
/// application ever wrote it. A session that ran yesterday therefore kept
/// showing "Đã lên lịch" forever and the completed filter was always empty.
///
/// Set BusinessSettings:AutoCompleteSessions to false to switch the job off.
/// </summary>
public sealed class SessionCompletionService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SessionCompletionService> _logger;

    public SessionCompletionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SessionCompletionService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("BusinessSettings:AutoCompleteSessions", true))
        {
            return;
        }

        using PeriodicTimer timer = new PeriodicTimer(Interval);
        do
        {
            await CompleteFinishedSessionsAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CompleteFinishedSessionsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            SportsCenterManagementContext context =
                scope.ServiceProvider.GetRequiredService<SportsCenterManagementContext>();

            // The in-memory provider used by the HTTP integration tests cannot
            // run SQL; there is nothing to complete there.
            if (!context.Database.IsRelational())
            {
                return;
            }

            TimeZoneInfo centreTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                _configuration["BusinessSettings:TimeZoneId"] ?? "SE Asia Standard Time");
            DateTime centreNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centreTimeZone);

            int completed = await context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE dbo.ClassSession
                   SET Status = 'COMPLETED', UpdatedAt = SYSUTCDATETIME()
                   WHERE Status = 'SCHEDULED'
                     AND DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS TIME), EndTime),
                                 CAST(SessionDate AS DATETIME2)) <= {centreNow}",
                stoppingToken);

            if (completed > 0)
            {
                _logger.LogInformation("Marked {Count} finished class sessions as COMPLETED.", completed);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down.
        }
        catch (Exception exception)
        {
            // A failed sweep must never take the API down; the next tick retries.
            _logger.LogWarning(exception, "Could not mark finished class sessions as COMPLETED.");
        }
    }
}
