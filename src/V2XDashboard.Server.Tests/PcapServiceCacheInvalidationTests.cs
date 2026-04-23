using System.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Server.Services.PcapReader;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Services.PcapReader.TsharkWrapper;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public sealed class PcapServiceCacheInvalidationTests
{
    [Fact]
    public async Task RecordOBUToRSUCorrelationAsync_WhenRowsInserted_InvalidatesCorrelationLayer()
    {
        var mapTileService = new CountingMapTileService();
        var service = BuildService(
            new StubCorrelationRepository(new CorrelationRecordSummary
            {
                TotalSrems = 10,
                SelectedCandidates = 3,
                InsertedTotal = 2,
                InsertedStrict = 1,
                InsertedFallback = 1
            }),
            mapTileService);

        await service.RecordOBUToRSUCorrelationAsync();

        Assert.Equal(1, mapTileService.CorrelationInvalidationCalls);
    }

    [Fact]
    public async Task RecordOBUToRSUCorrelationAsync_WhenNoRowsInserted_DoesNotInvalidateCorrelationLayer()
    {
        var mapTileService = new CountingMapTileService();
        var service = BuildService(
            new StubCorrelationRepository(new CorrelationRecordSummary
            {
                TotalSrems = 5,
                SelectedCandidates = 0,
                InsertedTotal = 0,
                InsertedStrict = 0,
                InsertedFallback = 0
            }),
            mapTileService);

        await service.RecordOBUToRSUCorrelationAsync();

        Assert.Equal(0, mapTileService.CorrelationInvalidationCalls);
    }

    private static PcapService BuildService(ICorrelationRepository correlationRepository, CountingMapTileService mapTileService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=v2x_database;Username=v2x_admin;Password=supertajneheslo",
                ["PcapDataPath"] = Path.GetTempPath()
            })
            .Build();

        return new PcapService(
            configuration,
            new MemoryCache(new MemoryCacheOptions()),
            new StubCamDecoder(),
            new StubDenmDecoder(),
            new StubMapemDecoder(),
            new StubSpatemDecoder(),
            new StubSremDecoder(),
            new StubSsemDecoder(),
            new StubTsharkParser(),
            new StubPacketRepository(),
            new StubV2XMessageRepository(),
            correlationRepository,
            mapTileService,
            new StubMapEntityRepository(),
            new StubStationProfileRepository(),
            new StubProcessedFileRepository(),
            NullLogger<PcapService>.Instance);
    }

    private sealed class CountingMapTileService : IMapTileService
    {
        public int CorrelationInvalidationCalls { get; private set; }

        public Task<byte[]> GetTileAsync(string layer, int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public TileCacheDiagnosticsDto GetCacheDiagnostics()
            => new()
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                LayerGenerations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [TileLayerNames.Correlations] = CorrelationInvalidationCalls
                }
            };

        public void InvalidateAllTiles()
        {
        }

        public void InvalidateLayer(string layer)
        {
            if (string.Equals(layer, TileLayerNames.Correlations, StringComparison.OrdinalIgnoreCase))
            {
                CorrelationInvalidationCalls++;
            }
        }
    }

    private sealed class StubCorrelationRepository : ICorrelationRepository
    {
        private readonly CorrelationRecordSummary _summary;

        public StubCorrelationRepository(CorrelationRecordSummary summary)
        {
            _summary = summary;
        }

        public Task PopulateIntersectionMetadataForFileAsync(string fileName) => Task.CompletedTask;

        public Task PopulateIntersectionMetadataForAllFilesAsync() => Task.CompletedTask;

        public Task<CorrelationRecordSummary> RecordOBUToRSUCorrelationAsync(int timeWindowSeconds)
            => Task.FromResult(_summary);

        public Task<List<CorrelationDto>> GetCorrelationsAsync(string? obuStationId = null, string? rsuIntersectionId = null, string? requestId = null, string? correlationType = null, DateTime? fromTime = null, DateTime? toTime = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, int? limit = null)
            => throw new NotSupportedException();

        public Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(string? obuStationId = null, string? rsuIntersectionId = null, string? requestId = null, string? correlationType = null, DateTime? fromTime = null, DateTime? toTime = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, int pageNumber = 1, int pageSize = 10)
            => throw new NotSupportedException();

        public Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId)
            => throw new NotSupportedException();
    }

    private sealed class StubCamDecoder : ICamDecoder
    {
        public CAM DecodeCAM(Packet packet) => new() { PacketId = packet.Id };
    }

    private sealed class StubDenmDecoder : IDenmDecoder
    {
        public DENM DecodeDENM(Packet packet) => new() { PacketId = packet.Id };
    }

    private sealed class StubMapemDecoder : IMapemDecoder
    {
        public MAPEM DecodeMAPEM(Packet packet) => new() { PacketId = packet.Id };
    }

    private sealed class StubSpatemDecoder : ISpatemDecoder
    {
        public SPATEM DecodeSPATEM(Packet packet) => new() { PacketId = packet.Id };
    }

    private sealed class StubSremDecoder : ISremDecoder
    {
        public SREM DecodeSREM(Packet packet) => new() { PacketId = packet.Id };
    }

    private sealed class StubSsemDecoder : ISsemDecoder
    {
        public SSEM DecodeSSEM(Packet packet) => new() { PacketId = packet.Id };
    }

    private sealed class StubTsharkParser : ITsharkParser
    {
        public Task<List<Packet>> ExtractPacketsAsync(string pcapFilePath, string filter = "")
            => Task.FromResult(new List<Packet>());
    }

    private sealed class StubPacketRepository : IPacketRepository
    {
        public Task InsertPacketsAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<Packet> packets)
            => Task.CompletedTask;

        public Task<List<Packet>> GetPacketsAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int? limit = null)
            => throw new NotSupportedException();

        public Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int pageNumber = 1, int pageSize = 25)
            => throw new NotSupportedException();

        public Task<Packet?> GetPacketByIdAsync(int id)
            => throw new NotSupportedException();

        public Task<SecurityMetadataSummaryDto> GetSecurityMetadataSummaryAsync()
            => throw new NotSupportedException();
    }

    private sealed class StubV2XMessageRepository : IV2XMessageRepository
    {
        public Task InsertCAMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<CAM> messages) => Task.CompletedTask;
        public Task InsertDENMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<DENM> messages) => Task.CompletedTask;
        public Task InsertMAPEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<MAPEM> messages) => Task.CompletedTask;
        public Task InsertSPATEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SPATEM> messages) => Task.CompletedTask;
        public Task InsertSREMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SREM> messages) => Task.CompletedTask;
        public Task InsertSSEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SSEM> messages) => Task.CompletedTask;
        public Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25) => throw new NotSupportedException();
        public Task<MessageCountsDto> GetMessageCountsAsync() => throw new NotSupportedException();
        public Task<DistinctVehicleWindowStatsDto> GetDistinctVehicleWindowStatsAsync() => Task.FromResult(new DistinctVehicleWindowStatsDto());
        public Task<MapVehicleFilterSummaryDto> GetMapVehicleFilterSummaryAsync(MapVehicleSummaryQueryParams filters) => throw new NotSupportedException();
        public Task<List<CAM>> GetCAMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<DENM>> GetDENMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<SREM>> GetSREMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<V2XMessage?> GetV2XMessageByIdAsync(int id, string? messageType = null) => throw new NotSupportedException();
    }

    private sealed class StubMapEntityRepository : IMapEntityRepository
    {
        public Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null)
            => throw new NotSupportedException();
    }

    private sealed class StubStationProfileRepository : IStationProfileRepository
    {
        public Task RebuildStationProfilesAsync() => Task.CompletedTask;

        public Task<PagedResult<StationProfileDto>> GetStationProfilesPagedAsync(
            string? stationId = null,
            string? entityType = null,
            string? messageType = null,
            string? vehicleCategory = null,
            int? stationType = null,
            bool? supportsSecureComm = null,
            int pageNumber = 1,
            int pageSize = 50)
            => throw new NotSupportedException();

        public Task<StationProfileDto?> GetStationProfileByStationIdAsync(string stationId)
            => throw new NotSupportedException();

        public Task<StationCapabilitiesSummaryDto> GetStationCapabilitiesSummaryAsync()
            => throw new NotSupportedException();

        public Task<HashSet<string>> GetStationIdsByProfileFiltersAsync(IEnumerable<string>? vehicleCategories = null, IEnumerable<int>? stationTypes = null)
            => throw new NotSupportedException();
    }

    private sealed class StubProcessedFileRepository : IProcessedFileRepository
    {
        public Task<bool> IsFileHashProcessedAsync(string fileHash)
            => throw new NotSupportedException();

        public Task<bool> TryInsertProcessedFileAsync(IDbConnection connection, IDbTransaction transaction, ProcessedFileRecord record)
            => throw new NotSupportedException();
    }
}
