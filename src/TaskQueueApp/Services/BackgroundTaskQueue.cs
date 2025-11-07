using System.Threading.Channels;

namespace TaskQueueApp.Services;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<WorkItem> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _queue = Channel.CreateBounded<WorkItem>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem, cancellationToken);
    }

    public async ValueTask<WorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}
