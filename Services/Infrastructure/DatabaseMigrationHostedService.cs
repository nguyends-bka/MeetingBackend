using MeetingBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services.Infrastructure;

public sealed class DatabaseMigrationHostedService : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
    ];

    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(IServiceProvider services, ILogger<DatabaseMigrationHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Best-effort migration with retry so the app can start even if Postgres
        // is temporarily unavailable (e.g., container cold start / recovery).
        for (var attempt = 1; attempt <= RetryDelays.Length && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(stoppingToken);
                _logger.LogInformation("Database migrations applied successfully.");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = RetryDelays[attempt - 1];
                _logger.LogWarning(ex, "Database migration attempt {Attempt} failed; retrying in {Delay}s", attempt, delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        _logger.LogError("Database migrations did not succeed after {Attempts} attempts; continuing without blocking startup.", RetryDelays.Length);
    }
}
