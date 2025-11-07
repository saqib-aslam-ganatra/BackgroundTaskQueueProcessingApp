using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using TaskQueueApp;
using TaskQueueApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSystemd();

if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Host.UseWindowsService();
}

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(capacity: 200));
builder.Services.AddSingleton<TaskProcessingService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaskProcessingService>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

app.MapPost("/simulate-update", async (SimulatedUpdateRequest request, IBackgroundTaskQueue queue, CancellationToken cancellationToken) =>
{
    var workItem = new WorkItem(request.TableName, request.Payload, DateTimeOffset.UtcNow);
    await queue.QueueBackgroundWorkItemAsync(workItem, cancellationToken);
    return Results.Accepted($"/tasks/{workItem.Id}", new { message = "Task queued", workItem.Id });
});

app.MapGet("/tasks", (TaskProcessingService processor) => Results.Ok(processor.RecentlyProcessedItems));

app.Run();
