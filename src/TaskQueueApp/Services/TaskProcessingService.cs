using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;

namespace TaskQueueApp.Services;

public sealed class TaskProcessingService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<TaskProcessingService> _logger;
    private readonly List<ProcessedWorkItem> _recentlyProcessed = new();
    private readonly TimeSpan _historyWindow = TimeSpan.FromMinutes(5);

    public TaskProcessingService(IBackgroundTaskQueue taskQueue, ILogger<TaskProcessingService> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    public IReadOnlyCollection<ProcessedWorkItem> RecentlyProcessedItems
    {
        get
        {
            lock (_recentlyProcessed)
            {
                _recentlyProcessed.RemoveAll(item => item.CompletedAt < DateTimeOffset.UtcNow - _historyWindow);
                return _recentlyProcessed.ToArray();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskProcessingService is starting");

        await foreach (var workItem in DequeueWorkItemsAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing work item {WorkItemId} from table {Table}", workItem.Id, workItem.TableName);
                await ProcessWorkItemAsync(workItem, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing work item {WorkItemId}", workItem.Id);
            }
        }

        _logger.LogInformation("TaskProcessingService is stopping");
    }

    private async IAsyncEnumerable<WorkItem> DequeueWorkItemsAsync([EnumeratorCancellation] CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            yield return await _taskQueue.DequeueAsync(stoppingToken);
        }
    }

    private async Task ProcessWorkItemAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        var processed = new ProcessedWorkItem(workItem.Id, workItem.TableName, workItem.Payload, workItem.EnqueuedAt, DateTimeOffset.UtcNow);

        lock (_recentlyProcessed)
        {
            _recentlyProcessed.Add(processed);
        }

        _logger.LogInformation("Completed work item {WorkItemId}", workItem.Id);
    }
}
