using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Services.PcapReader.TsharkWrapper;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader;

public class PcapService : IPcapService
{
    private readonly string _connectionString;
    private readonly string _pcapDataPath;
    private readonly ITsharkParser _tsharkWrapper;
    private readonly ICamDecoder _camDecoder;
    private readonly IDenmDecoder _denmDecoder;
    private readonly IMapemDecoder _mapemDecoder;
    private readonly ISpatemDecoder _spatemDecoder;
    private readonly ISremDecoder _sremDecoder;
    private readonly ISsemDecoder _ssemDecoder;
    private readonly IPacketRepository _packetRepository;
    private readonly IV2XMessageRepository _v2xMessageRepository;
    private readonly ICorrelationRepository _correlationRepository;
    private readonly IMapTileService _mapTileService;
    private readonly IMapEntityRepository _mapEntityRepository;
    private readonly IStationProfileRepository _stationProfileRepository;
    private readonly IProcessedFileRepository _processedFileRepository;
    private readonly ILogger<PcapService> _logger;

    public PcapService(
        IConfiguration configuration,
        ICamDecoder camDecoder,
        IDenmDecoder denmDecoder,
        IMapemDecoder mapemDecoder,
        ISpatemDecoder spatemDecoder,
        ISremDecoder sremDecoder,
        ISsemDecoder ssemDecoder,
        ITsharkParser tsharkParser,
        IPacketRepository packetRepository,
        IV2XMessageRepository v2XMessageRepository,
        ICorrelationRepository correlationRepository,
        IMapTileService mapTileService,
        IMapEntityRepository mapEntityRepository,
        IStationProfileRepository stationProfileRepository,
        IProcessedFileRepository processedFileRepository,
        ILogger<PcapService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
            throw new ArgumentNullException("DefaultConnection connection string not found");
        _pcapDataPath = configuration.GetValue<string>("PcapDataPath") ?? "/app/PcapData";
        _tsharkWrapper = tsharkParser;
        _camDecoder = camDecoder;
        _denmDecoder = denmDecoder;
        _mapemDecoder = mapemDecoder;
        _spatemDecoder = spatemDecoder;
        _sremDecoder = sremDecoder;
        _ssemDecoder = ssemDecoder;
        _packetRepository = packetRepository;
        _v2xMessageRepository = v2XMessageRepository;
        _correlationRepository = correlationRepository;
        _mapTileService = mapTileService;
        _mapEntityRepository = mapEntityRepository;
        _stationProfileRepository = stationProfileRepository;
        _processedFileRepository = processedFileRepository;
        _logger = logger;
    }

