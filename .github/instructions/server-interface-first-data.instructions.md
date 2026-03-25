---
description: "Use when modifying server services or repositories in V2XDashboard.Server, emphasizing interface-first design, Dapper best practices, and clean async boundaries."
name: "Server Interface First and Data Access Guidelines"
applyTo:
  "src/V2XDashboard.Server/Services/**/*.cs" +
  "src/V2XDashboard.Server/Infrastructure/**/*.cs"
---
# Server Interface First and Data Access Guidelines

- Follow interface-first design: define or update interfaces before concrete implementations.
- Keep classes single-responsibility and methods short with explicit names.
- Use async/await for I/O; do not block on tasks.
- Register new services and repositories in src/V2XDashboard.Server/Extensions/ServiceCollectionExtensions.cs.
- Keep orchestration in services, transport concerns in endpoints, and SQL concerns in repositories.
- Use parameterized SQL with Dapper only; never concatenate user input into SQL.
- Preserve batching/chunking for large writes to avoid oversized commands.
- For PostgreSQL inserts returning identifiers, place `RETURNING ...` after `VALUES (...)`.
- Prefer shared domain/message models from V2XDashboard.Shared where contracts overlap.
- Keep logging and exception messages actionable without leaking sensitive values.
- Avoid breaking existing public contracts unless explicitly requested.
