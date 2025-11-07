using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
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

app.MapGet("/", () =>
    Results.Content(
        """
        <html>
            <head>
                <title>Background Task Queue Processing App</title>
            </head>
            <body>
                <h1>Background Task Queue Processing App</h1>
                <p>The service is running. Use the available API endpoints to interact with it:</p>
                <ul>
                    <li><code>POST /simulate-update</code> &ndash; enqueue a simulated update request.</li>
                    <li><code>GET /tasks</code> &ndash; view recently processed tasks.</li>
                    <li><code>GET /health</code> &ndash; health check endpoint for readiness probes.</li>
                </ul>
                <p>If you are running in the Development environment, you can also explore the API through <a href="/swagger">Swagger UI</a>.</p>
            </body>
        </html>
        """,
        "text/html"));

app.MapPost("/simulate-update", async (SimulatedUpdateRequest request, IBackgroundTaskQueue queue, CancellationToken cancellationToken) =>
{
    var workItem = new WorkItem(request.TableName, request.Payload, DateTimeOffset.UtcNow);
    await queue.QueueBackgroundWorkItemAsync(workItem, cancellationToken);
    return Results.Ok(new { message = "Task queued", id = workItem.Id });
});

app.MapGet("/tasks", (TaskProcessingService processor) => Results.Ok(processor.RecentlyProcessedItems));

app.Run();
