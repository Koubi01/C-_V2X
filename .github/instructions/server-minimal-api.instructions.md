---
description: "Use when adding or editing ASP.NET Core minimal API endpoints in V2XDashboard.Server, including endpoint mapping extensions, request validation, typed results, pagination, and service orchestration."
name: "Server Minimal API Guidelines"
applyTo: "src/V2XDashboard.Server/Api/Endpoints/**/*.cs"
---
# Server Minimal API Guidelines

- Keep endpoint files focused on routing and HTTP translation only.
- Put business logic in services behind interfaces; endpoints should orchestrate, not implement domain logic.
- Validate inputs early and return clear typed results (`BadRequest`, `NotFound`, `Ok`, `Problem`, `Results<T1, T2, ...>`).
- Preserve existing pagination and filtering contracts when extending query endpoints.
- Add new endpoint groups as `MapXxxEndpoints` extension methods.
- Register dependencies in src/V2XDashboard.Server/Extensions/ServiceCollectionExtensions.cs before wiring new endpoint usage.
- Keep handlers async for I/O work and pass cancellation tokens when available.
- Prefer small private helper methods over long handlers when mapping query parameters.
- Keep responses stable unless a task explicitly requires contract changes.
- Reuse shared DTO/message models from V2XDashboard.Shared when possible.
- For persistence calls, use parameterized queries through repository interfaces and avoid SQL in endpoints.
- When changing API behavior, ensure development Swagger output remains valid and discoverable.
