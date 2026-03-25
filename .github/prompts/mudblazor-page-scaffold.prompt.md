---
description: "Scaffold a Blazor WASM page using MudBlazor patterns in V2XDashboard.Client with async data loading, service-driven data access, and clear loading/empty/error states."
name: "MudBlazor Page Scaffold"
argument-hint: "Describe page goal, route, data source, table/card layout, and filters"
agent: "agent"
---
Create and implement a new Blazor WebAssembly page in this workspace using existing MudBlazor patterns.

Requirements:
- Place page/component files under src/V2XDashboard.Client following existing structure.
- Reuse existing layout and theme conventions before introducing custom styles.
- Keep UI concerns in Razor component(s) and move data access/transform logic to injectable services.
- Use async lifecycle methods for loading data and avoid blocking calls.
- Implement explicit loading, empty, and error states.
- Use host-relative HTTP API calls through configured client services.
- Add cancellation support for long-running data fetches when practical.
- Keep components small and extract reusable UI blocks when repeated.

Execution checklist:
1. Inspect closest existing page and service patterns.
2. Scaffold page route and MudBlazor structure.
3. Add/update client service methods for required API calls.
4. Implement loading, empty, error, and success states.
5. Validate compile/build for changed projects.
6. Summarize changed files and interaction flow.

If details are missing, ask focused questions before editing.
