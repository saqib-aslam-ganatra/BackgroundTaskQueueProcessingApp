namespace TaskQueueApp.Services;

public record WorkItem(string TableName, string Payload, DateTimeOffset EnqueuedAt)
{
    public Guid Id { get; } = Guid.NewGuid();
}

public record ProcessedWorkItem(Guid Id, string TableName, string Payload, DateTimeOffset EnqueuedAt, DateTimeOffset CompletedAt);