    public async Task<List<string>> GetPcapFilesAsync()
    {
        try
        {
            if (!Directory.Exists(_pcapDataPath))
            {
                _logger.LogWarning("PCAP data directory not found: {PcapDataPath}", _pcapDataPath);
                return new List<string>();
            }

            var directoryInfo = new DirectoryInfo(_pcapDataPath);
            var files = directoryInfo.GetFiles()
                .Where(f => f.Extension.Equals(".pcap", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".cap", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToList();

            return await Task.FromResult(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PCAP files from {PcapDataPath}", _pcapDataPath);
            return new List<string>();
        }
    }

    public async Task<bool> ProcessPcapFileAsync(string fileName)
    {
        try
        {
            var totalStopwatch = Stopwatch.StartNew();
            var filePath = Path.Combine(_pcapDataPath, fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"PCAP file not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var fileHash = await ComputeFileSha256Async(filePath);

            if (await _processedFileRepository.IsFileHashProcessedAsync(fileHash))
            {
                _logger.LogInformation(
                    "Skipping already processed file {FileName} (sha256={FileHash})",
                    fileName,
                    fileHash);
                return true;
            }

            // Extract packets using TsharkWrapper
            var parseStopwatch = Stopwatch.StartNew();
            var packets = await _tsharkWrapper.ExtractPacketsAsync(filePath);
            parseStopwatch.Stop();

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            // Store packets in database
            var storePacketsStopwatch = Stopwatch.StartNew();
            await StorePacketsAsync(connection, transaction, packets);
            storePacketsStopwatch.Stop();

            // Process V2X messages from packets
            var processMessagesStopwatch = Stopwatch.StartNew();
            await ProcessV2XMessagesAsync(connection, transaction, packets, fileName);
            processMessagesStopwatch.Stop();

            var processedFileInserted = await _processedFileRepository.TryInsertProcessedFileAsync(
                connection,
                transaction,
                new ProcessedFileRecord
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    FileHash = fileHash,
                    PacketCount = packets.Count,
                    Status = "Processed"
                });

            if (!processedFileInserted)
            {
                await transaction.RollbackAsync();
                _logger.LogInformation(
                    "Skipping file {FileName} because hash {FileHash} was recorded while processing.",
                    fileName,
                    fileHash);
                return true;
            }

            await transaction.CommitAsync();
            _mapTileService.InvalidateAllTiles();

            // Run a second pass after the entire file is stored to maximize intersection enrichment.
            var enrichmentStopwatch = Stopwatch.StartNew();
            await PopulateIntersectionMetadataForFileAsync(fileName);
            _mapTileService.InvalidateLayer(TileLayerNames.Spatem);
            _mapTileService.InvalidateLayer(TileLayerNames.Srem);
            _mapTileService.InvalidateLayer(TileLayerNames.Ssem);
            enrichmentStopwatch.Stop();

            var stationProfilesStopwatch = Stopwatch.StartNew();
            await RefreshStationProfilesAsync();
            stationProfilesStopwatch.Stop();

            totalStopwatch.Stop();

            _logger.LogInformation(
                "ProcessPcapFileAsync timings for {FileName}: parse={ParseMs}ms, storePackets={StorePacketsMs}ms, processV2X={ProcessV2XMs}ms, enrichment={EnrichmentMs}ms, stationProfiles={StationProfilesMs}ms, total={TotalMs}ms, packets={PacketCount}",
                fileName,
                parseStopwatch.ElapsedMilliseconds,
                storePacketsStopwatch.ElapsedMilliseconds,
                processMessagesStopwatch.ElapsedMilliseconds,
                enrichmentStopwatch.ElapsedMilliseconds,
                stationProfilesStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds,
                packets.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PCAP file {FileName}", fileName);
            return false;
        }
    }

    private static async Task<string> ComputeFileSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes);
    }

    public async Task<bool> ProcessAllPcapFilesAsync()
    {
        try
        {
            var files = await GetPcapFilesAsync();
            var allSucceeded = true;

            foreach (var file in files)
            {
                bool success = await ProcessPcapFileAsync(file);
                if (!success)
                {
                    allSucceeded = false;
                    _logger.LogWarning("Failed to process file {FileName}", file);
                }
            }

            var globalEnrichmentStopwatch = Stopwatch.StartNew();
            await PopulateIntersectionMetadataForAllFilesAsync();
            _mapTileService.InvalidateLayer(TileLayerNames.Spatem);
            _mapTileService.InvalidateLayer(TileLayerNames.Srem);
            _mapTileService.InvalidateLayer(TileLayerNames.Ssem);
            globalEnrichmentStopwatch.Stop();

            _logger.LogInformation(
                "Global enrichment completed after process-all: files={FileCount}, duration={DurationMs}ms",
                files.Count,
                globalEnrichmentStopwatch.ElapsedMilliseconds);

            var correlationStopwatch = Stopwatch.StartNew();
            await RecordOBUToRSUCorrelationAsync();
            correlationStopwatch.Stop();

            _logger.LogInformation(
                "Final correlation pass completed after process-all: files={FileCount}, duration={DurationMs}ms",
                files.Count,
                correlationStopwatch.ElapsedMilliseconds);
            
            return allSucceeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing all PCAP files");
            return false;
        }
    }

    public async Task<List<Packet>> GetPacketsAsync(
        string? filter = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        string? signerId = null,
        int? limit = null)
    {
        return await _packetRepository.GetPacketsAsync(filter, isSecureSigned, isSecureEncrypted, signerId, limit);
    }

    public async Task<PagedResult<Packet>> GetPacketsPagedAsync(
        string? filter = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        string? signerId = null,
        int pageNumber = 1,
        int pageSize = 25)
    {
        return await _packetRepository.GetPacketsPagedAsync(filter, isSecureSigned, isSecureEncrypted, signerId, pageNumber, pageSize);
    }

    public async Task<SecurityMetadataSummaryDto> GetSecurityMetadataSummaryAsync()
    {
        return await _packetRepository.GetSecurityMetadataSummaryAsync();
    }

    public async Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25)
    {
        return await _v2xMessageRepository.GetMessageListPagedAsync(messageType, pageNumber, pageSize);
    }

    public async Task<MessageCountsDto> GetMessageCountsAsync()
    {
        return await _v2xMessageRepository.GetMessageCountsAsync();
    }

    public async Task<List<V2XMessage>> GetV2XMessagesAsync(string? messageType = null, int? limit = null)
    {
        var allMessages = new List<V2XMessage>();

        if (!string.IsNullOrEmpty(messageType))
        {
            var normalizedMessageType = messageType.Trim().ToUpperInvariant();

            switch (normalizedMessageType)
            {
                case "CAM":
                    allMessages.AddRange(await GetCAMMessagesAsync(limit));
                    break;
                case "DENM":
                    allMessages.AddRange(await GetDENMMessagesAsync(limit));
                    break;
                case "MAPEM":
                    allMessages.AddRange(await GetMAPEMMessagesAsync(limit));
                    break;
                case "SPATEM":
                    allMessages.AddRange(await GetSPATEMMessagesAsync(limit));
                    break;
                case "SREM":
                    allMessages.AddRange(await GetSREMMessagesAsync(limit));
                    break;
                case "SSEM":
                    allMessages.AddRange(await GetSSEMMessagesAsync(limit));
                    break;
                default:
                    throw new ArgumentException($"Unsupported messageType '{messageType}'.", nameof(messageType));
            }
        }
        else
        {
            // Get all message types
            allMessages.AddRange(await GetCAMMessagesAsync(limit));
            allMessages.AddRange(await GetDENMMessagesAsync(limit));
            allMessages.AddRange(await GetMAPEMMessagesAsync(limit));
            allMessages.AddRange(await GetSPATEMMessagesAsync(limit));
            allMessages.AddRange(await GetSREMMessagesAsync(limit));
            allMessages.AddRange(await GetSSEMMessagesAsync(limit));
        }

        // Sort by generation time descending
        return allMessages.OrderByDescending(m => m.GenerationTime).ToList();
    }

    public async Task<List<CAM>> GetCAMMessagesAsync(int? limit = null)
    {
        return await _v2xMessageRepository.GetCAMMessagesAsync(limit);
    }

    public async Task<List<DENM>> GetDENMMessagesAsync(int? limit = null)
    {
        return await _v2xMessageRepository.GetDENMMessagesAsync(limit);
    }

    public async Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null)
    {
        return await _v2xMessageRepository.GetMAPEMMessagesAsync(limit);
    }

