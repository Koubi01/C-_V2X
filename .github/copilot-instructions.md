# V2X PCAP Dashboard - Workspace Instructions

## Project Scope

This repository is a full-stack .NET 10 system for V2X PCAP ingestion and analysis:
- ASP.NET Core server with minimal API endpoints
- Blazor WebAssembly client using MudBlazor
- PostgreSQL persistence via Dapper
- tshark-based packet extraction

## Code Style

- Prioritize clean code: small methods, explicit names, and single-responsibility classes.
- Keep nullable reference types and file-scoped namespaces consistent with the existing projects.
- Prefer async/await for I/O paths; avoid sync-over-async.
- Keep public APIs and contracts stable unless the task explicitly requires breaking changes.

## Architecture

- Use interface-first design for services and repositories. Add or update interfaces before concrete implementations.
- Server API is minimal API based. Add endpoints in `src/V2XDashboard.Server/Api/Endpoints/` using endpoint-mapping extensions.
- Register dependencies through `src/V2XDashboard.Server/Extensions/ServiceCollectionExtensions.cs`.
- Keep boundaries clear:
    - `V2XDashboard.Server`: API, orchestration, infrastructure
    - `V2XDashboard.Shared`: shared DTO/domain message models
    - `V2XDashboard.Client`: Blazor WASM UI and API consumption

## Minimal API Conventions

- Prefer endpoint-group extension methods (`MapXxxEndpoints`) over controller-style additions.
- Validate inputs early and return typed, meaningful HTTP results.
- Keep endpoint handlers thin; place business logic in services behind interfaces.
- Preserve pagination and filtering patterns on query endpoints.

## MudBlazor + WASM Conventions

- Use MudBlazor components as the default UI building blocks; reuse existing theme and layout patterns before adding new styles.
- Keep rendering logic in Razor components and move data operations to injectable services.
- For WASM, use async data loading, cancellation-aware calls where relevant, and avoid blocking UI interactions.
- Keep API calls relative to the current host base address unless a task explicitly introduces cross-origin behavior.

## Data Access Conventions

- Use parameterized SQL via Dapper only; never concatenate user input into SQL.
- Keep batched operations and chunking patterns for large ingestion writes.
- For PostgreSQL inserts with returned IDs, place `RETURNING ...` after `VALUES (...)`.

## Build and Run

- Build solution: `dotnet build src/V2XDashboard.slnx`
- Run server (from `src/V2XDashboard.Server`): `dotnet run`
- Start dependencies: `docker-compose up -d`
- Swagger: `/swagger` in development

## Pitfalls

- `tshark` must be available (local install or container image).
- Connection string host differs by environment (`localhost` in local development, Docker service name in containers).
- Keep PCAP path configuration aligned with environment (`PcapDataPath` in appsettings).

## References

- Product and setup context: `README.md`
- Architecture decisions and roadmap: `plan.md`
- Database schema: `docker/postgres/init.sql`
- Validation scripts: `scripts/README.md`
