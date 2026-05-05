using Microsoft.Extensions.Hosting;

namespace MeetingBackend.Services;

public sealed class BackgroundTaskService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<BackgroundTaskService> _logger;

    public BackgroundTaskService(IBackgroundTaskQueue taskQueue, ILogger<BackgroundTaskService> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundTaskService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);
                await workItem(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackgroundTaskService failed running work item");
            }
        }
    }
}
