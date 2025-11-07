namespace TaskQueueApp.Services;

public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    ValueTask<WorkItem> DequeueAsync(CancellationToken cancellationToken);
}
