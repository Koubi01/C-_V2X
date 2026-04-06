using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using V2XDashboard.Server;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public class Phase0SmokeTests : IClassFixture<Phase0SmokeTests.TestAppFactory>
{
    private readonly HttpClient _client;

    public Phase0SmokeTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessOneFile_ReturnsOk()
    {
        var response = await _client.PostAsync("/api/ingestion/process/data1.pcap", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QueryCam_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/messages/cam?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<List<CAM>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);
    }

    [Fact]
    public async Task QueryMapEntitiesPaged_ReturnsOkWithPagedResult()
    {
        var response = await _client.GetAsync("/api/map/entities/paged?pageNumber=1&pageSize=25");
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<MapEntityDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount > 0);
        Assert.NotEmpty(payload.Items);
    }

    [Fact]
    public async Task QueryCorrelationsPaged_ReturnsOkWithPagedResult()
    {
        var response = await _client.GetAsync("/api/correlations/paged?pageNumber=1&pageSize=10");
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<CorrelationDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount > 0);
        Assert.NotEmpty(payload.Items);
    }

    [Fact]
    public async Task QueryStationProfilesPaged_ReturnsOkWithPagedResult()
    {
        var response = await _client.GetAsync("/api/stations/profiles?pageNumber=1&pageSize=25");
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<StationProfileDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount > 0);
        Assert.NotEmpty(payload.Items);
    }

    [Fact]
    public async Task TileEndpoint_WithValidRequest_ReturnsProtobufResponse()
    {
        var response = await _client.GetAsync("/api/tiles/CAM/0/0/0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TileEndpoint_WithInvalidLayer_ReturnsBadRequestValidationProblem()
    {
        var response = await _client.GetAsync("/api/tiles/INVALID/0/0/0");
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Errors.ContainsKey("layer"));
    }

    [Fact]
    public async Task TileEndpoint_WithInvalidCoordinates_ReturnsBadRequestValidationProblem()
    {
        var response = await _client.GetAsync("/api/tiles/CAM/1/5/0");
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Errors.ContainsKey("x"));
    }

    [Fact]
    public async Task TileEndpoint_WithInvalidDateRange_ReturnsBadRequestValidationProblem()
    {
        var response = await _client.GetAsync("/api/tiles/CAM/0/0/0?fromTime=2026-01-02T00:00:00Z&toTime=2026-01-01T00:00:00Z");
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Errors.ContainsKey("fromTime"));
    }

    [Fact]
    public async Task MessagesPaged_WithInvalidPageNumber_ReturnsBadRequestValidationProblem()
    {
        var response = await _client.GetAsync("/api/messages/paged?messageType=CAM&pageNumber=0&pageSize=25");
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Errors.ContainsKey("pageNumber"));
    }

    [Fact]
    public async Task MapEntitiesPaged_WithInvalidDateRange_ReturnsBadRequestValidationProblem()
    {
        var response = await _client.GetAsync(
            "/api/map/entities/paged?fromTime=2026-01-02T00:00:00Z&toTime=2026-01-01T00:00:00Z&pageNumber=1&pageSize=10");
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Errors.ContainsKey("fromTime"));
    }

    [Fact]
    public async Task MessagesPaged_PerformanceSmoke_CompletesUnderOneSecond()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/messages/paged?messageType=CAM&pageNumber=1&pageSize=25");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Expected endpoint under 1000ms but was {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task IngestionSchedulerStatus_ReturnsOkWithPayload()
    {
        var response = await _client.GetAsync("/api/ingestion/scheduler/status");
        var payload = await response.Content.ReadFromJsonAsync<IngestionSchedulerStatusDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.IntervalSeconds > 0);
    }

    [Fact]
    public async Task IngestionSchedulerStartStop_ReturnsOk()
    {
        var stopResponse = await _client.PostAsync("/api/ingestion/scheduler/stop", content: null);
        var stopPayload = await stopResponse.Content.ReadFromJsonAsync<IngestionSchedulerStatusDto>();

        Assert.Equal(HttpStatusCode.OK, stopResponse.StatusCode);
        Assert.NotNull(stopPayload);
        Assert.False(stopPayload!.IsRunning);

        var startResponse = await _client.PostAsync("/api/ingestion/scheduler/start", content: null);
        var startPayload = await startResponse.Content.ReadFromJsonAsync<IngestionSchedulerStatusDto>();

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.NotNull(startPayload);
        Assert.True(startPayload!.IsRunning);
    }

    [Fact]
    public async Task PacketSecuritySummary_ReturnsOkWithPayload()
    {
        var response = await _client.GetAsync("/api/packets/security/summary");
        var payload = await response.Content.ReadFromJsonAsync<SecurityMetadataSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalPackets >= payload.SecurePackets);
    }

    public sealed class TestAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IPcapIngestionService, FakePcapIngestionService>();
                services.AddSingleton<IV2XMessageQueryService, FakeMessageQueryService>();
                services.AddSingleton<IPacketQueryService, FakePacketQueryService>();
                services.AddSingleton<IMapEntityService, FakeMapEntityService>();
                services.AddSingleton<ICorrelationService, FakeCorrelationService>();
                services.AddSingleton<IStationProfileService, FakeStationProfileService>();
                services.AddSingleton<IIngestionScheduler, FakeIngestionScheduler>();
                services.AddSingleton<IMapTileService, FakeMapTileService>();
            });
        }
    }

    private sealed class FakePacketQueryService : IPacketQueryService
    {
        public Task<List<Packet>> GetPacketsAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int? limit = null)
            => Task.FromResult(new List<Packet>());

        public Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int pageNumber = 1, int pageSize = 25)
            => Task.FromResult(new PagedResult<Packet>
            {
                Items = new List<Packet>(),
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<Packet?> GetPacketByIdAsync(int id)
            => Task.FromResult<Packet?>(new Packet { Id = id, PacketType = "CAM" });

        public Task<SecurityMetadataSummaryDto> GetSecurityMetadataSummaryAsync()
            => Task.FromResult(new SecurityMetadataSummaryDto
            {
                TotalPackets = 4,
                SignedPackets = 3,
                EncryptedPackets = 1,
                SecurePackets = 3,
                DistinctSigners = 2,
                DistinctCertificates = 2
            });
    }

    private sealed class FakePcapIngestionService : IPcapIngestionService
    {
        public Task<List<string>> GetPcapFilesAsync() => Task.FromResult(new List<string> { "data1.pcap" });
        public Task<bool> ProcessPcapFileAsync(string fileName) => Task.FromResult(true);
        public Task<bool> ProcessAllPcapFilesAsync() => Task.FromResult(true);
    }

    private sealed class FakeMessageQueryService : IV2XMessageQueryService
    {
        public Task<List<V2XMessage>> GetV2XMessagesAsync(string? messageType = null, int? limit = null)
            => Task.FromResult<List<V2XMessage>>(new List<V2XMessage> { BuildCam() });

        public Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25)
            => Task.FromResult(new PagedResult<MessageListItemDto>
            {
                Items = new List<MessageListItemDto>
                {
                    new() { MessageType = messageType, StationLabel = "station-a", Detail = "detail", GenerationTime = DateTime.UtcNow }
                },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<MessageCountsDto> GetMessageCountsAsync()
            => Task.FromResult(new MessageCountsDto { TotalPackets = 1, CAM = 1, TotalCorrelations = 1 });

        public Task<List<CAM>> GetCAMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<CAM> { BuildCam() });

        public Task<List<DENM>> GetDENMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<DENM>());

        public Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<MAPEM>());

        public Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<SPATEM>());

        public Task<List<SREM>> GetSREMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<SREM>());

        public Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<SSEM>());

        public Task<V2XMessage?> GetV2XMessageByIdAsync(int id, string? messageType = null)
            => Task.FromResult<V2XMessage?>(BuildCam());

        private static CAM BuildCam()
        {
            return new CAM
            {
                Id = 1,
                PacketId = 1,
                StationId = "station-a",
                GenerationTime = DateTime.UtcNow,
                Latitude = 49.82,
                Longitude = 18.26,
                Speed = 10
            };
        }
    }

    private sealed class FakeMapEntityService : IMapEntityService
    {
        public Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null)
            => Task.FromResult(new List<MapEntityDto> { BuildEntity() });

        public Task<PagedResult<MapEntityDto>> GetMapEntitiesPagedAsync(
            DateTime? fromTime = null,
            DateTime? toTime = null,
            IEnumerable<string>? entityTypes = null,
            IEnumerable<string>? messageTypes = null,
            IEnumerable<string>? vehicleCategories = null,
            IEnumerable<int>? stationTypes = null,
            bool? isSecureSigned = null,
            bool? isSecureEncrypted = null,
            int pageNumber = 1,
            int pageSize = 100)
            => Task.FromResult(new PagedResult<MapEntityDto>
            {
                Items = new List<MapEntityDto> { BuildEntity() },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        private static MapEntityDto BuildEntity()
        {
            return new MapEntityDto
            {
                EntityType = "OBU",
                MessageType = "CAM",
                StationId = "station-a",
                Latitude = 49.82,
                Longitude = 18.26,
                GenerationTime = DateTime.UtcNow
            };
        }
    }

    private sealed class FakeCorrelationService : ICorrelationService
    {
        public Task<List<CorrelationDto>> GetCorrelationsAsync(
            string? obuStationId = null,
            string? rsuIntersectionId = null,
            string? requestId = null,
            string? correlationType = null,
            DateTime? fromTime = null,
            DateTime? toTime = null,
            bool? isSecureSigned = null,
            bool? isSecureEncrypted = null,
            int? limit = null)
            => Task.FromResult(new List<CorrelationDto> { BuildCorrelation() });

        public Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(
            string? obuStationId = null,
            string? rsuIntersectionId = null,
            string? requestId = null,
            string? correlationType = null,
            DateTime? fromTime = null,
            DateTime? toTime = null,
            bool? isSecureSigned = null,
            bool? isSecureEncrypted = null,
            int pageNumber = 1,
            int pageSize = 10)
            => Task.FromResult(new PagedResult<CorrelationDto>
            {
                Items = new List<CorrelationDto> { BuildCorrelation() },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId)
            => Task.FromResult<SremSsemMatchDto?>(new SremSsemMatchDto
            {
                Id = 1,
                SsemId = 1,
                CorrelationType = "strict",
                MatchConfidence = 1.0
            });

        public Task RecordOBUToRSUCorrelationAsync() => Task.CompletedTask;

        private static CorrelationDto BuildCorrelation()
        {
            return new CorrelationDto
            {
                Id = 1,
                CorrelationType = "strict",
                MatchConfidence = 1.0,
                SremTimestamp = DateTime.UtcNow,
                SsemTimestamp = DateTime.UtcNow,
                TimeDeltaMs = 25
            };
        }
    }

    private sealed class FakeIngestionScheduler : IIngestionScheduler
    {
        private bool _isRunning = true;

        public Task<IngestionSchedulerStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BuildStatus());
        }

        public Task<IngestionSchedulerStatusDto> StartSchedulingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isRunning = true;
            return Task.FromResult(BuildStatus());
        }

        public Task<IngestionSchedulerStatusDto> StopSchedulingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isRunning = false;
            return Task.FromResult(BuildStatus());
        }

        private IngestionSchedulerStatusDto BuildStatus()
        {
            return new IngestionSchedulerStatusDto
            {
                IsSchedulerNode = true,
                IsRunning = _isRunning,
                IntervalSeconds = 30,
                TotalRuns = 1,
                LastRunStartedAtUtc = DateTime.UtcNow.AddSeconds(-1),
                LastRunFinishedAtUtc = DateTime.UtcNow,
                LastRunSucceeded = true,
                LastRunDurationMs = 100
            };
        }
    }

    private sealed class FakeStationProfileService : IStationProfileService
    {
        public Task RefreshStationProfilesAsync() => Task.CompletedTask;

        public Task<PagedResult<StationProfileDto>> GetStationProfilesPagedAsync(
            string? stationId = null,
            string? entityType = null,
            string? messageType = null,
            string? vehicleCategory = null,
            int? stationType = null,
            bool? supportsSecureComm = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            var profile = BuildProfile();
            return Task.FromResult(new PagedResult<StationProfileDto>
            {
                Items = new List<StationProfileDto> { profile },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public Task<StationProfileDto?> GetStationProfileByStationIdAsync(string stationId)
            => Task.FromResult<StationProfileDto?>(BuildProfile());

        public Task<StationCapabilitiesSummaryDto> GetStationCapabilitiesAsync()
            => Task.FromResult(new StationCapabilitiesSummaryDto
            {
                TotalStations = 1,
                ObuStations = 1,
                SecureStations = 1,
                SignedStations = 1,
                MessageTypeCoverage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CAM"] = 1
                }
            });

        private static StationProfileDto BuildProfile()
        {
            return new StationProfileDto
            {
                StationId = "station-a",
                EntityType = "OBU",
                StationType = 5,
                VehicleCategory = "Passenger Car",
                SupportsSecureComm = true,
                SupportsSigned = true,
                SupportsEncrypted = false,
                FirstSeenAt = DateTime.UtcNow.AddMinutes(-5),
                LastSeenAt = DateTime.UtcNow,
                ObservedMessageTypes = new List<string> { "CAM" }
            };
        }
    }

    private sealed class FakeMapTileService : IMapTileService
    {
        private static readonly IReadOnlyDictionary<string, int> DefaultGenerations =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [TileLayerNames.Cam] = 0,
                [TileLayerNames.Denm] = 0,
                [TileLayerNames.Mapem] = 0,
                [TileLayerNames.Spatem] = 0,
                [TileLayerNames.Srem] = 0,
                [TileLayerNames.Ssem] = 0,
                [TileLayerNames.Correlations] = 0
            };

        public Task<byte[]> GetTileAsync(string layer, int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new byte[] { 0x1A, 0x00 });

        public TileCacheDiagnosticsDto GetCacheDiagnostics()
            => new()
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                LayerGenerations = DefaultGenerations
            };

        public void InvalidateAllTiles()
        {
        }

        public void InvalidateLayer(string layer)
        {
        }
    }
}