    public async Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null)
    {
        return await _v2xMessageRepository.GetSPATEMMessagesAsync(limit);
    }

    public async Task<List<SREM>> GetSREMMessagesAsync(int? limit = null)
    {
        return await _v2xMessageRepository.GetSREMMessagesAsync(limit);
    }

    public async Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null)
    {
        return await _v2xMessageRepository.GetSSEMMessagesAsync(limit);
    }

    public async Task<Packet?> GetPacketByIdAsync(int id)
    {
        return await _packetRepository.GetPacketByIdAsync(id);
    }

    public async Task<V2XMessage?> GetV2XMessageByIdAsync(int id, string? messageType = null)
    {
        return await _v2xMessageRepository.GetV2XMessageByIdAsync(id, messageType);
    }

    private async Task StorePacketsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Packet> packets)
    {
        await _packetRepository.InsertPacketsAsync(connection, transaction, packets);
    }

    private async Task ProcessV2XMessagesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Packet> packets, string fileName)
    {
        var v2xPackets = packets.Where(p =>
            p.PacketType == "CAM" ||
            p.PacketType == "DENM" ||
            p.PacketType == "MAPEM" ||
            p.PacketType == "SPATEM" ||
            p.PacketType == "SREM" ||
            p.PacketType == "SSEM").ToList();

        // Group by message type and batch decode + store
        var camPackets = v2xPackets.Where(p => p.PacketType == "CAM").ToList();
        var denmPackets = v2xPackets.Where(p => p.PacketType == "DENM").ToList();
        var mapemPackets = v2xPackets.Where(p => p.PacketType == "MAPEM").ToList();
        var spatemPackets = v2xPackets.Where(p => p.PacketType == "SPATEM").ToList();
        var sremPackets = v2xPackets.Where(p => p.PacketType == "SREM").ToList();
        var ssemPackets = v2xPackets.Where(p => p.PacketType == "SSEM").ToList();

        if (camPackets.Count > 0) await StoreCAMBatchAsync(connection, transaction, camPackets, fileName);
        if (denmPackets.Count > 0) await StoreDENMBatchAsync(connection, transaction, denmPackets, fileName);
        if (mapemPackets.Count > 0) await StoreMAPEMBatchAsync(connection, transaction, mapemPackets, fileName);
        if (spatemPackets.Count > 0) await StoreSPATEMBatchAsync(connection, transaction, spatemPackets, fileName);
        if (sremPackets.Count > 0) await StoreSREMBatchAsync(connection, transaction, sremPackets, fileName);
        if (ssemPackets.Count > 0) await StoreSSEMBatchAsync(connection, transaction, ssemPackets, fileName);
    }

    private async Task StoreCAMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets, string fileName)
    {
        var decoded = packets
            .Select(packet => _camDecoder.DecodeCAM(packet))
            .ToList();

        LogCoordinateQuality(fileName, "CAM", decoded.Count, decoded.Count(m => HasValidCoordinates(m.Latitude, m.Longitude)));

        await _v2xMessageRepository.InsertCAMBatchAsync(connection, transaction, decoded);
    }

    private async Task StoreDENMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets, string fileName)
    {
        var decoded = packets
            .Select(packet => _denmDecoder.DecodeDENM(packet))
            .ToList();

        LogCoordinateQuality(fileName, "DENM", decoded.Count, decoded.Count(m => HasValidCoordinates(m.Latitude, m.Longitude)));

        await _v2xMessageRepository.InsertDENMBatchAsync(connection, transaction, decoded);
    }

    private async Task StoreMAPEMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets, string fileName)
    {
        var decoded = packets
            .Select(packet => _mapemDecoder.DecodeMAPEM(packet))
            .ToList();

        LogCoordinateQuality(fileName, "MAPEM", decoded.Count, decoded.Count(m => HasValidCoordinates(m.Latitude, m.Longitude)));

        await _v2xMessageRepository.InsertMAPEMBatchAsync(connection, transaction, decoded);
    }

    private async Task StoreSPATEMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets, string fileName)
    {
        var decoded = packets
            .Select(packet => _spatemDecoder.DecodeSPATEM(packet))
            .ToList();

        LogCoordinateQuality(fileName, "SPATEM", decoded.Count, decoded.Count(m => HasValidCoordinates(m.Latitude, m.Longitude)));

        await _v2xMessageRepository.InsertSPATEMBatchAsync(connection, transaction, decoded);
    }

    private async Task StoreSREMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets, string fileName)
    {
        var decoded = packets
            .Select(packet => _sremDecoder.DecodeSREM(packet))
            .ToList();

        LogCoordinateQuality(fileName, "SREM", decoded.Count, decoded.Count(m => HasValidCoordinates(m.Latitude, m.Longitude)));

        await _v2xMessageRepository.InsertSREMBatchAsync(connection, transaction, decoded);
    }

    private async Task StoreSSEMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets, string fileName)
    {
        var decoded = packets
            .Select(packet => _ssemDecoder.DecodeSSEM(packet))
            .ToList();

        LogCoordinateQuality(fileName, "SSEM", decoded.Count, decoded.Count(m => HasValidCoordinates(m.Latitude, m.Longitude)));

        await _v2xMessageRepository.InsertSSEMBatchAsync(connection, transaction, decoded);
    }

    private void LogCoordinateQuality(string fileName, string messageType, int totalCount, int nonZeroCount)
    {
        _logger.LogInformation(
            "Decode quality for {FileName}/{MessageType}: nonZeroCoordinates={NonZeroCount}/{TotalCount}",
            fileName,
            messageType,
            nonZeroCount,
            totalCount);
    }

    private static bool HasValidCoordinates(double latitude, double longitude)
    {
        return latitude != 0d && longitude != 0d;
    }

    private async Task PopulateIntersectionMetadataForFileAsync(string fileName)
    {
        await _correlationRepository.PopulateIntersectionMetadataForFileAsync(fileName);
    }

    private async Task PopulateIntersectionMetadataForAllFilesAsync()
    {
        await _correlationRepository.PopulateIntersectionMetadataForAllFilesAsync();
    }

    /// <summary>
    /// Records OBU-RSU correlations by matching SREM requests to SSEM responses.
    /// Uses strict matching (requestId == requestIdRef) first, then fallback matching (intersectionId + time window).
    /// </summary>
    public async Task RecordOBUToRSUCorrelationAsync()
    {
        try
        {
            const int timeWindowSeconds = 10;
            var summary = await _correlationRepository.RecordOBUToRSUCorrelationAsync(timeWindowSeconds);

            _logger.LogInformation(
                "Correlation summary (set-based): scanned={TotalSrems}, selected={SelectedCandidates}, inserted={InsertedTotal}, strict={InsertedStrict}, fallback={InsertedFallback}, duplicatesSkipped={DuplicatesSkipped}",
                summary.TotalSrems,
                summary.SelectedCandidates,
                summary.InsertedTotal,
                summary.InsertedStrict,
                summary.InsertedFallback,
                summary.SelectedCandidates - summary.InsertedTotal);

            if (summary.InsertedTotal > 0)
            {
                _mapTileService.InvalidateLayer(TileLayerNames.Correlations);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording OBU-RSU correlations");
            throw;
        }
    }

    /// <summary>
    /// Query OBU-RSU correlations with optional filters for map visualization.
    /// </summary>
    public async Task<List<CorrelationDto>> GetCorrelationsAsync(
        string? obuStationId = null,
        string? rsuIntersectionId = null,
        string? requestId = null,
        string? correlationType = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        int? limit = null)
    {
        return await _correlationRepository.GetCorrelationsAsync(
            obuStationId,
            rsuIntersectionId,
            requestId,
            correlationType,
            fromTime,
            toTime,
            isSecureSigned,
            isSecureEncrypted,
            limit);
    }

    public async Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(
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
    {
        return await _correlationRepository.GetCorrelationsPagedAsync(
            obuStationId,
            rsuIntersectionId,
            requestId,
            correlationType,
            fromTime,
            toTime,
            isSecureSigned,
            isSecureEncrypted,
            pageNumber,
            pageSize);
    }

    private static (int PageNumber, int PageSize, int Offset) NormalizePaging(int pageNumber, int pageSize, int maxPageSize = 100)
    {
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize < 1 ? 25 : Math.Min(pageSize, maxPageSize);
        var offset = (safePageNumber - 1) * safePageSize;
        return (safePageNumber, safePageSize, offset);
    }

    /// <summary>
    /// Get the SSEM response for a specific SREM request.
    /// </summary>
    public async Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId)
    {
        return await _correlationRepository.GetSsemForSremAsync(sremId);
    }

    /// <summary>
    /// Get map entities (OBU positions from CAM, RSU intersections from MAPEM/SPATEM).
    /// </summary>
    public async Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null)
    {
        return await _mapEntityRepository.GetMapEntitiesAsync(fromTime, toTime);
    }

    public async Task RefreshStationProfilesAsync()
    {
        await _stationProfileRepository.RebuildStationProfilesAsync();
    }

    public async Task<PagedResult<StationProfileDto>> GetStationProfilesPagedAsync(
        string? stationId = null,
        string? entityType = null,
        string? messageType = null,
        string? vehicleCategory = null,
        int? stationType = null,
        bool? supportsSecureComm = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        return await _stationProfileRepository.GetStationProfilesPagedAsync(
            stationId,
            entityType,
            messageType,
            vehicleCategory,
            stationType,
            supportsSecureComm,
            pageNumber,
            pageSize);
    }

    public async Task<StationProfileDto?> GetStationProfileByStationIdAsync(string stationId)
    {
        return await _stationProfileRepository.GetStationProfileByStationIdAsync(stationId);
    }

    public async Task<StationCapabilitiesSummaryDto> GetStationCapabilitiesAsync()
    {
        return await _stationProfileRepository.GetStationCapabilitiesSummaryAsync();
    }

    public async Task<PagedResult<MapEntityDto>> GetMapEntitiesPagedAsync(
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
    {
        var (safePageNumber, safePageSize, offset) = NormalizePaging(pageNumber, pageSize, 500);

        var all = await GetMapEntitiesAsync(fromTime, toTime);

        var normalizedEntityTypes = (entityTypes ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedMessageTypes = (messageTypes ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedVehicleCategories = (vehicleCategories ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedStationTypes = (stationTypes ?? Array.Empty<int>())
            .ToHashSet();

        HashSet<string> stationIdsFromProfile = new(StringComparer.OrdinalIgnoreCase);
        if (normalizedVehicleCategories.Count > 0 || selectedStationTypes.Count > 0)
        {
            stationIdsFromProfile = await _stationProfileRepository.GetStationIdsByProfileFiltersAsync(
                normalizedVehicleCategories.Count > 0 ? normalizedVehicleCategories : null,
                selectedStationTypes.Count > 0 ? selectedStationTypes : null);
        }

        var filtered = all
            .Where(entity => normalizedEntityTypes.Count == 0 || normalizedEntityTypes.Contains(entity.EntityType))
            .Where(entity => normalizedMessageTypes.Count == 0 || normalizedMessageTypes.Contains(entity.MessageType))
            .Where(entity =>
                (normalizedVehicleCategories.Count == 0 && selectedStationTypes.Count == 0) ||
                (!string.IsNullOrWhiteSpace(entity.StationId) && stationIdsFromProfile.Contains(entity.StationId)))
            .Where(entity => !isSecureSigned.HasValue || entity.IsSecureSigned == isSecureSigned.Value)
            .Where(entity => !isSecureEncrypted.HasValue || entity.IsSecureEncrypted == isSecureEncrypted.Value)
            .OrderByDescending(entity => entity.GenerationTime)
            .ToList();

        var pagedItems = filtered
            .Skip(offset)
            .Take(safePageSize)
            .ToList();

        return new PagedResult<MapEntityDto>
        {
            Items = pagedItems,
            TotalCount = filtered.Count,
            PageNumber = safePageNumber,
            PageSize = safePageSize
        };
    }
}
