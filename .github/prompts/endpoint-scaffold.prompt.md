---
description: "Scaffold a new V2XDashboard server feature end-to-end: minimal API endpoint group, interface-first service/repository updates, DI wiring, and request/response contracts."
name: "Endpoint Scaffold"
argument-hint: "Describe the feature, route shape, request/response model, and persistence behavior"
agent: "agent"
---
Create an implementation plan and then implement a new server feature in this workspace with these constraints:

- Use minimal API endpoint-group extensions in src/V2XDashboard.Server/Api/Endpoints.
- Keep endpoint handlers thin; put orchestration and business logic in services behind interfaces.
- Use interface-first design for new capabilities in Services and Infrastructure layers.
- Register dependencies in src/V2XDashboard.Server/Extensions/ServiceCollectionExtensions.cs.
- Use parameterized Dapper SQL only; preserve batching/chunking patterns where relevant.
- If PostgreSQL insert IDs are returned, place RETURNING after VALUES.
- Keep contracts stable unless the task explicitly calls for breaking changes.
- Reuse V2XDashboard.Shared models where appropriate.

Execution checklist:
1. Discover existing endpoint and service patterns in the closest related feature.
2. Add or update interfaces first.
3. Implement service/repository changes.
4. Add or update endpoint mapping extensions.
5. Wire DI registrations.
6. Build the solution and report errors/fixes.
7. Summarize changed files and contract impact.

When information is missing, ask focused questions before implementing.
