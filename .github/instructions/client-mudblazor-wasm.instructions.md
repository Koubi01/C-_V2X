---
description: "Use when creating or updating Blazor WebAssembly UI in V2XDashboard.Client with MudBlazor components, async data loading, and client service integration patterns."
name: "Client MudBlazor WASM Guidelines"
applyTo:
  "src/V2XDashboard.Client/**/*.razor"+
  "src/V2XDashboard.Client/**/*.cs"
---
# Client MudBlazor WASM Guidelines

- Prefer MudBlazor components first; reuse existing layout and theme before introducing custom CSS.
- Keep rendering/state in Razor components and move API/data logic to injectable client services.
- Use async lifecycle methods (`OnInitializedAsync`, `OnParametersSetAsync`) for data loading.
- Keep UI responsive: avoid blocking calls and avoid sync-over-async patterns.
- Use host-relative API calls via the configured HttpClient base address unless cross-origin is explicitly required.
- Add cancellation support for long-running requests where practical.
- Keep components small and composable; extract repeated UI blocks into reusable components.
- Preserve navigation and layout conventions in Layout and Pages unless redesign is requested.
- Prefer clear loading, empty, and error states using MudBlazor feedback components.
- Keep page-level code-behind focused; move transformation logic to dedicated services/helpers.
