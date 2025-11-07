# Background Task Queue Processing App

## Overview
This ASP.NET Core minimal API application demonstrates how to keep a background worker online for processing database-driven tasks while remaining ready for HTTP requests. The same build can run self-hosted (Kestrel), behind IIS, in Azure App Service, or installed as a Windows service/systemd daemon.

## Deliverables
- Design rationale, hosting configuration guidance, and architecture diagram are captured in [`docs/design.md`](docs/design.md).
- Prototype functionality includes:
  - `POST /simulate-update` – simulates a database-triggered change by enqueuing a work item.
  - `GET /tasks` – inspects the most recent processed items.
  - `GET /health` – ASP.NET Core health check endpoint for readiness/keep-alive pings.

## Getting Started

1. Install the .NET 8.0 SDK.
2. Restore dependencies and run the web app locally:
   ```bash
   dotnet restore TaskQueueApp.sln
   dotnet run --project src/TaskQueueApp/TaskQueueApp.csproj
   ```
3. Exercise the API with `curl`, Postman, or Swagger UI (auto-enabled in Development):
   - `POST /simulate-update` sample body:
     ```json
     {
       "tableName": "Orders",
       "payload": "OrderId=1234"
     }
     ```
   - `GET /tasks`
   - `GET /health`

### Hosting Notes
- **IIS**: Publish with `dotnet publish`, configure Always On, disable idle timeout, and register `/health` as an initialization endpoint.
- **Azure App Service**: Enable Always On, configure warm-up pings to `/health`, and consider deployment slots with auto-swap.
- **Windows/systemd service**: The app calls `UseWindowsService()`/`UseSystemd()` so it can be installed as a background worker without code changes.
