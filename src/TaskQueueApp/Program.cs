using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskQueueApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddSingleton<TaskProcessingService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaskProcessingService>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/simulate-update", async (SimulatedUpdateRequest request, IBackgroundTaskQueue queue, CancellationToken cancellationToken) =>
{
    var workItem = new WorkItem(request.TableName, request.Payload, DateTimeOffset.UtcNow);
    await queue.QueueBackgroundWorkItemAsync(workItem, cancellationToken);
    return Results.Accepted($"/tasks/{workItem.Id}", new { message = "Task queued", workItem.Id });
});

app.MapGet("/tasks", (TaskProcessingService processor) => Results.Ok(processor.RecentlyProcessedItems));

app.Run();

internal record SimulatedUpdateRequest(string TableName, string Payload);

namespace TaskQueueApp.Services
{
    public record WorkItem(string TableName, string Payload, DateTimeOffset EnqueuedAt)
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public interface IBackgroundTaskQueue
    {
        ValueTask QueueBackgroundWorkItemAsync(WorkItem workItem, CancellationToken cancellationToken = default);
        ValueTask<WorkItem> DequeueAsync(CancellationToken cancellationToken);
    }

    public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<WorkItem> _queue;

        public BackgroundTaskQueue()
        {
            var options = new BoundedChannelOptions(100)
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

    public record ProcessedWorkItem(Guid Id, string TableName, string Payload, DateTimeOffset EnqueuedAt, DateTimeOffset CompletedAt);
}
