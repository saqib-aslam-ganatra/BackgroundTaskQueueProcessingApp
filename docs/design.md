# Background Task Queue Processing App - Design Document

## 1. Project Type Selection

The solution uses an **ASP.NET Core Web API (minimal API)** project. This template offers:

- **Broad hosting support** – the same project can run in Kestrel self-hosting, behind IIS, in Azure App Service, or even as a Windows Service/systemd service thanks to `UseWindowsService()`/`UseSystemd()` hooks.
- **Lightweight footprint** – minimal APIs start quickly and reduce cold-start overhead compared to MVC-heavy templates.
- **Integrated background processing** – the ASP.NET Core hosting model natively supports `IHostedService` and dependency injection, making it simple to run queue workers alongside HTTP endpoints.

## 2. Architecture Overview

```mermaid
graph TD
    ExternalSystems[External systems / DB triggers]
    SimulatedEndpoint[Simulated Update Endpoint]
    BackgroundQueue[In-memory Background Task Queue]
    Worker[Background Worker Service]
    Logger[Logging & Monitoring]
    HealthEndpoint[Health Check Endpoint]
    HostingEnvironment[Hosting Environment]

    ExternalSystems -->|Database change webhook| SimulatedEndpoint
    SimulatedEndpoint -->|Enqueue WorkItem| BackgroundQueue
    BackgroundQueue -->|Dequeue| Worker
    Worker -->|Process task & log| Logger
    Worker -->|Result| ExternalSystems
    HealthEndpoint -->|Warm-up ping| HostingEnvironment
```

- **Queue Producer** – In production this would be a notification from the database change-tracking mechanism. The prototype simulates this with an HTTP endpoint (`POST /simulate-update`).
- **Queue** – An in-memory bounded channel implementing `IBackgroundTaskQueue`. It provides back-pressure to avoid unbounded memory consumption (capacity 200 in the prototype).
- **Consumer** – `TaskProcessingService` (`BackgroundService`) that continuously dequeues and processes work items.
- **Health Monitoring** – `/health` endpoint wired through ASP.NET Core Health Checks so IIS/Azure can keep the app warm.
- **Hosting** – The same app can be run via the `dotnet` CLI (Kestrel), IIS (ASP.NET Core Module), Azure App Service, or as a Windows/systemd service for worker-style hosting.

## 3. IIS and Azure Configuration to Avoid Shutdowns

### IIS
- Enable **Always On** in the application pool to prevent idle shutdown.
- Disable or extend the **Idle Time-out** setting (set to `0` for no timeout or a sufficiently high value).
- Use the **Start Mode = AlwaysRunning** option (requires IIS 8.0+) to keep the worker process alive.
- Configure the ASP.NET Core Module with `hostingModel="InProcess"` for faster startup or `OutOfProcess` when self-contained.
- Register an application initialization URL (e.g., `/health`) so IIS performs warm-up requests.

### Azure App Service
- Enable **Always On** in the App Service settings to keep the site warm.
- Configure **Application Initialization** (in `applicationHost.xdt`) to ping `/health` so the worker is ready before swaps.
- Consider setting `WEBSITE_AUTO_SWAP_SLOT_NAME` with warm-up enabled for zero-downtime deployments.
- Use the **Application Initialization** feature or deployment slots with auto-swap to reduce cold starts.
- For background-only scenarios, pair the site with **Azure WebJobs** or **Functions** for triggered execution while keeping the web app warm.

## 4. Cost Minimization Strategy in Azure
- Use an **App Service Plan** in the **Basic** tier or **Premium v2/v3** with the smallest instance size that meets CPU/memory requirements. Combine with auto-scale rules based on CPU/queue length metrics.
- Leverage **Azure Functions** or **WebJobs** for bursty workloads if the queue can be externalized (e.g., Storage Queue) while keeping the API on the smaller App Service instance.
- Monitor queue length and worker throughput to adjust scaling dynamically, avoiding over-provisioning.
- Use **Application Insights sampling** and logging levels tuned to reduce storage and telemetry costs.
- For development/test, use the **Free** or **Shared** tiers, accepting their idle timeout, while production stays on a paid tier with Always On.

## 5. Prototype Behavior Summary
- `POST /simulate-update` simulates a database-triggered update and enqueues a work item.
- `TaskProcessingService` continuously dequeues and processes tasks with simulated work.
- `GET /tasks` returns recently processed items (last 5 minutes) for visibility.
- `GET /health` provides a readiness probe for load balancers, IIS warm-up, or Azure health checks.
- The host gracefully waits up to 30 seconds on shutdown to finish in-flight tasks, supporting smooth restarts.
