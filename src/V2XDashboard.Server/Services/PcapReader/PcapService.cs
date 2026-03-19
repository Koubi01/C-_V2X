using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Services.PcapReader.TsharkWrapper;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader;

public class PcapService : IPcapService
{
    private readonly string _connectionString;
    private readonly string _pcapDataPath;
    private readonly TsharkParser _tsharkWrapper;
    private readonly IV2XMessageDecoder _v2xMessageDecoder;

    public PcapService(IConfiguration configuration, IV2XMessageDecoder v2xMessageDecoder)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
            throw new ArgumentNullException("DefaultConnection connection string not found");
        _pcapDataPath = configuration.GetValue<string>("PcapDataPath") ?? "/app/PcapData";
        _tsharkWrapper = new TsharkParser();
        _v2xMessageDecoder = v2xMessageDecoder;
    }

    public async Task<List<string>> GetPcapFilesAsync()
    {
        // In a real implementation, this would scan the _pcapDataPath directory
        // For now, return a hardcoded list based on the known files
        return new List<string>
        {
            "data1.pcap",
            "ddddddd.pcap",
            "dump_06-31-50.pcap",
            "dump_06-41-15.pcap",
            "dump_06-48-28.pcap",
            "dump_06-49-36.pcap",
            "dump_06-50-23.pcap",
            "dump_06-50-57.pcap",
            "dump_06-51-40.pcap",
            "dump_06-51-41.pcap",
            "dump_07-10-27.pcap",
            "dump_07-12-36.pcap",
            "dump_16-14-16.pcap"
        };
    }

    public async Task<bool> ProcessPcapFileAsync(string fileName)
    {
        try
        {
            var totalStopwatch = Stopwatch.StartNew();
            var filePath = Path.Combine(_pcapDataPath, fileName);

            // Check if file exists (in container environment)
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"PCAP file not found: {filePath}");
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
            await ProcessV2XMessagesAsync(connection, transaction, packets);
            processMessagesStopwatch.Stop();

            await transaction.CommitAsync();

            // Run a second pass after the entire file is stored to maximize intersection enrichment.
            var enrichmentStopwatch = Stopwatch.StartNew();
            await PopulateIntersectionMetadataForFileAsync(fileName);
            enrichmentStopwatch.Stop();

            totalStopwatch.Stop();

            Console.WriteLine(
                $"ProcessPcapFileAsync timings for '{fileName}': " +
                $"parse={parseStopwatch.ElapsedMilliseconds}ms, " +
                $"storePackets={storePacketsStopwatch.ElapsedMilliseconds}ms, " +
                $"processV2X={processMessagesStopwatch.ElapsedMilliseconds}ms, " +
                $"enrichment={enrichmentStopwatch.ElapsedMilliseconds}ms, " +
                $"total={totalStopwatch.ElapsedMilliseconds}ms, packets={packets.Count}");

            return true;
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error processing PCAP file {fileName}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ProcessAllPcapFilesAsync()
    {
        try
        {
            var files = await GetPcapFilesAsync();
            foreach (var file in files)
            {
                bool success = await ProcessPcapFileAsync(file);
                if (!success)
                {
                    Console.WriteLine($"Failed to process file: {file}");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing all PCAP files: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Packet>> GetPacketsAsync(string? filter = null, int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var query = "SELECT * FROM packets";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(filter))
        {
            query += " WHERE packet_type = @Filter";
            parameters.Add("@Filter", filter);
        }

        query += " ORDER BY timestamp DESC";

        if (limit.HasValue)
        {
            query += " LIMIT @Limit";
            parameters.Add("@Limit", limit.Value);
        }

        var packets = await connection.QueryAsync<Packet>(query, parameters);
        return packets.ToList();
    }

    public async Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, int pageNumber = 1, int pageSize = 25)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var (safePageNumber, safePageSize, offset) = NormalizePaging(pageNumber, pageSize);

        var whereClause = string.IsNullOrWhiteSpace(filter)
            ? string.Empty
            : @" WHERE packet_type ILIKE @LikeFilter
                    OR protocol ILIKE @LikeFilter
                    OR pcap_file_name ILIKE @LikeFilter
                    OR source_ip ILIKE @LikeFilter
                    OR destination_ip ILIKE @LikeFilter";

        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            parameters.Add("@LikeFilter", $"%{filter}%");
        }

        parameters.Add("@Limit", safePageSize);
        parameters.Add("@Offset", offset);

        var totalCount = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM packets{whereClause}", parameters);

        var dataQuery = $@"
            SELECT *
            FROM packets
            {whereClause}
            ORDER BY timestamp DESC
            LIMIT @Limit OFFSET @Offset";

        var items = (await connection.QueryAsync<Packet>(dataQuery, parameters)).ToList();

        return new PagedResult<Packet>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = safePageNumber,
            PageSize = safePageSize
        };
    }

    public async Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var (safePageNumber, safePageSize, offset) = NormalizePaging(pageNumber, pageSize);
        var normalizedType = (messageType ?? string.Empty).Trim().ToUpperInvariant();

        (string CountQuery, string DataQuery) queries = normalizedType switch
        {
            "CAM" => (
                "SELECT COUNT(*) FROM cam_messages",
                @"SELECT
                    'CAM' AS MessageType,
                    generation_time AS GenerationTime,
                    station_id AS StationLabel,
                    latitude AS Latitude,
                    longitude AS Longitude,
                    CONCAT('Speed ', ROUND(COALESCE(speed, 0)::numeric, 1), ' m/s, heading ', ROUND(COALESCE(heading, 0)::numeric, 1), ', role ', COALESCE(vehicle_role, 'n/a')) AS Detail
                  FROM cam_messages
                  ORDER BY generation_time DESC
                  LIMIT @Limit OFFSET @Offset"
            ),
            "DENM" => (
                "SELECT COUNT(*) FROM denm_messages",
                @"SELECT
                    'DENM' AS MessageType,
                    generation_time AS GenerationTime,
                    station_id AS StationLabel,
                    latitude AS Latitude,
                    longitude AS Longitude,
                    CONCAT('Cause ', COALESCE(cause_code, 'n/a'), ', station type ', COALESCE(station_type, 0)) AS Detail
                  FROM denm_messages
                  ORDER BY generation_time DESC
                  LIMIT @Limit OFFSET @Offset"
            ),
            "MAPEM" => (
                "SELECT COUNT(*) FROM mapem_messages",
                @"SELECT
                    'MAPEM' AS MessageType,
                    generation_time AS GenerationTime,
                    COALESCE(NULLIF(publisher_id, ''), station_id, 'n/a') AS StationLabel,
                    latitude AS Latitude,
                    longitude AS Longitude,
                    CONCAT('Intersection ', COALESCE(intersection_id, 0), ', lanes ', COALESCE(lane_count, 0), ', version ', COALESCE(map_version, 'n/a')) AS Detail
                  FROM mapem_messages
                  ORDER BY generation_time DESC
                  LIMIT @Limit OFFSET @Offset"
            ),
            "SPATEM" => (
                "SELECT COUNT(*) FROM spatem_messages",
                @"SELECT
                    'SPATEM' AS MessageType,
                    generation_time AS GenerationTime,
                    COALESCE(NULLIF(publisher_id, ''), station_id, 'n/a') AS StationLabel,
                    latitude AS Latitude,
                    longitude AS Longitude,
                    CONCAT('Intersection ', COALESCE(intersection_id, 0), ', phase ', COALESCE(current_phase, 0), ', state ', COALESCE(phase_state, 'n/a')) AS Detail
                  FROM spatem_messages
                  ORDER BY generation_time DESC
                  LIMIT @Limit OFFSET @Offset"
            ),
            "SREM" => (
                "SELECT COUNT(*) FROM srem_messages",
                @"SELECT
                    'SREM' AS MessageType,
                    generation_time AS GenerationTime,
                    COALESCE(NULLIF(requestor_id, ''), station_id, 'n/a') AS StationLabel,
                    latitude AS Latitude,
                    longitude AS Longitude,
                    CONCAT('Request ', COALESCE(request_id, 'n/a'), ', phase ', COALESCE(requested_phase, 0), ', reason ', COALESCE(request_reason, 'n/a')) AS Detail
                  FROM srem_messages
                  ORDER BY generation_time DESC
                  LIMIT @Limit OFFSET @Offset"
            ),
            "SSEM" => (
                "SELECT COUNT(*) FROM ssem_messages",
                @"SELECT
                    'SSEM' AS MessageType,
                    generation_time AS GenerationTime,
                    COALESCE(NULLIF(responder_id, ''), station_id, 'n/a') AS StationLabel,
                    latitude AS Latitude,
                    longitude AS Longitude,
                    CONCAT('Status ', COALESCE(status_code, 'n/a'), ', duration ', COALESCE(granted_duration, 0), 's, request ', COALESCE(request_id_ref, 'n/a')) AS Detail
                  FROM ssem_messages
                  ORDER BY generation_time DESC
                  LIMIT @Limit OFFSET @Offset"
            ),
            _ => throw new ArgumentException($"Unsupported messageType '{messageType}'.", nameof(messageType))
        };

        var parameters = new DynamicParameters();
        parameters.Add("@Limit", safePageSize);
        parameters.Add("@Offset", offset);

        var totalCount = await connection.ExecuteScalarAsync<int>(queries.CountQuery);
        var items = (await connection.QueryAsync<MessageListItemDto>(queries.DataQuery, parameters)).ToList();

        return new PagedResult<MessageListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = safePageNumber,
            PageSize = safePageSize
        };
    }

    public async Task<MessageCountsDto> GetMessageCountsAsync()
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM packets)              AS total_packets,
                (SELECT COUNT(*) FROM cam_messages)         AS cam,
                (SELECT COUNT(*) FROM denm_messages)        AS denm,
                (SELECT COUNT(*) FROM mapem_messages)       AS mapem,
                (SELECT COUNT(*) FROM spatem_messages)      AS spatem,
                (SELECT COUNT(*) FROM srem_messages)        AS srem,
                (SELECT COUNT(*) FROM ssem_messages)        AS ssem,
                (SELECT COUNT(*) FROM obu_rsu_correlations) AS total_correlations
            """;

        using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleAsync(sql);

        return new MessageCountsDto
        {
            TotalPackets      = (int)row.total_packets,
            CAM               = (int)row.cam,
            DENM              = (int)row.denm,
            MAPEM             = (int)row.mapem,
            SPATEM            = (int)row.spatem,
            SREM              = (int)row.srem,
            SSEM              = (int)row.ssem,
            TotalCorrelations = (int)row.total_correlations
        };
    }

    public async Task<List<V2XMessage>> GetV2XMessagesAsync(string? messageType = null, int? limit = null)
    {
        var allMessages = new List<V2XMessage>();

        // If specific message type requested, query only that table
        if (!string.IsNullOrEmpty(messageType))
        {
            switch (messageType.ToUpper())
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
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT * FROM cam_messages ORDER BY generation_time DESC";
        if (limit.HasValue) query += $" LIMIT {limit.Value}";
        var messages = await connection.QueryAsync<CAM>(query);
        return messages.ToList();
    }

    public async Task<List<DENM>> GetDENMMessagesAsync(int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT * FROM denm_messages ORDER BY generation_time DESC";
        if (limit.HasValue) query += $" LIMIT {limit.Value}";
        var messages = await connection.QueryAsync<DENM>(query);
        return messages.ToList();
    }

    public async Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT * FROM mapem_messages ORDER BY generation_time DESC";
        if (limit.HasValue) query += $" LIMIT {limit.Value}";
        var messages = await connection.QueryAsync<MAPEM>(query);
        return messages.ToList();
    }

    public async Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT * FROM spatem_messages ORDER BY generation_time DESC";
        if (limit.HasValue) query += $" LIMIT {limit.Value}";
        var messages = await connection.QueryAsync<SPATEM>(query);
        return messages.ToList();
    }

    public async Task<List<SREM>> GetSREMMessagesAsync(int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT * FROM srem_messages ORDER BY generation_time DESC";
        if (limit.HasValue) query += $" LIMIT {limit.Value}";
        var messages = await connection.QueryAsync<SREM>(query);
        return messages.ToList();
    }

    public async Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT * FROM ssem_messages ORDER BY generation_time DESC";
        if (limit.HasValue) query += $" LIMIT {limit.Value}";
        var messages = await connection.QueryAsync<SSEM>(query);
        return messages.ToList();
    }

    public async Task<Packet?> GetPacketByIdAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Packet>(
            "SELECT * FROM packets WHERE id = @Id", new { Id = id });
    }

    public async Task<V2XMessage?> GetV2XMessageByIdAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<V2XMessage>(
            "SELECT * FROM v2x_messages WHERE id = @Id", new { Id = id });
    }

    private async Task StorePacketsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0)
        {
            return;
        }

        const int batchSize = 1000;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var sql = new System.Text.StringBuilder();
            sql.AppendLine("INSERT INTO packets (timestamp, source_mac, destination_mac, packet_type, length, source_port, destination_port, payload, pcap_file_name)");
            sql.AppendLine("VALUES");

            var parameters = new DynamicParameters();
            for (var i = 0; i < chunk.Count; i++)
            {
                var packet = chunk[i];
                var suffix = $"_{i}";

                if (i > 0)
                {
                    sql.AppendLine(",");
                }

                sql.Append($"(@Timestamp{suffix}, @SourceMac{suffix}, @DestinationMac{suffix}, @PacketType{suffix}, @Length{suffix}, @SourcePort{suffix}, @DestinationPort{suffix}, @Payload{suffix}, @PcapFileName{suffix})");

                parameters.Add($"@Timestamp{suffix}", packet.Timestamp);
                parameters.Add($"@SourceMac{suffix}", packet.SourceMac);
                parameters.Add($"@DestinationMac{suffix}", packet.DestinationMac);
                parameters.Add($"@PacketType{suffix}", packet.PacketType);
                parameters.Add($"@Length{suffix}", packet.Length);
                parameters.Add($"@SourcePort{suffix}", packet.SourcePort);
                parameters.Add($"@DestinationPort{suffix}", packet.DestinationPort);
                parameters.Add($"@Payload{suffix}", packet.Payload);
                parameters.Add($"@PcapFileName{suffix}", packet.PcapFileName);
            }

            sql.AppendLine();
            sql.AppendLine("RETURNING id");

            var returnedIds = (await connection.QueryAsync<int>(sql.ToString(), parameters, transaction)).ToList();
            for (var i = 0; i < chunk.Count && i < returnedIds.Count; i++)
            {
                chunk[i].Id = returnedIds[i];
            }
        }
    }

    private async Task ProcessV2XMessagesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Packet> packets)
    {
        var v2xPackets = packets.Where(p =>
            p.PacketType == "CAM" ||
            p.PacketType == "DENM" ||
            p.PacketType == "MAPEM" ||
            p.PacketType == "SPATEM" ||
            p.PacketType == "SREM" ||
            p.PacketType == "SSEM").ToList();

        foreach (var packet in v2xPackets)
        {
            // This is a simplified implementation
            // Real V2X message parsing would require decoding the actual protocol data
            await StoreV2XMessageFromPacketAsync(connection, transaction, packet);
        }
    }

    private async Task StoreV2XMessageFromPacketAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        switch (packet.PacketType)
        {
            case "CAM":
                await StoreCAMAsync(connection, transaction, packet);
                break;
            case "DENM":
                await StoreDENMAsync(connection, transaction, packet);
                break;
            case "MAPEM":
                await StoreMAPEMAsync(connection, transaction, packet);
                break;
            case "SPATEM":
                await StoreSPATEMAsync(connection, transaction, packet);
                break;
            case "SREM":
                await StoreSREMAsync(connection, transaction, packet);
                break;
            case "SSEM":
                await StoreSSEMAsync(connection, transaction, packet);
                break;
        }
    }

    private async Task StoreCAMAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        var decoded = _v2xMessageDecoder.DecodeCAM(packet);

        var query = @"
            INSERT INTO cam_messages (packet_id, generation_time, station_id,
                latitude, longitude, altitude, speed, heading, station_type,
                vehicle_role, acceleration, curvature, yaw_rate,
                lateral_acceleration, vertical_acceleration, decode_status, vehicle_length, vehicle_width)
            VALUES (@PacketId, @GenerationTime, @StationId,
                @Latitude, @Longitude, @Altitude, @Speed, @Heading, @StationType,
                @VehicleRole, @Acceleration, @Curvature, @YawRate,
                @LateralAcceleration, @VerticalAcceleration, @DecodeStatus, @VehicleLength, @VehicleWidth)";

        var parameters = new
        {
            decoded.PacketId,
            decoded.GenerationTime,
            decoded.StationId,
            decoded.Latitude,
            decoded.Longitude,
            decoded.Altitude,
            decoded.Speed,
            decoded.Heading,
            decoded.StationType,
            VehicleRole = string.IsNullOrWhiteSpace(decoded.VehicleRole) ? "unknown" : decoded.VehicleRole,
            decoded.Acceleration,
            decoded.Curvature,
            decoded.YawRate,
            decoded.LateralAcceleration,
            decoded.VerticalAcceleration,
            decoded.DecodeStatus,
            decoded.VehicleLength,
            decoded.VehicleWidth
        };

        await connection.ExecuteAsync(query, parameters, transaction);
    }

    private async Task StoreDENMAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        var decoded = _v2xMessageDecoder.DecodeDENM(packet);

        var query = @"
            INSERT INTO denm_messages (packet_id, generation_time, station_id,
                cause_code, detection_time, reference_time,
                latitude, longitude, altitude,
                relevance_traffic_direction, validity_duration, station_type, awareness_traffic_direction, original_station_type)
            VALUES (@PacketId, @GenerationTime, @StationId,
                @CauseCode, @DetectionTime, @ReferenceTime,
                @Latitude, @Longitude, @Altitude,
                @RelevanceTrafficDirection, @ValidityDuration, @StationType, @AwarenessTrafficDirection, @OriginalStationType)";

        var parameters = new
        {
            decoded.PacketId,
            decoded.GenerationTime,
            decoded.StationId,
            CauseCode = string.IsNullOrWhiteSpace(decoded.CauseCode) ? "unknown" : decoded.CauseCode,
            decoded.DetectionTime,
            decoded.ReferenceTime,
            decoded.Latitude,
            decoded.Longitude,
            decoded.Altitude,
            decoded.RelevanceTrafficDirection,
            decoded.ValidityDuration,
            decoded.StationType,
            decoded.AwarenessTrafficDirection,
            decoded.OriginalStationType
        };

        await connection.ExecuteAsync(query, parameters, transaction);
    }

    private async Task StoreMAPEMAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        var decoded = _v2xMessageDecoder.DecodeMAPEM(packet);

        var query = @"
            INSERT INTO mapem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                lane_count, road_width, speed_limit, map_version, publisher_id)
            VALUES (@PacketId, @GenerationTime, @StationId,
                @IntersectionId, @IntersectionName, @Latitude, @Longitude,
                @LaneCount, @RoadWidth, @SpeedLimit, @MapVersion, @PublisherId)";

        var parameters = new
        {
            decoded.PacketId,
            decoded.GenerationTime,
            decoded.StationId,
            decoded.IntersectionId,
            decoded.IntersectionName,
            decoded.Latitude,
            decoded.Longitude,
            decoded.LaneCount,
            decoded.RoadWidth,
            decoded.SpeedLimit,
            MapVersion = string.IsNullOrWhiteSpace(decoded.MapVersion) ? "1.0" : decoded.MapVersion,
            decoded.PublisherId
        };

        await connection.ExecuteAsync(query, parameters, transaction);
    }

    private async Task StoreSPATEMAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        var decoded = _v2xMessageDecoder.DecodeSPATEM(packet);

        var query = @"
            INSERT INTO spatem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                current_phase, phase_state, connection_maneuver_assist_id,
                phase0_signal_group, phase1_signal_group, phase2_signal_group,
                phase3_signal_group, phase4_signal_group, phase5_signal_group,
                phase0_event_state, phase1_event_state, phase2_event_state,
                phase3_event_state, phase4_event_state, phase5_event_state,
                phase0_connection_maneuver_assist_id0, phase0_connection_maneuver_assist_id1,
                phase1_connection_maneuver_assist_id0, phase1_connection_maneuver_assist_id1,
                phase2_connection_maneuver_assist_id0, phase2_connection_maneuver_assist_id1,
                phase3_connection_maneuver_assist_id0, phase3_connection_maneuver_assist_id1,
                phase4_connection_maneuver_assist_id0, phase4_connection_maneuver_assist_id1,
                phase5_connection_maneuver_assist_id0, phase5_connection_maneuver_assist_id1,
                publisher_id)
            VALUES (@PacketId, @GenerationTime, @StationId,
                @IntersectionId, @IntersectionName, @Latitude, @Longitude,
                @CurrentPhase, @PhaseState, @ConnectionManeuverAssistId,
                @Phase0SignalGroup, @Phase1SignalGroup, @Phase2SignalGroup,
                @Phase3SignalGroup, @Phase4SignalGroup, @Phase5SignalGroup,
                @Phase0EventState, @Phase1EventState, @Phase2EventState,
                @Phase3EventState, @Phase4EventState, @Phase5EventState,
                @Phase0ConnectionManeuverAssistId0, @Phase0ConnectionManeuverAssistId1,
                @Phase1ConnectionManeuverAssistId0, @Phase1ConnectionManeuverAssistId1,
                @Phase2ConnectionManeuverAssistId0, @Phase2ConnectionManeuverAssistId1,
                @Phase3ConnectionManeuverAssistId0, @Phase3ConnectionManeuverAssistId1,
                @Phase4ConnectionManeuverAssistId0, @Phase4ConnectionManeuverAssistId1,
                @Phase5ConnectionManeuverAssistId0, @Phase5ConnectionManeuverAssistId1,
                @PublisherId)";

        var parameters = new
        {
            decoded.PacketId,
            decoded.GenerationTime,
            decoded.StationId,
            decoded.IntersectionId,
            decoded.IntersectionName,
            decoded.Latitude,
            decoded.Longitude,
            decoded.CurrentPhase,
            decoded.ConnectionManeuverAssistId,
            decoded.Phase0SignalGroup,
            decoded.Phase1SignalGroup,
            decoded.Phase2SignalGroup,
            decoded.Phase3SignalGroup,
            decoded.Phase4SignalGroup,
            decoded.Phase5SignalGroup,
            decoded.Phase0EventState,
            decoded.Phase1EventState,
            decoded.Phase2EventState,
            decoded.Phase3EventState,
            decoded.Phase4EventState,
            decoded.Phase5EventState,
            decoded.Phase0ConnectionManeuverAssistId0,
            decoded.Phase0ConnectionManeuverAssistId1,
            decoded.Phase1ConnectionManeuverAssistId0,
            decoded.Phase1ConnectionManeuverAssistId1,
            decoded.Phase2ConnectionManeuverAssistId0,
            decoded.Phase2ConnectionManeuverAssistId1,
            decoded.Phase3ConnectionManeuverAssistId0,
            decoded.Phase3ConnectionManeuverAssistId1,
            decoded.Phase4ConnectionManeuverAssistId0,
            decoded.Phase4ConnectionManeuverAssistId1,
            decoded.Phase5ConnectionManeuverAssistId0,
            decoded.Phase5ConnectionManeuverAssistId1,
            PhaseState = string.IsNullOrWhiteSpace(decoded.PhaseState) ? "unknown" : decoded.PhaseState,
            decoded.PublisherId
        };

        await connection.ExecuteAsync(query, parameters, transaction);
    }

    private async Task StoreSREMAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        var decoded = _v2xMessageDecoder.DecodeSREM(packet);

        var query = @"
            INSERT INTO srem_messages (packet_id, generation_time, station_id,
                intersection_name, intersection_id, latitude, longitude,
                requested_phase, vehicle_type, request_reason,
                request_id, requestor_id, required_accuracy,
                in_bound_lane_id, out_bound_lane_id, heading, speed, transmission_power,
                route_names, transit_schedule, requestor_name)
            VALUES (@PacketId, @GenerationTime, @StationId,
                @IntersectionName, @IntersectionId, @Latitude, @Longitude,
                @RequestedPhase, @VehicleType, @RequestReason,
                @RequestId, @RequestorId, @RequiredAccuracy,
                @InBoundLaneId, @OutBoundLaneId, @Heading, @Speed, @TransmissionPower,
                @RouteNames, @TransitSchedule, @RequestorName)";

        var parameters = new
        {
            decoded.PacketId,
            decoded.GenerationTime,
            decoded.StationId,
            decoded.IntersectionName,
            decoded.IntersectionId,
            decoded.Latitude,
            decoded.Longitude,
            decoded.RequestedPhase,
            decoded.VehicleType,
            decoded.RequestReason,
            decoded.RequestId,
            decoded.RequestorId,
            decoded.RequiredAccuracy,
            decoded.InBoundLaneId,
            decoded.OutBoundLaneId,
            decoded.Heading,
            decoded.Speed,
            decoded.TransmissionPower,
            decoded.routeNames,
            decoded.transitSchedule,
            decoded.RequestorName
        };

        await connection.ExecuteAsync(query, parameters, transaction);
    }

    private async Task StoreSSEMAsync(IDbConnection connection, IDbTransaction transaction, Packet packet)
    {
        var decoded = _v2xMessageDecoder.DecodeSSEM(packet);

        var query = @"
            INSERT INTO ssem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                status_code, granted_duration, request_id_ref, responder_id, request_station_id_ref)
            VALUES (@PacketId, @GenerationTime, @StationId,
                @IntersectionId, @IntersectionName, @Latitude, @Longitude,
                @StatusCode, @GrantedDuration, @RequestIdRef, @ResponderId, @RequestStationIdRef)";

        var parameters = new
        {
            decoded.PacketId,
            decoded.GenerationTime,
            decoded.StationId,
            decoded.IntersectionName,
            decoded.Latitude,
            decoded.Longitude,
            decoded.IntersectionId,
            StatusCode = string.IsNullOrWhiteSpace(decoded.StatusCode) ? "pending" : decoded.StatusCode,
            decoded.GrantedDuration,
            decoded.RequestIdRef,
            decoded.RequestStationIdRef,
            decoded.ResponderId
        };

        await connection.ExecuteAsync(query, parameters, transaction);
    }

    private async Task PopulateIntersectionMetadataForFileAsync(string fileName)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        await UpdateSpatemIntersectionForFileAsync(connection, fileName);
        await UpdateSremIntersectionForFileAsync(connection, fileName);
        await UpdateSsemIntersectionForFileAsync(connection, fileName);
    }

    private static Task UpdateSpatemIntersectionForFileAsync(IDbConnection connection, string fileName)
    {
        const string query = @"
            UPDATE spatem_messages s
            SET intersection_name = COALESCE((
                    SELECT m.intersection_name
                    FROM mapem_messages m
                    WHERE m.intersection_id = s.intersection_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - s.generation_time))) ASC
                    LIMIT 1
                ), s.intersection_name),
                latitude = COALESCE((
                    SELECT m.latitude
                    FROM mapem_messages m
                    WHERE m.intersection_id = s.intersection_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - s.generation_time))) ASC
                    LIMIT 1
                ), s.latitude),
                longitude = COALESCE((
                    SELECT m.longitude
                    FROM mapem_messages m
                    WHERE m.intersection_id = s.intersection_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - s.generation_time))) ASC
                    LIMIT 1
                ), s.longitude)
            FROM packets p
            WHERE s.packet_id = p.id
              AND p.pcap_file_name = @FileName";

        return connection.ExecuteAsync(query, new { FileName = fileName });
    }

    private static Task UpdateSremIntersectionForFileAsync(IDbConnection connection, string fileName)
    {
        const string query = @"
            UPDATE srem_messages s
            SET intersection_name = COALESCE((
                    SELECT ss.intersection_name
                    FROM ssem_messages ss
                    WHERE ss.request_station_id_ref = s.requestor_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (ss.generation_time - s.generation_time))) ASC
                    LIMIT 1
                ), s.intersection_name),
                intersection_id = COALESCE((
                    SELECT ss.intersection_id
                    FROM ssem_messages ss
                    WHERE ss.request_station_id_ref = s.requestor_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (ss.generation_time - s.generation_time))) ASC
                    LIMIT 1
                ), s.intersection_id)
            FROM packets p
            WHERE s.packet_id = p.id
              AND p.pcap_file_name = @FileName";

        return connection.ExecuteAsync(query, new { FileName = fileName });
    }

    private static Task UpdateSsemIntersectionForFileAsync(IDbConnection connection, string fileName)
    {
        const string query = @"
            UPDATE ssem_messages ss
            SET intersection_name = COALESCE((
                    SELECT m.intersection_name
                    FROM mapem_messages m
                    WHERE m.intersection_id = ss.intersection_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - ss.generation_time))) ASC
                    LIMIT 1
                ), ss.intersection_name),
                latitude = COALESCE((
                    SELECT m.latitude
                    FROM mapem_messages m
                    WHERE m.intersection_id = ss.intersection_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - ss.generation_time))) ASC
                    LIMIT 1
                ), ss.latitude),
                longitude = COALESCE((
                    SELECT m.longitude
                    FROM mapem_messages m
                    WHERE m.intersection_id = ss.intersection_id
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - ss.generation_time))) ASC
                    LIMIT 1
                ), ss.longitude)
            FROM packets p
            WHERE ss.packet_id = p.id
              AND p.pcap_file_name = @FileName";

        return connection.ExecuteAsync(query, new { FileName = fileName });
    }

    /// <summary>
    /// Records OBU-RSU correlations by matching SREM requests to SSEM responses.
    /// Uses strict matching (requestId == requestIdRef) first, then fallback matching (intersectionId + time window).
    /// </summary>
    public async Task RecordOBUToRSUCorrelationAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);

            // Query all SREM messages that can be matched to SSEM responses.
            var sremsQuery = @"
                SELECT s.id, s.request_id, s.requestor_id, s.station_id, s.intersection_id, s.intersection_name, s.generation_time
                FROM srem_messages s
                WHERE s.generation_time IS NOT NULL
                ORDER BY s.generation_time ASC";

            var srems = await connection.QueryAsync<dynamic>(sremsQuery);

            // Symmetric time window for strict/fallback matching (+/-10s).
            const int TIME_WINDOW_MS = 10000;

            int totalSrems = 0;
            int strictMatches = 0;
            int fallbackMatchesByIntersectionId = 0;
            int fallbackMatchesByIntersectionName = 0;
            int fallbackMatchesByStationRef = 0;
            int skippedMissingRequestId = 0;
            int skippedMissingIntersection = 0;
            int duplicatePairsSkipped = 0;

            foreach (var srem in srems)
            {
                totalSrems++;

                string? sremRequestId = srem.request_id;
                string? sremRequestorId = srem.requestor_id;
                string? sremStationId = srem.station_id;
                int? sremIntersectionId = srem.intersection_id;
                string? sremIntersectionName = srem.intersection_name;
                DateTime sremTime = srem.generation_time;
                int sremId = srem.id;

                var startTime = sremTime.AddMilliseconds(-TIME_WINDOW_MS);
                var endTime = sremTime.AddMilliseconds(TIME_WINDOW_MS);

                // Strategy 1: Strict match using requestId
                if (!string.IsNullOrWhiteSpace(sremRequestId))
                {
                    var ssemQuery = @"
                        SELECT id, responder_id, granted_duration, status_code, generation_time
                        FROM ssem_messages
                        WHERE request_id_ref = @RequestId
                        AND generation_time BETWEEN @StartTime AND @EndTime
                        ORDER BY ABS(EXTRACT(EPOCH FROM (generation_time - @SremTime)))
                        LIMIT 1";

                    var ssem = await connection.QueryFirstOrDefaultAsync<dynamic>(ssemQuery, new
                    {
                        RequestId = sremRequestId,
                        StartTime = startTime,
                        EndTime = endTime,
                        SremTime = sremTime
                    });

                    if (ssem != null && ssem.generation_time != null)
                    {
#pragma warning disable CS8602 // Dereference of possibly null reference - safe due to null check above
                        int ssemId = ssem.id;
                        string? responderId = (string?)ssem.responder_id;
                        int grantedDuration = (int?)ssem.granted_duration ?? 0;
                        string statusCode = (string?)ssem.status_code ?? "unknown";
#pragma warning restore CS8602
                        DateTime ssemTime = (DateTime)ssem.generation_time;
                        int timeDelta = (int)((ssemTime - sremTime).TotalMilliseconds);

                        if (await CorrelationExistsAsync(connection, sremId, ssemId))
                        {
                            duplicatePairsSkipped++;
                            continue;
                        }

                        await RecordCorrelationAsync(connection, sremId, ssemId, null, null,
                            sremRequestorId, responderId, sremRequestId, "strict",
                            1.0, sremTime, ssemTime, timeDelta,
                            "signal_request", statusCode, grantedDuration);

                        strictMatches++;
                        continue;
                    }
                }
                else
                {
                    skippedMissingRequestId++;
                }

                // Strategy 2a: Fallback match by intersectionId + symmetric time window.
                if (sremIntersectionId.HasValue && sremIntersectionId > 0)
                {
                    var fallbackSsemQuery = @"
                        SELECT id, responder_id, granted_duration, status_code, generation_time
                        FROM ssem_messages
                        WHERE intersection_id = @IntersectionId
                        AND generation_time BETWEEN @StartTime AND @EndTime
                        ORDER BY ABS(EXTRACT(EPOCH FROM (generation_time - @SremTime)))
                        LIMIT 1";

                    var fallbackSsem = await connection.QueryFirstOrDefaultAsync<dynamic>(fallbackSsemQuery, new
                    {
                        IntersectionId = sremIntersectionId,
                        StartTime = startTime,
                        EndTime = endTime,
                        SremTime = sremTime
                    });

                    if (fallbackSsem != null && fallbackSsem.generation_time != null)
                    {
#pragma warning disable CS8602 // Dereference of possibly null reference - safe due to null check above
                        int ssemId = fallbackSsem.id;
                        string? responderId = (string?)fallbackSsem.responder_id;
                        int grantedDuration = (int?)fallbackSsem.granted_duration ?? 0;
                        string statusCode = (string?)fallbackSsem.status_code ?? "unknown";
#pragma warning restore CS8602
                        DateTime ssemTime = (DateTime)fallbackSsem.generation_time;
                        int timeDelta = (int)((ssemTime - sremTime).TotalMilliseconds);

                        if (await CorrelationExistsAsync(connection, sremId, ssemId))
                        {
                            duplicatePairsSkipped++;
                            continue;
                        }

                        // Fallback confidence is lower (0.7) than strict match (1.0)
                        await RecordCorrelationAsync(connection, sremId, ssemId, null, null,
                            sremRequestorId, responderId, sremRequestId, "fallback",
                            0.7, sremTime, ssemTime, timeDelta,
                            "signal_request", statusCode, grantedDuration);

                        fallbackMatchesByIntersectionId++;
                        continue;
                    }
                }

                // Strategy 2b: Fallback by intersection name if intersectionId is missing.
                if (!string.IsNullOrWhiteSpace(sremIntersectionName))
                {
                    var fallbackByNameSsemQuery = @"
                        SELECT id, responder_id, granted_duration, status_code, generation_time
                        FROM ssem_messages
                        WHERE intersection_name IS NOT NULL
                        AND LOWER(TRIM(intersection_name)) = LOWER(TRIM(@IntersectionName))
                        AND generation_time BETWEEN @StartTime AND @EndTime
                        ORDER BY ABS(EXTRACT(EPOCH FROM (generation_time - @SremTime)))
                        LIMIT 1";

                    var fallbackByNameSsem = await connection.QueryFirstOrDefaultAsync<dynamic>(fallbackByNameSsemQuery, new
                    {
                        IntersectionName = sremIntersectionName,
                        StartTime = startTime,
                        EndTime = endTime,
                        SremTime = sremTime
                    });

                    if (fallbackByNameSsem != null && fallbackByNameSsem.generation_time != null)
                    {
#pragma warning disable CS8602 // Dereference of possibly null reference - safe due to null check above
                        int ssemId = fallbackByNameSsem.id;
                        string? responderId = (string?)fallbackByNameSsem.responder_id;
                        int grantedDuration = (int?)fallbackByNameSsem.granted_duration ?? 0;
                        string statusCode = (string?)fallbackByNameSsem.status_code ?? "unknown";
#pragma warning restore CS8602
                        DateTime ssemTime = (DateTime)fallbackByNameSsem.generation_time;
                        int timeDelta = (int)((ssemTime - sremTime).TotalMilliseconds);

                        if (await CorrelationExistsAsync(connection, sremId, ssemId))
                        {
                            duplicatePairsSkipped++;
                            continue;
                        }

                        await RecordCorrelationAsync(connection, sremId, ssemId, null, null,
                            sremRequestorId, responderId, sremRequestId, "fallback",
                            0.6, sremTime, ssemTime, timeDelta,
                            "signal_request", statusCode, grantedDuration);

                        fallbackMatchesByIntersectionName++;
                        continue;
                    }
                }

                // Strategy 2c: Fallback by OBU station id vs. SSEM request station id reference.
                if (!string.IsNullOrWhiteSpace(sremStationId))
                {
                    var fallbackByStationQuery = @"
                        SELECT id, responder_id, granted_duration, status_code, generation_time
                        FROM ssem_messages
                        WHERE request_station_id_ref = @StationId
                        AND generation_time BETWEEN @StartTime AND @EndTime
                        ORDER BY ABS(EXTRACT(EPOCH FROM (generation_time - @SremTime)))
                        LIMIT 1";

                    var fallbackByStationSsem = await connection.QueryFirstOrDefaultAsync<dynamic>(fallbackByStationQuery, new
                    {
                        StationId = sremStationId,
                        StartTime = startTime,
                        EndTime = endTime,
                        SremTime = sremTime
                    });

                    if (fallbackByStationSsem != null && fallbackByStationSsem.generation_time != null)
                    {
#pragma warning disable CS8602 // Dereference of possibly null reference - safe due to null check above
                        int ssemId = fallbackByStationSsem.id;
                        string? responderId = (string?)fallbackByStationSsem.responder_id;
                        int grantedDuration = (int?)fallbackByStationSsem.granted_duration ?? 0;
                        string statusCode = (string?)fallbackByStationSsem.status_code ?? "unknown";
#pragma warning restore CS8602
                        DateTime ssemTime = (DateTime)fallbackByStationSsem.generation_time;
                        int timeDelta = (int)((ssemTime - sremTime).TotalMilliseconds);

                        if (await CorrelationExistsAsync(connection, sremId, ssemId))
                        {
                            duplicatePairsSkipped++;
                            continue;
                        }

                        await RecordCorrelationAsync(connection, sremId, ssemId, null, null,
                            sremRequestorId, responderId, sremRequestId, "fallback",
                            0.5, sremTime, ssemTime, timeDelta,
                            "signal_request", statusCode, grantedDuration);

                        fallbackMatchesByStationRef++;
                        continue;
                    }
                }

                if (!sremIntersectionId.HasValue || sremIntersectionId <= 0)
                {
                    skippedMissingIntersection++;
                }
            }

            Console.WriteLine(
                $"Correlation summary: scanned={totalSrems}, strict={strictMatches}, " +
                $"fallbackByIntersectionId={fallbackMatchesByIntersectionId}, " +
                $"fallbackByIntersectionName={fallbackMatchesByIntersectionName}, " +
                $"fallbackByStationRef={fallbackMatchesByStationRef}, " +
                $"missingRequestId={skippedMissingRequestId}, missingIntersection={skippedMissingIntersection}, " +
                $"duplicatesSkipped={duplicatePairsSkipped}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording OBU-RSU correlations: {ex.Message}");
            throw;
        }
    }

    private static async Task<bool> CorrelationExistsAsync(IDbConnection connection, int sremId, int ssemId)
    {
        const string existsQuery = @"
            SELECT 1
            FROM obu_rsu_correlations
            WHERE srem_id = @SremId AND ssem_id = @SsemId
            LIMIT 1";

        var exists = await connection.QueryFirstOrDefaultAsync<int?>(existsQuery, new
        {
            SremId = sremId,
            SsemId = ssemId
        });

        return exists.HasValue;
    }

    private async Task RecordCorrelationAsync(
        IDbConnection connection,
        int sremId,
        int ssemId,
        int? mapemId,
        int? spatemId,
        string? obuStationId,
        string? rsuIntersectionId,
        string? requestId,
        string correlationType,
        double matchConfidence,
        DateTime sremTimestamp,
        DateTime ssemTimestamp,
        int timeDeltaMs,
        string requestType,
        string statusCode,
        int grantedDuration)
    {
        var query = @"
            INSERT INTO obu_rsu_correlations (srem_id, ssem_id, mapem_id, spatem_id,
                obu_station_id, rsu_intersection_id, request_id, correlation_type,
                match_confidence, srem_timestamp, ssem_timestamp, time_delta_ms,
                request_type, status_code, granted_duration)
            VALUES (@SremId, @SsemId, @MapemId, @SpaTemId,
                @ObuStationId, @RsuIntersectionId, @RequestId, @CorrelationType,
                @MatchConfidence, @SremTimestamp, @SsemTimestamp, @TimeDeltaMs,
                @RequestType, @StatusCode, @GrantedDuration)";

        await connection.ExecuteAsync(query, new
        {
            SremId = sremId,
            SsemId = ssemId,
            MapemId = mapemId,
            SpaTemId = spatemId,
            ObuStationId = obuStationId,
            RsuIntersectionId = rsuIntersectionId,
            RequestId = requestId,
            CorrelationType = correlationType,
            MatchConfidence = matchConfidence,
            SremTimestamp = sremTimestamp,
            SsemTimestamp = ssemTimestamp,
            TimeDeltaMs = timeDeltaMs,
            RequestType = requestType,
            StatusCode = statusCode,
            GrantedDuration = grantedDuration
        });
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
        int? limit = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var query = new System.Text.StringBuilder(@"
            SELECT c.id AS Id,
                c.srem_id AS SremId,
                c.ssem_id AS SsemId,
                c.obu_station_id AS ObuStationId,
                c.rsu_intersection_id AS RsuIntersectionId,
                c.request_id AS RequestId,
                c.correlation_type AS CorrelationType,
                c.match_confidence AS MatchConfidence,
                c.srem_timestamp AS SremTimestamp,
                c.ssem_timestamp AS SsemTimestamp,
                c.time_delta_ms AS TimeDeltaMs,
                c.request_type AS RequestType,
                c.status_code AS StatusCode,
                c.granted_duration AS GrantedDuration,
                s.latitude AS SremLatitude,
                s.longitude AS SremLongitude,
                ss.latitude AS SsemLatitude,
                ss.longitude AS SsemLongitude
            FROM obu_rsu_correlations c
            LEFT JOIN srem_messages s ON c.srem_id = s.id
            LEFT JOIN ssem_messages ss ON c.ssem_id = ss.id
            WHERE 1=1");

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(obuStationId))
        {
            query.Append(" AND c.obu_station_id = @ObuStationId");
            parameters.Add("@ObuStationId", obuStationId);
        }

        if (!string.IsNullOrWhiteSpace(rsuIntersectionId))
        {
            query.Append(" AND c.rsu_intersection_id = @RsuIntersectionId");
            parameters.Add("@RsuIntersectionId", rsuIntersectionId);
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            query.Append(" AND c.request_id = @RequestId");
            parameters.Add("@RequestId", requestId);
        }

        if (!string.IsNullOrWhiteSpace(correlationType))
        {
            query.Append(" AND c.correlation_type = @CorrelationType");
            parameters.Add("@CorrelationType", correlationType);
        }

        if (fromTime.HasValue)
        {
            query.Append(" AND c.srem_timestamp >= @FromTime");
            parameters.Add("@FromTime", fromTime.Value);
        }

        if (toTime.HasValue)
        {
            query.Append(" AND c.srem_timestamp <= @ToTime");
            parameters.Add("@ToTime", toTime.Value);
        }

        query.Append(" ORDER BY c.srem_timestamp DESC");

        if (limit.HasValue)
        {
            query.Append(" LIMIT @Limit");
            parameters.Add("@Limit", limit.Value);
        }
        else
        {
            query.Append(" LIMIT 100"); // Default limit
        }

        var correlations = await connection.QueryAsync<CorrelationDto>(query.ToString(), parameters);
        return correlations.ToList();
    }

    public async Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(
        string? obuStationId = null,
        string? rsuIntersectionId = null,
        string? requestId = null,
        string? correlationType = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var (safePageNumber, safePageSize, offset) = NormalizePaging(pageNumber, pageSize, 250);

        var baseFrom = new System.Text.StringBuilder(@"
            FROM obu_rsu_correlations c
            LEFT JOIN srem_messages s ON c.srem_id = s.id
            LEFT JOIN ssem_messages ss ON c.ssem_id = ss.id
            WHERE 1=1");

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(obuStationId))
        {
            baseFrom.Append(" AND c.obu_station_id = @ObuStationId");
            parameters.Add("@ObuStationId", obuStationId);
        }

        if (!string.IsNullOrWhiteSpace(rsuIntersectionId))
        {
            baseFrom.Append(" AND c.rsu_intersection_id = @RsuIntersectionId");
            parameters.Add("@RsuIntersectionId", rsuIntersectionId);
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            baseFrom.Append(" AND c.request_id = @RequestId");
            parameters.Add("@RequestId", requestId);
        }

        if (!string.IsNullOrWhiteSpace(correlationType))
        {
            baseFrom.Append(" AND c.correlation_type = @CorrelationType");
            parameters.Add("@CorrelationType", correlationType);
        }

        if (fromTime.HasValue)
        {
            baseFrom.Append(" AND c.srem_timestamp >= @FromTime");
            parameters.Add("@FromTime", fromTime.Value);
        }

        if (toTime.HasValue)
        {
            baseFrom.Append(" AND c.srem_timestamp <= @ToTime");
            parameters.Add("@ToTime", toTime.Value);
        }

        var countQuery = $"SELECT COUNT(*) {baseFrom}";
        var totalCount = await connection.ExecuteScalarAsync<int>(countQuery, parameters);

        parameters.Add("@Limit", safePageSize);
        parameters.Add("@Offset", offset);

        var dataQuery = $@"
            SELECT c.id AS Id,
                c.srem_id AS SremId,
                c.ssem_id AS SsemId,
                c.obu_station_id AS ObuStationId,
                c.rsu_intersection_id AS RsuIntersectionId,
                c.request_id AS RequestId,
                c.correlation_type AS CorrelationType,
                c.match_confidence AS MatchConfidence,
                c.srem_timestamp AS SremTimestamp,
                c.ssem_timestamp AS SsemTimestamp,
                c.time_delta_ms AS TimeDeltaMs,
                c.request_type AS RequestType,
                c.status_code AS StatusCode,
                c.granted_duration AS GrantedDuration,
                s.latitude AS SremLatitude,
                s.longitude AS SremLongitude,
                ss.latitude AS SsemLatitude,
                ss.longitude AS SsemLongitude
            {baseFrom}
            ORDER BY c.srem_timestamp DESC
            LIMIT @Limit OFFSET @Offset";

        var items = (await connection.QueryAsync<CorrelationDto>(dataQuery, parameters)).ToList();

        return new PagedResult<CorrelationDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = safePageNumber,
            PageSize = safePageSize
        };
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
        using var connection = new NpgsqlConnection(_connectionString);

        var query = @"
            SELECT c.id AS Id,
                c.ssem_id AS SsemId,
                c.request_id AS RequestId,
                c.correlation_type AS CorrelationType,
                c.match_confidence AS MatchConfidence,
                ss.id AS SsemMessageId,
                ss.status_code AS StatusCode,
                ss.granted_duration AS GrantedDuration,
                ss.generation_time AS GenerationTime,
                ss.latitude AS Latitude,
                ss.longitude AS Longitude
            FROM obu_rsu_correlations c
            LEFT JOIN ssem_messages ss ON c.ssem_id = ss.id
            WHERE c.srem_id = @SremId
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<SremSsemMatchDto>(query, new { SremId = sremId });
    }

    /// <summary>
    /// Get map entities (OBU positions from CAM, RSU intersections from MAPEM/SPATEM).
    /// </summary>
    public async Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var entities = new List<MapEntityDto>();

        // Get latest OBU positions from CAM messages
        var obuQuery = @"
            SELECT DISTINCT ON (station_id)
                'OBU' AS EntityType,
                'CAM' AS MessageType,
                station_id AS StationId,
                NULL::INTEGER AS IntersectionId,
                NULL::VARCHAR AS IntersectionName,
                NULL::VARCHAR AS PublisherId,
                latitude AS Latitude,
                longitude AS Longitude,
                speed AS Speed,
                heading AS Heading,
                generation_time AS GenerationTime,
                vehicle_role AS VehicleRole,
                station_type AS StationType
            FROM cam_messages
                        WHERE latitude IS NOT NULL
                            AND longitude IS NOT NULL
                            AND latitude BETWEEN -90 AND 90
                            AND longitude BETWEEN -180 AND 180
                            AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue || toTime.HasValue)
        {
            obuQuery += " AND generation_time BETWEEN @FromTime AND @ToTime";
        }

        obuQuery += " ORDER BY station_id, generation_time DESC LIMIT 1000";

        var obuParameters = new DynamicParameters();
        if (fromTime.HasValue) obuParameters.Add("@FromTime", fromTime.Value);
        if (toTime.HasValue) obuParameters.Add("@ToTime", toTime.Value);

        var obuEntities = await connection.QueryAsync<MapEntityDto>(obuQuery, obuParameters);
        entities.AddRange(obuEntities);

        // Get RSU positions from MAPEM messages
        var rsuMapemQuery = @"
            SELECT
                'RSU' AS EntityType,
                'MAPEM' AS MessageType,
                station_id AS StationId,
                intersection_id AS IntersectionId,
                intersection_name AS IntersectionName,
                publisher_id AS PublisherId,
                latitude AS Latitude,
                longitude AS Longitude,
                NULL::DOUBLE PRECISION AS Speed,
                NULL::DOUBLE PRECISION AS Heading,
                generation_time AS GenerationTime,
                NULL::VARCHAR AS VehicleRole,
                NULL::INTEGER AS StationType
            FROM mapem_messages
                        WHERE latitude IS NOT NULL
                            AND longitude IS NOT NULL
                            AND latitude BETWEEN -90 AND 90
                            AND longitude BETWEEN -180 AND 180
                            AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue || toTime.HasValue)
        {
            rsuMapemQuery += " AND generation_time BETWEEN @FromTime AND @ToTime";
        }

        rsuMapemQuery += " ORDER BY generation_time DESC LIMIT 500";

        var rsuParameters = new DynamicParameters();
        if (fromTime.HasValue) rsuParameters.Add("@FromTime", fromTime.Value);
        if (toTime.HasValue) rsuParameters.Add("@ToTime", toTime.Value);

        var rsuMapemEntities = await connection.QueryAsync<MapEntityDto>(rsuMapemQuery, rsuParameters);
        entities.AddRange(rsuMapemEntities);

        var rsuSpatemQuery = @"
            SELECT
                'RSU' AS EntityType,
                'SPATEM' AS MessageType,
                s.station_id AS StationId,
                s.intersection_id AS IntersectionId,
                s.intersection_name AS IntersectionName,
                s.publisher_id AS PublisherId,
                CASE
                    WHEN s.latitude = 0 AND s.longitude = 0 THEN COALESCE(mapem_ref.latitude, s.latitude)
                    ELSE s.latitude
                END AS Latitude,
                CASE
                    WHEN s.latitude = 0 AND s.longitude = 0 THEN COALESCE(mapem_ref.longitude, s.longitude)
                    ELSE s.longitude
                END AS Longitude,
                NULL::DOUBLE PRECISION AS Speed,
                NULL::DOUBLE PRECISION AS Heading,
                s.generation_time AS GenerationTime,
                NULL::VARCHAR AS VehicleRole,
                NULL::INTEGER AS StationType
            FROM spatem_messages s
            LEFT JOIN LATERAL (
                SELECT m.latitude, m.longitude
                FROM mapem_messages m
                WHERE m.latitude IS NOT NULL
                  AND m.longitude IS NOT NULL
                  AND m.latitude BETWEEN -90 AND 90
                  AND m.longitude BETWEEN -180 AND 180
                  AND NOT (m.latitude = 0 AND m.longitude = 0)
                  AND (
                      (s.intersection_id IS NOT NULL AND s.intersection_id <> 0 AND m.intersection_id = s.intersection_id)
                      OR (NULLIF(s.publisher_id, '') IS NOT NULL AND m.publisher_id = s.publisher_id)
                      OR (NULLIF(s.station_id, '') IS NOT NULL AND m.station_id = s.station_id)
                  )
                ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - s.generation_time))) ASC
                LIMIT 1
            ) mapem_ref ON TRUE
            WHERE (
                (s.latitude IS NOT NULL
                  AND s.longitude IS NOT NULL
                  AND s.latitude BETWEEN -90 AND 90
                  AND s.longitude BETWEEN -180 AND 180
                  AND NOT (s.latitude = 0 AND s.longitude = 0))
                OR (mapem_ref.latitude IS NOT NULL
                  AND mapem_ref.longitude IS NOT NULL)
            )";

        if (fromTime.HasValue || toTime.HasValue)
        {
            rsuSpatemQuery += " AND generation_time BETWEEN @FromTime AND @ToTime";
        }

        rsuSpatemQuery += " ORDER BY generation_time DESC LIMIT 500";

        var rsuSpatemEntities = await connection.QueryAsync<MapEntityDto>(rsuSpatemQuery, rsuParameters);
        entities.AddRange(rsuSpatemEntities);

        // Get OBU request positions from SREM messages.
        var obuSremQuery = @"
            SELECT
                'OBU' AS EntityType,
                'SREM' AS MessageType,
                station_id AS StationId,
                intersection_id AS IntersectionId,
                intersection_name AS IntersectionName,
                NULL::VARCHAR AS PublisherId,
                latitude AS Latitude,
                longitude AS Longitude,
                speed AS Speed,
                heading AS Heading,
                generation_time AS GenerationTime,
                vehicle_type AS VehicleRole,
                NULL::INTEGER AS StationType
            FROM srem_messages
            WHERE latitude IS NOT NULL
              AND longitude IS NOT NULL
              AND latitude BETWEEN -90 AND 90
              AND longitude BETWEEN -180 AND 180
              AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue || toTime.HasValue)
        {
            obuSremQuery += " AND generation_time BETWEEN @FromTime AND @ToTime";
        }

        obuSremQuery += " ORDER BY generation_time DESC LIMIT 1000";

        var obuSremEntities = await connection.QueryAsync<MapEntityDto>(obuSremQuery, rsuParameters);
        entities.AddRange(obuSremEntities);

        // Get hazard/event positions from DENM messages.
        var denmQuery = @"
            SELECT
                'EVENT' AS EntityType,
                'DENM' AS MessageType,
                station_id AS StationId,
                NULL::INTEGER AS IntersectionId,
                cause_code AS IntersectionName,
                NULL::VARCHAR AS PublisherId,
                latitude AS Latitude,
                longitude AS Longitude,
                NULL::DOUBLE PRECISION AS Speed,
                NULL::DOUBLE PRECISION AS Heading,
                generation_time AS GenerationTime,
                NULL::VARCHAR AS VehicleRole,
                station_type AS StationType
            FROM denm_messages
            WHERE latitude IS NOT NULL
              AND longitude IS NOT NULL
              AND latitude BETWEEN -90 AND 90
              AND longitude BETWEEN -180 AND 180
              AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue || toTime.HasValue)
        {
            denmQuery += " AND generation_time BETWEEN @FromTime AND @ToTime";
        }

        denmQuery += " ORDER BY generation_time DESC LIMIT 1000";

        var denmEntities = await connection.QueryAsync<MapEntityDto>(denmQuery, rsuParameters);
        entities.AddRange(denmEntities);

        // Get SSEM signal status positions (typically RSU-side status updates).
        var ssemQuery = @"
            SELECT
                'RSU' AS EntityType,
                'SSEM' AS MessageType,
                ss.station_id AS StationId,
                ss.intersection_id AS IntersectionId,
                ss.intersection_name AS IntersectionName,
                ss.responder_id AS PublisherId,
                CASE
                    WHEN ss.latitude = 0 AND ss.longitude = 0 THEN COALESCE(mapem_ref.latitude, ss.latitude)
                    ELSE ss.latitude
                END AS Latitude,
                CASE
                    WHEN ss.latitude = 0 AND ss.longitude = 0 THEN COALESCE(mapem_ref.longitude, ss.longitude)
                    ELSE ss.longitude
                END AS Longitude,
                NULL::DOUBLE PRECISION AS Speed,
                NULL::DOUBLE PRECISION AS Heading,
                ss.generation_time AS GenerationTime,
                ss.status_code AS VehicleRole,
                NULL::INTEGER AS StationType
            FROM ssem_messages ss
            LEFT JOIN LATERAL (
                SELECT m.latitude, m.longitude
                FROM mapem_messages m
                WHERE m.latitude IS NOT NULL
                  AND m.longitude IS NOT NULL
                  AND m.latitude BETWEEN -90 AND 90
                  AND m.longitude BETWEEN -180 AND 180
                  AND NOT (m.latitude = 0 AND m.longitude = 0)
                  AND (
                      (ss.intersection_id IS NOT NULL AND ss.intersection_id <> 0 AND m.intersection_id = ss.intersection_id)
                      OR (NULLIF(ss.responder_id, '') IS NOT NULL AND m.publisher_id = ss.responder_id)
                      OR (NULLIF(ss.station_id, '') IS NOT NULL AND m.station_id = ss.station_id)
                  )
                ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - ss.generation_time))) ASC
                LIMIT 1
            ) mapem_ref ON TRUE
            WHERE (
                (ss.latitude IS NOT NULL
                  AND ss.longitude IS NOT NULL
                  AND ss.latitude BETWEEN -90 AND 90
                  AND ss.longitude BETWEEN -180 AND 180
                  AND NOT (ss.latitude = 0 AND ss.longitude = 0))
                OR (mapem_ref.latitude IS NOT NULL
                  AND mapem_ref.longitude IS NOT NULL)
            )";

        if (fromTime.HasValue || toTime.HasValue)
        {
            ssemQuery += " AND ss.generation_time BETWEEN @FromTime AND @ToTime";
        }

        ssemQuery += " ORDER BY ss.generation_time DESC LIMIT 1000";

        var ssemEntities = await connection.QueryAsync<MapEntityDto>(ssemQuery, rsuParameters);
        entities.AddRange(ssemEntities);

        return entities;
    }
}
