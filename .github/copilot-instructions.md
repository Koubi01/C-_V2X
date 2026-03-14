# V2X PCAP Dashboard - Copilot Instructions

## Project Overview

**V2X PCAP Dashboard** is a full-stack .NET 10 application for analyzing Vehicle-to-Everything (V2X) communication captured in PCAP files. It processes network packets, detects V2X message types (CAM, DENM, MAPEM, SPATEM, SREM, SSEM), and provides a web-based dashboard for visualization.

**Tech Stack**: ASP.NET Core 10 + Blazor WebAssembly + PostgreSQL + Docker + tshark

---

## Quick Start

### Build & Run
```bash
# Build entire solution
dotnet build src/V2XDashboard.slnx

# Run backend server (from src/V2XDashboard.Server)
dotnet run

# Start Docker services (PostgreSQL)
docker-compose up -d

# Swagger API docs available at: http://localhost:5000/swagger
```

### Database Setup
```bash
# PostgreSQL is started via docker-compose.yml
# init.sql runs automatically and creates all tables
docker-compose up -d postgres
```

### Project Structure
```
src/
├── V2XDashboard.Server/      # ASP.NET Core backend + API
│   ├── Controllers/          # REST endpoints (PcapController)
│   ├── Services/             # Business logic (PcapService)
│   │   └── PcapReader/       # PCAP parsing service
│   │       └── TsharkWrapper/# tshark JSON parser
│   ├── Program.cs            # Dependency injection setup
│   └── appsettings*.json     # Config
├── V2XDashboard.Client/      # Blazor WebAssembly frontend
│   ├── Pages/                # Razor components
│   ├── Layout/               # Main layout & navigation
│   └── wwwroot/              # Static assets
└── V2XDashboard.Shared/      # Shared models
    ├── Packet.cs             # Parsed packet model
    └── V2XMessage.cs         # V2X message base

PcapData/                       # PCAP files to process
docker/                         # Docker build files
docker-compose.yml
```

---

## Architecture Patterns

### Service Layer
- **IPcapService**: Main interface for PCAP operations
- **PcapService**: Implements packet extraction, message type detection, and database storage
- **TsharkParser**: Wraps tshark CLI, executes with JSON output, parses JSON to `Packet` objects

### Data Flow
1. PCAP file → tshark CLI (JSON output) → TsharkParser (parse JSON) → PcapService (detect message type, store in DB)
2. Database query → API endpoint → REST response

### Message Type Detection
Done in `TsharkParser.DeterminePacketType()`:
- **Port-based** (primary): UDP ports 4729 (CAM), 2001 (DENM), 4731 (MAPEM), 4732 (SPATEM), 4733 (SREM), 4734 (SSEM)
- **MAC-based** (fallback): IEEE 802.11p OUI detection (00:50:C2)

### Code Style
- **Nullable reference types enabled** in all projects
- **Implicit using statements** enabled
- **Async/await** pattern throughout
- **Dependency injection** via ServiceCollection
- **File-scoped namespaces** (`namespace X.Y.Z;`)

---

## Key Files & Responsibilities

| File | Purpose |
|------|---------|
| `PcapController.cs` | REST API endpoints (`/api/pcap/*`) |
| `PcapService.cs` | Coordinates PCAP processing pipeline |
| `TsharkWrapper.cs` | tshark CLI execution + JSON parsing |
| `Packet.cs` (Shared) | Parsed packet model from tshark |
| `V2XMessage.cs` (Shared) | Base model for V2X messages |
| `Program.cs` | Service registration, middleware setup |
| `docker-compose.yml` | PostgreSQL + web service definitions |
| `init.sql` | Database schema (V2X message tables) |

---

## API Endpoints

All endpoints under `/api/pcap/`:

```
GET /files                  - List available PCAP files
POST /process/{fileName}    - Process PCAP file by name
GET /{messageType}?limit=100 - Query messages by type (CAM, DENM, MAPEM, SPATEM, SREM, SSEM)
```

### Example
```bash
curl http://localhost:5000/api/pcap/files
curl -X POST http://localhost:5000/api/pcap/process/data1.pcap
curl http://localhost:5000/api/pcap/cam?limit=50
```

---

## Development Conventions

### Naming
- **Services**: `XyzService` implementing `IXyzService`
- **Controllers**: `XyzController` with `[ApiController]` + `[Route("api/[controller]")]`
- **Models**: Clear, domain-driven names (Packet, V2XMessage, etc.)

### Error Handling
- Wrap exceptions with context: `throw new Exception($"Operation failed: {ex.Message}", ex);`
- Catch-all blocks in async methods (tshark execution, DB queries)
- Return 500 for unrecoverable errors, 400 for client errors

### Dependencies
- **tshark**: External CLI tool (must be installed on system or container)
- **PostgreSQL**: Via Docker (npgsql driver)
- **Dapper**: Lightweight ORM for queries
- **MudBlazor**: UI component library (Blazor)
- **Swashbuckle**: Swagger/OpenAPI generation

### Testing
- PCAP files in `PcapData/` directory for manual testing
- Swagger UI at `/swagger` for API exploration
- Docker logs: `docker-compose logs -f`

---

## Common Tasks

### Add a New REST Endpoint
1. Add method to `PcapController`
2. Inject `IPcapService` in constructor (already done)
3. Call service method and return `Ok()`, `BadRequest()`, or `StatusCode()`
4. Endpoint auto-documented in Swagger

### Parse New PCAP File Type
1. Update `TsharkParser.ExtractPacketsAsync()` arguments to include new fields
2. Update `ParseTsharkJson()` to extract new properties
3. Update `DeterminePacketType()` for new detection logic
4. Add corresponding database storage in `PcapService`

### Add Database Table
1. Update `init.sql` in `docker/postgres/`
2. Rebuild: `docker-compose up -d postgres`
3. Add query methods to `PcapService`

---

## Known Issues & Workarounds

- **tshark not found**: Install Wireshark/tshark on your system, or ensure container has it
- **Database connection failed**: Ensure `docker-compose up -d postgres` runs first; check appsettings.json for correct connection string
- **Port conflicts**: Ensure ports 5000 (API) and 5432 (PostgreSQL) are free

---

## Performance Tips

- tshark JSON parsing can be slow on large PCAP files; consider filtering at tshark level with `-Y "udp.dstport == 4729"` for specific message types
- Add database indexes on frequently queried fields (already done in init.sql)
- Use pagination (`limit` parameter) in API queries for large datasets

---

## Next Steps for Development

1. **Frontend Components**: Build Blazor pages for message display/filtering
2. **Real Protocol Parsing**: Implement actual V2X message decoding (currently using placeholders)
3. **Advanced Filtering**: Time range, message type, source/destination filters
4. **Data Visualization**: Maps, charts, message timelines
5. **Performance**: Batch processing for large PCAP files

---

## Reference Links

- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core)
- [Blazor Docs](https://docs.microsoft.com/aspnet/core/blazor)
- [tshark Documentation](https://www.wireshark.org/docs/man-pages/tshark.html)
- [ETSI ITS Standards](https://www.etsi.org/committee/ITS) (V2X message specs)
