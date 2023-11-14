using Liberex.Internal;

namespace Liberex.HostServices;

public class QueuedHostedService : BackgroundService
{
    private readonly ILogger<QueuedHostedService> _logger;
    private readonly PriorityTaskQueue _taskQueue;

    public QueuedHostedService(ILogger<QueuedHostedService> logger, PriorityTaskQueue taskQueue)
    {
        _logger = logger;
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Queued Hosted Service is starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.ReadAsync(cancellationToken);

            try
            {
                await workItem(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
            }
        }

        _logger.LogInformation("Queued Hosted Service is stopping.");
    }
}