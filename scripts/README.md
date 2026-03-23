# Stage 4 Validation Scripts

## stage4-validation.ps1

Runs API integration checks and latency benchmarking for paged map/correlation endpoints.

### Prerequisites
- PostgreSQL Docker container running (`v2x_postgres`)
- API server running with latest code (recommended local run)

### Run

```powershell
pwsh ./scripts/stage4-validation.ps1 -BaseUrl "http://localhost:5007" -Iterations 15 -PageSize 100
```

### What it checks
- `map-entities/paged` returns data
- `correlations/paged` returns data
- station type filtered CAM count is not greater than total CAM count
- strict correlation count is not greater than total correlation count

### What it benchmarks
- `map-entities/paged` with CAM filter
- `map-entities/paged` with CAM + station type filter
- `correlations/paged` all
- `correlations/paged` strict
