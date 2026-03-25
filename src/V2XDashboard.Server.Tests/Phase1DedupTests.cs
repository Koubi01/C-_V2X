using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Server.Services.PcapReader;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Services.PcapReader.TsharkWrapper;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public class Phase1DedupTests
{
    [Fact]
    public async Task ProcessPcapFileAsync_WhenHashAlreadyProcessed_SkipsParserAndStorage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"v2x-phase1-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var fileName = "duplicate.pcap";
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3, 4, 5 });

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=v2x_database;Username=v2x_admin;Password=supertajneheslo",
                    ["PcapDataPath"] = tempDir
                })
                .Build();

            var tsharkParser = new CountingTsharkParser();
            var packetRepository = new CountingPacketRepository();
            var processedFileRepository = new StubProcessedFileRepository(isAlreadyProcessed: true);

            var service = new PcapService(
                configuration,
                new StubCamDecoder(),
                new StubDenmDecoder(),
                new StubMapemDecoder(),
                new StubSpatemDecoder(),
                new StubSremDecoder(),
                new StubSsemDecoder(),
                tsharkParser,
                packetRepository,
                new StubV2XMessageRepository(),
                new StubCorrelationRepository(),
                new StubMapEntityRepository(),
                new StubStationProfileRepository(),
                processedFileRepository,
                NullLogger<PcapService>.Instance);

            var success = await service.ProcessPcapFileAsync(fileName);

            Assert.True(success);
            Assert.True(processedFileRepository.IsHashCheckCalled);
            Assert.Equal(0, tsharkParser.ExtractCalls);
            Assert.Equal(0, packetRepository.InsertCalls);
            Assert.Equal(0, processedFileRepository.TryInsertCalls);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private sealed class CountingTsharkParser : ITsharkParser
    {
        public int ExtractCalls { get; private set; }

        public Task<List<Packet>> ExtractPacketsAsync(string pcapFilePath, string filter = "")
        {
            ExtractCalls++;
            return Task.FromResult(new List<Packet>());
        }
    }

    private sealed class CountingPacketRepository : IPacketRepository
    {
        public int InsertCalls { get; private set; }

        public Task InsertPacketsAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<Packet> packets)
        {
            InsertCalls++;
            return Task.CompletedTask;
        }

        public Task<List<Packet>> GetPacketsAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int? limit = null)
            => throw new NotSupportedException();

        public Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int pageNumber = 1, int pageSize = 25)
            => throw new NotSupportedException();

        public Task<Packet?> GetPacketByIdAsync(int id)
            => throw new NotSupportedException();

        public Task<SecurityMetadataSummaryDto> GetSecurityMetadataSummaryAsync()
            => throw new NotSupportedException();
    }

    private sealed class StubProcessedFileRepository : IProcessedFileRepository
    {
        private readonly bool _isAlreadyProcessed;

        public bool IsHashCheckCalled { get; private set; }
        public int TryInsertCalls { get; private set; }

        public StubProcessedFileRepository(bool isAlreadyProcessed)
        {
            _isAlreadyProcessed = isAlreadyProcessed;
        }

        public Task<bool> IsFileHashProcessedAsync(string fileHash)
        {
            IsHashCheckCalled = true;
            return Task.FromResult(_isAlreadyProcessed);
        }

        public Task<bool> TryInsertProcessedFileAsync(IDbConnection connection, IDbTransaction transaction, ProcessedFileRecord record)
        {
            TryInsertCalls++;
            return Task.FromResult(true);
        }
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
        public Task<List<CAM>> GetCAMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<DENM>> GetDENMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<SREM>> GetSREMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null) => throw new NotSupportedException();
        public Task<V2XMessage?> GetV2XMessageByIdAsync(int id) => throw new NotSupportedException();
    }

    private sealed class StubCorrelationRepository : ICorrelationRepository
    {
        public Task PopulateIntersectionMetadataForFileAsync(string fileName) => Task.CompletedTask;
        public Task<CorrelationRecordSummary> RecordOBUToRSUCorrelationAsync(int timeWindowSeconds) => throw new NotSupportedException();
        public Task<List<CorrelationDto>> GetCorrelationsAsync(string? obuStationId = null, string? rsuIntersectionId = null, string? requestId = null, string? correlationType = null, DateTime? fromTime = null, DateTime? toTime = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, int? limit = null) => throw new NotSupportedException();
        public Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(string? obuStationId = null, string? rsuIntersectionId = null, string? requestId = null, string? correlationType = null, DateTime? fromTime = null, DateTime? toTime = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, int pageNumber = 1, int pageSize = 10) => throw new NotSupportedException();
        public Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId) => throw new NotSupportedException();
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

        public Task<HashSet<string>> GetStationIdsByProfileFiltersAsync(
            IEnumerable<string>? vehicleCategories = null,
            IEnumerable<int>? stationTypes = null)
            => Task.FromResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}