using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MeetingBackend.Data;
using MeetingBackend.Entities;

namespace MeetingBackend.Services.Meeting;

public class MeetingCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MeetingCleanupWorker> _logger;

    public MeetingCleanupWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MeetingCleanupWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MeetingCleanupWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanUpMeetingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during meeting cleanup.");
            }

            // Run every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("MeetingCleanupWorker stopped.");
    }

    private async Task CleanUpMeetingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // 1. Process NoShow meetings:
        // Transition Upcoming meetings where now > EstimatedEndUtc (or now > CreatedAt + 1h) and StartedAt is null
        var upcomingMeetings = await dbContext.Meetings
            .Where(m => m.Status == MeetingStatus.Upcoming && m.StartedAt == null)
            .ToListAsync(cancellationToken);

        int noShowCount = 0;
        foreach (var m in upcomingMeetings)
        {
            var estimatedEnd = m.EstimatedEndAt ?? m.CreatedAt.AddHours(1);
            if (now > estimatedEnd)
            {
                m.Status = MeetingStatus.NoShow;
                noShowCount++;
            }
        }

        // 2. Process Auto-close meetings:
        // Transition Live meetings with activeParticipantCount == 0 for 15+ consecutive minutes AND now > EstimatedEndAt to Ended
        var liveMeetings = await dbContext.Meetings
            .Where(m => m.Status == MeetingStatus.Live)
            .ToListAsync(cancellationToken);

        int autoEndedCount = 0;
        foreach (var m in liveMeetings)
        {
            var estimatedEnd = m.EstimatedEndAt ?? m.CreatedAt.AddHours(1);
            if (now <= estimatedEnd)
            {
                // Must be past estimated end time to auto-close
                continue;
            }

            var activeCount = await dbContext.MeetingParticipants
                .Where(p => p.MeetingId == m.Id && p.LeftAt == null)
                .CountAsync(cancellationToken);

            if (activeCount == 0)
            {
                var lastLeft = await dbContext.MeetingParticipants
                    .Where(p => p.MeetingId == m.Id && p.LeftAt != null)
                    .OrderByDescending(p => p.LeftAt)
                    .Select(p => p.LeftAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var emptySince = lastLeft ?? m.StartedAt ?? m.CreatedAt;
                if (now - emptySince >= TimeSpan.FromMinutes(15))
                {
                    m.Status = MeetingStatus.Ended;
                    m.EndedAt = now;
                    autoEndedCount++;
                }
            }
        }

        if (noShowCount > 0 || autoEndedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleanup finished. Marked {NoShowCount} as NoShow, auto-closed {AutoEndedCount} empty meetings.", noShowCount, autoEndedCount);
        }
    }
}
