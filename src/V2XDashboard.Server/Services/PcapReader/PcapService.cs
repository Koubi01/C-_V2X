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
    private static readonly Dictionary<int, string> StationTypeNames = new()
    {
        [0] = "Unknown",
        [1] = "Pedestrian",
        [2] = "Cyclist",
        [3] = "Moped",
        [4] = "Motorcycle",
        [5] = "Passenger Car",
        [6] = "Bus",
        [7] = "Light Truck",
        [8] = "Heavy Truck",
        [9] = "Trailer",
        [10] = "Special Vehicle",
        [11] = "Tram",
        [15] = "RSU"
    };

    private static readonly Dictionary<int, string> VehicleRoleNames = new()
    {
        [0] = "0",
        [1] = "Public Transport",
        [2] = "Special Transport",
        [3] = "Dangerous Goods",
        [4] = "Road Work",
        [5] = "Rescue",
        [6] = "Emergency",
        [7] = "Safety Car",
        [8] = "Agriculture",
        [9] = "Commercial",
        [10] = "Military",
        [11] = "Road Operator",
        [12] = "Taxi"
    };

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

        // Group by message type and batch decode + store
        var camPackets = v2xPackets.Where(p => p.PacketType == "CAM").ToList();
        var denmPackets = v2xPackets.Where(p => p.PacketType == "DENM").ToList();
        var mapemPackets = v2xPackets.Where(p => p.PacketType == "MAPEM").ToList();
        var spatemPackets = v2xPackets.Where(p => p.PacketType == "SPATEM").ToList();
        var sremPackets = v2xPackets.Where(p => p.PacketType == "SREM").ToList();
        var ssemPackets = v2xPackets.Where(p => p.PacketType == "SSEM").ToList();

        if (camPackets.Count > 0) await StoreCAMBatchAsync(connection, transaction, camPackets);
        if (denmPackets.Count > 0) await StoreDENMBatchAsync(connection, transaction, denmPackets);
        if (mapemPackets.Count > 0) await StoreMAPEMBatchAsync(connection, transaction, mapemPackets);
        if (spatemPackets.Count > 0) await StoreSPATEMBatchAsync(connection, transaction, spatemPackets);
        if (sremPackets.Count > 0) await StoreSREMBatchAsync(connection, transaction, sremPackets);
        if (ssemPackets.Count > 0) await StoreSSEMBatchAsync(connection, transaction, ssemPackets);
    }

    private async Task StoreCAMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();
            
            var sql = @"INSERT INTO cam_messages (packet_id, generation_time, station_id,
                latitude, longitude, altitude, speed, heading, station_type,
                vehicle_role, acceleration, curvature, yaw_rate,
                lateral_acceleration, vertical_acceleration, decode_status, vehicle_length, vehicle_width)
            VALUES ";
            
            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var decoded = _v2xMessageDecoder.DecodeCAM(chunk[i]);
                var p = i.ToString();
                
                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @lat{p}, @lon{p}, @alt{p}, @spd{p}, @hed{p}, @stype{p}, @vrole{p}, @acc{p}, @cur{p}, @yaw{p}, @lacc{p}, @vacc{p}, @dstat{p}, @vlen{p}, @vwid{p})");
                
                parameters.Add("@pid" + p, decoded.PacketId);
                parameters.Add("@gen" + p, decoded.GenerationTime);
                parameters.Add("@sid" + p, decoded.StationId);
                parameters.Add("@lat" + p, decoded.Latitude);
                parameters.Add("@lon" + p, decoded.Longitude);
                parameters.Add("@alt" + p, decoded.Altitude);
                parameters.Add("@spd" + p, decoded.Speed);
                parameters.Add("@hed" + p, decoded.Heading);
                parameters.Add("@stype" + p, decoded.StationType);
                parameters.Add("@vrole" + p, string.IsNullOrWhiteSpace(decoded.VehicleRole) ? "unknown" : decoded.VehicleRole);
                parameters.Add("@acc" + p, decoded.Acceleration);
                parameters.Add("@cur" + p, decoded.Curvature);
                parameters.Add("@yaw" + p, decoded.YawRate);
                parameters.Add("@lacc" + p, decoded.LateralAcceleration);
                parameters.Add("@vacc" + p, decoded.VerticalAcceleration);
                parameters.Add("@dstat" + p, decoded.DecodeStatus);
                parameters.Add("@vlen" + p, decoded.VehicleLength);
                parameters.Add("@vwid" + p, decoded.VehicleWidth);
            }
            
            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    private async Task StoreDENMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();
            
            var sql = @"INSERT INTO denm_messages (packet_id, generation_time, station_id,
                cause_code, detection_time, reference_time,
                latitude, longitude, altitude,
                relevance_traffic_direction, validity_duration, station_type, awareness_traffic_direction, original_station_type)
            VALUES ";
            
            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var decoded = _v2xMessageDecoder.DecodeDENM(chunk[i]);
                var p = i.ToString();
                
                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @cc{p}, @det{p}, @ref{p}, @lat{p}, @lon{p}, @alt{p}, @rtd{p}, @vd{p}, @st{p}, @atd{p}, @ost{p})");
                
                parameters.Add("@pid" + p, decoded.PacketId);
                parameters.Add("@gen" + p, decoded.GenerationTime);
                parameters.Add("@sid" + p, decoded.StationId);
                parameters.Add("@cc" + p, string.IsNullOrWhiteSpace(decoded.CauseCode) ? "unknown" : decoded.CauseCode);
                parameters.Add("@det" + p, decoded.DetectionTime);
                parameters.Add("@ref" + p, decoded.ReferenceTime);
                parameters.Add("@lat" + p, decoded.Latitude);
                parameters.Add("@lon" + p, decoded.Longitude);
                parameters.Add("@alt" + p, decoded.Altitude);
                parameters.Add("@rtd" + p, decoded.RelevanceTrafficDirection);
                parameters.Add("@vd" + p, decoded.ValidityDuration);
                parameters.Add("@st" + p, decoded.StationType);
                parameters.Add("@atd" + p, decoded.AwarenessTrafficDirection);
                parameters.Add("@ost" + p, decoded.OriginalStationType);
            }
            
            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    private async Task StoreMAPEMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();
            
            var sql = @"INSERT INTO mapem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                lane_count, road_width, speed_limit, map_version, publisher_id)
            VALUES ";
            
            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var decoded = _v2xMessageDecoder.DecodeMAPEM(chunk[i]);
                var p = i.ToString();
                
                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @iid{p}, @in{p}, @lat{p}, @lon{p}, @lc{p}, @rw{p}, @sl{p}, @mv{p}, @pub{p})");
                
                parameters.Add("@pid" + p, decoded.PacketId);
                parameters.Add("@gen" + p, decoded.GenerationTime);
                parameters.Add("@sid" + p, decoded.StationId);
                parameters.Add("@iid" + p, decoded.IntersectionId);
                parameters.Add("@in" + p, decoded.IntersectionName);
                parameters.Add("@lat" + p, decoded.Latitude);
                parameters.Add("@lon" + p, decoded.Longitude);
                parameters.Add("@lc" + p, decoded.LaneCount);
                parameters.Add("@rw" + p, decoded.RoadWidth);
                parameters.Add("@sl" + p, decoded.SpeedLimit);
                parameters.Add("@mv" + p, string.IsNullOrWhiteSpace(decoded.MapVersion) ? "1.0" : decoded.MapVersion);
                parameters.Add("@pub" + p, decoded.PublisherId);
            }
            
            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    private async Task StoreSPATEMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var decodedList = chunk.Select(p => _v2xMessageDecoder.DecodeSPATEM(p)).ToList();

            var sql = new System.Text.StringBuilder();
            sql.AppendLine(@"INSERT INTO spatem_messages (packet_id, generation_time, station_id,
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
                publisher_id)");
            sql.AppendLine("VALUES");

            var parameters = new DynamicParameters();
            for (var i = 0; i < decodedList.Count; i++)
            {
                var decoded = decodedList[i];
                var suffix = "_" + i;

                if (i > 0) sql.AppendLine(",");

                var paramList = new[] {
                    "@PacketId" + suffix,
                    "@GenerationTime" + suffix,
                    "@StationId" + suffix,
                    "@IntersectionId" + suffix,
                    "@IntersectionName" + suffix,
                    "@Latitude" + suffix,
                    "@Longitude" + suffix,
                    "@CurrentPhase" + suffix,
                    "@PhaseState" + suffix,
                    "@ConnectionManeuverAssistId" + suffix,
                    "@Phase0SignalGroup" + suffix,
                    "@Phase1SignalGroup" + suffix,
                    "@Phase2SignalGroup" + suffix,
                    "@Phase3SignalGroup" + suffix,
                    "@Phase4SignalGroup" + suffix,
                    "@Phase5SignalGroup" + suffix,
                    "@Phase0EventState" + suffix,
                    "@Phase1EventState" + suffix,
                    "@Phase2EventState" + suffix,
                    "@Phase3EventState" + suffix,
                    "@Phase4EventState" + suffix,
                    "@Phase5EventState" + suffix,
                    "@Phase0ConnectionManeuverAssistId0" + suffix,
                    "@Phase0ConnectionManeuverAssistId1" + suffix,
                    "@Phase1ConnectionManeuverAssistId0" + suffix,
                    "@Phase1ConnectionManeuverAssistId1" + suffix,
                    "@Phase2ConnectionManeuverAssistId0" + suffix,
                    "@Phase2ConnectionManeuverAssistId1" + suffix,
                    "@Phase3ConnectionManeuverAssistId0" + suffix,
                    "@Phase3ConnectionManeuverAssistId1" + suffix,
                    "@Phase4ConnectionManeuverAssistId0" + suffix,
                    "@Phase4ConnectionManeuverAssistId1" + suffix,
                    "@Phase5ConnectionManeuverAssistId0" + suffix,
                    "@Phase5ConnectionManeuverAssistId1" + suffix,
                    "@PublisherId" + suffix
                };

                sql.Append("(" + string.Join(", ", paramList) + ")");

                parameters.Add("@PacketId" + suffix, decoded.PacketId);
                parameters.Add("@GenerationTime" + suffix, decoded.GenerationTime);
                parameters.Add("@StationId" + suffix, decoded.StationId);
                parameters.Add("@IntersectionId" + suffix, decoded.IntersectionId);
                parameters.Add("@IntersectionName" + suffix, decoded.IntersectionName);
                parameters.Add("@Latitude" + suffix, decoded.Latitude);
                parameters.Add("@Longitude" + suffix, decoded.Longitude);
                parameters.Add("@CurrentPhase" + suffix, decoded.CurrentPhase);
                parameters.Add("@PhaseState" + suffix, string.IsNullOrWhiteSpace(decoded.PhaseState) ? "unknown" : decoded.PhaseState);
                parameters.Add("@ConnectionManeuverAssistId" + suffix, decoded.ConnectionManeuverAssistId);
                parameters.Add("@Phase0SignalGroup" + suffix, decoded.Phase0SignalGroup);
                parameters.Add("@Phase1SignalGroup" + suffix, decoded.Phase1SignalGroup);
                parameters.Add("@Phase2SignalGroup" + suffix, decoded.Phase2SignalGroup);
                parameters.Add("@Phase3SignalGroup" + suffix, decoded.Phase3SignalGroup);
                parameters.Add("@Phase4SignalGroup" + suffix, decoded.Phase4SignalGroup);
                parameters.Add("@Phase5SignalGroup" + suffix, decoded.Phase5SignalGroup);
                parameters.Add("@Phase0EventState" + suffix, decoded.Phase0EventState);
                parameters.Add("@Phase1EventState" + suffix, decoded.Phase1EventState);
                parameters.Add("@Phase2EventState" + suffix, decoded.Phase2EventState);
                parameters.Add("@Phase3EventState" + suffix, decoded.Phase3EventState);
                parameters.Add("@Phase4EventState" + suffix, decoded.Phase4EventState);
                parameters.Add("@Phase5EventState" + suffix, decoded.Phase5EventState);
                parameters.Add("@Phase0ConnectionManeuverAssistId0" + suffix, decoded.Phase0ConnectionManeuverAssistId0);
                parameters.Add("@Phase0ConnectionManeuverAssistId1" + suffix, decoded.Phase0ConnectionManeuverAssistId1);
                parameters.Add("@Phase1ConnectionManeuverAssistId0" + suffix, decoded.Phase1ConnectionManeuverAssistId0);
                parameters.Add("@Phase1ConnectionManeuverAssistId1" + suffix, decoded.Phase1ConnectionManeuverAssistId1);
                parameters.Add("@Phase2ConnectionManeuverAssistId0" + suffix, decoded.Phase2ConnectionManeuverAssistId0);
                parameters.Add("@Phase2ConnectionManeuverAssistId1" + suffix, decoded.Phase2ConnectionManeuverAssistId1);
                parameters.Add("@Phase3ConnectionManeuverAssistId0" + suffix, decoded.Phase3ConnectionManeuverAssistId0);
                parameters.Add("@Phase3ConnectionManeuverAssistId1" + suffix, decoded.Phase3ConnectionManeuverAssistId1);
                parameters.Add("@Phase4ConnectionManeuverAssistId0" + suffix, decoded.Phase4ConnectionManeuverAssistId0);
                parameters.Add("@Phase4ConnectionManeuverAssistId1" + suffix, decoded.Phase4ConnectionManeuverAssistId1);
                parameters.Add("@Phase5ConnectionManeuverAssistId0" + suffix, decoded.Phase5ConnectionManeuverAssistId0);
                parameters.Add("@Phase5ConnectionManeuverAssistId1" + suffix, decoded.Phase5ConnectionManeuverAssistId1);
                parameters.Add("@PublisherId" + suffix, decoded.PublisherId);
            }

            await connection.ExecuteAsync(sql.ToString(), parameters, transaction);
        }
    }

    private async Task StoreSREMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();
            
            var sql = @"INSERT INTO srem_messages (packet_id, generation_time, station_id,
                intersection_name, intersection_id, latitude, longitude,
                requested_phase, vehicle_type, request_reason,
                request_id, requestor_id, required_accuracy,
                in_bound_lane_id, out_bound_lane_id, heading, speed, transmission_power,
                route_names, transit_schedule, requestor_name)
            VALUES ";
            
            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var decoded = _v2xMessageDecoder.DecodeSREM(chunk[i]);
                var p = i.ToString();
                
                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @in{p}, @iid{p}, @lat{p}, @lon{p}, @rp{p}, @vt{p}, @rr{p}, @rid{p}, @reqid{p}, @ra{p}, @ibli{p}, @obli{p}, @h{p}, @s{p}, @tp{p}, @rn{p}, @ts{p}, @rname{p})");
                
                parameters.Add("@pid" + p, decoded.PacketId);
                parameters.Add("@gen" + p, decoded.GenerationTime);
                parameters.Add("@sid" + p, decoded.StationId);
                parameters.Add("@in" + p, decoded.IntersectionName);
                parameters.Add("@iid" + p, decoded.IntersectionId);
                parameters.Add("@lat" + p, decoded.Latitude);
                parameters.Add("@lon" + p, decoded.Longitude);
                parameters.Add("@rp" + p, decoded.RequestedPhase);
                parameters.Add("@vt" + p, decoded.VehicleType);
                parameters.Add("@rr" + p, decoded.RequestReason);
                parameters.Add("@rid" + p, decoded.RequestId);
                parameters.Add("@reqid" + p, decoded.RequestorId);
                parameters.Add("@ra" + p, decoded.RequiredAccuracy);
                parameters.Add("@ibli" + p, decoded.InBoundLaneId);
                parameters.Add("@obli" + p, decoded.OutBoundLaneId);
                parameters.Add("@h" + p, decoded.Heading);
                parameters.Add("@s" + p, decoded.Speed);
                parameters.Add("@tp" + p, decoded.TransmissionPower);
                parameters.Add("@rn" + p, decoded.routeNames);
                parameters.Add("@ts" + p, decoded.transitSchedule);
                parameters.Add("@rname" + p, decoded.RequestorName);
            }
            
            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    private async Task StoreSSEMBatchAsync(IDbConnection connection, IDbTransaction transaction, List<Packet> packets)
    {
        if (packets.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < packets.Count; start += batchSize)
        {
            var chunk = packets.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();
            
            var sql = @"INSERT INTO ssem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                status_code, granted_duration, request_id_ref, responder_id, request_station_id_ref)
            VALUES ";
            
            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var decoded = _v2xMessageDecoder.DecodeSSEM(chunk[i]);
                var p = i.ToString();
                
                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @iid{p}, @in{p}, @lat{p}, @lon{p}, @sc{p}, @gd{p}, @rid{p}, @rid2{p}, @rsid{p})");
                
                parameters.Add("@pid" + p, decoded.PacketId);
                parameters.Add("@gen" + p, decoded.GenerationTime);
                parameters.Add("@sid" + p, decoded.StationId);
                parameters.Add("@iid" + p, decoded.IntersectionId);
                parameters.Add("@in" + p, decoded.IntersectionName);
                parameters.Add("@lat" + p, decoded.Latitude);
                parameters.Add("@lon" + p, decoded.Longitude);
                parameters.Add("@sc" + p, string.IsNullOrWhiteSpace(decoded.StatusCode) ? "pending" : decoded.StatusCode);
                parameters.Add("@gd" + p, decoded.GrantedDuration);
                parameters.Add("@rid" + p, decoded.RequestIdRef);
                parameters.Add("@rid2" + p, decoded.ResponderId);
                parameters.Add("@rsid" + p, decoded.RequestStationIdRef);
            }
            
            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
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

            const int timeWindowSeconds = 10;

            const string correlationQuery = @"
                WITH strict_candidates AS (
                    SELECT
                        s.id AS srem_id,
                        ss.id AS ssem_id,
                        s.requestor_id AS obu_station_id,
                        ss.responder_id AS rsu_intersection_id,
                        s.request_id AS request_id,
                        s.generation_time AS srem_timestamp,
                        ss.generation_time AS ssem_timestamp,
                        (EXTRACT(EPOCH FROM (ss.generation_time - s.generation_time)) * 1000)::INT AS time_delta_ms,
                        COALESCE(ss.status_code, 'unknown') AS status_code,
                        COALESCE(ss.granted_duration, 0) AS granted_duration,
                        'strict'::VARCHAR(20) AS correlation_type,
                        1.0::NUMERIC(3,2) AS match_confidence,
                        1 AS strategy_order
                    FROM srem_messages s
                    JOIN ssem_messages ss
                      ON ss.request_id_ref = s.request_id
                     AND ss.generation_time BETWEEN s.generation_time - (@WindowSeconds * INTERVAL '1 second')
                                               AND s.generation_time + (@WindowSeconds * INTERVAL '1 second')
                    WHERE s.generation_time IS NOT NULL
                      AND NULLIF(TRIM(s.request_id), '') IS NOT NULL
                ),
                fallback_by_intersection_id AS (
                    SELECT
                        s.id AS srem_id,
                        ss.id AS ssem_id,
                        s.requestor_id AS obu_station_id,
                        ss.responder_id AS rsu_intersection_id,
                        s.request_id AS request_id,
                        s.generation_time AS srem_timestamp,
                        ss.generation_time AS ssem_timestamp,
                        (EXTRACT(EPOCH FROM (ss.generation_time - s.generation_time)) * 1000)::INT AS time_delta_ms,
                        COALESCE(ss.status_code, 'unknown') AS status_code,
                        COALESCE(ss.granted_duration, 0) AS granted_duration,
                        'fallback'::VARCHAR(20) AS correlation_type,
                        0.7::NUMERIC(3,2) AS match_confidence,
                        2 AS strategy_order
                    FROM srem_messages s
                    JOIN ssem_messages ss
                      ON ss.intersection_id = s.intersection_id
                     AND ss.generation_time BETWEEN s.generation_time - (@WindowSeconds * INTERVAL '1 second')
                                               AND s.generation_time + (@WindowSeconds * INTERVAL '1 second')
                    WHERE s.generation_time IS NOT NULL
                      AND s.intersection_id IS NOT NULL
                      AND s.intersection_id > 0
                ),
                fallback_by_intersection_name AS (
                    SELECT
                        s.id AS srem_id,
                        ss.id AS ssem_id,
                        s.requestor_id AS obu_station_id,
                        ss.responder_id AS rsu_intersection_id,
                        s.request_id AS request_id,
                        s.generation_time AS srem_timestamp,
                        ss.generation_time AS ssem_timestamp,
                        (EXTRACT(EPOCH FROM (ss.generation_time - s.generation_time)) * 1000)::INT AS time_delta_ms,
                        COALESCE(ss.status_code, 'unknown') AS status_code,
                        COALESCE(ss.granted_duration, 0) AS granted_duration,
                        'fallback'::VARCHAR(20) AS correlation_type,
                        0.6::NUMERIC(3,2) AS match_confidence,
                        3 AS strategy_order
                    FROM srem_messages s
                    JOIN ssem_messages ss
                      ON LOWER(TRIM(ss.intersection_name)) = LOWER(TRIM(s.intersection_name))
                     AND ss.generation_time BETWEEN s.generation_time - (@WindowSeconds * INTERVAL '1 second')
                                               AND s.generation_time + (@WindowSeconds * INTERVAL '1 second')
                    WHERE s.generation_time IS NOT NULL
                      AND NULLIF(TRIM(s.intersection_name), '') IS NOT NULL
                ),
                fallback_by_station_ref AS (
                    SELECT
                        s.id AS srem_id,
                        ss.id AS ssem_id,
                        s.requestor_id AS obu_station_id,
                        ss.responder_id AS rsu_intersection_id,
                        s.request_id AS request_id,
                        s.generation_time AS srem_timestamp,
                        ss.generation_time AS ssem_timestamp,
                        (EXTRACT(EPOCH FROM (ss.generation_time - s.generation_time)) * 1000)::INT AS time_delta_ms,
                        COALESCE(ss.status_code, 'unknown') AS status_code,
                        COALESCE(ss.granted_duration, 0) AS granted_duration,
                        'fallback'::VARCHAR(20) AS correlation_type,
                        0.5::NUMERIC(3,2) AS match_confidence,
                        4 AS strategy_order
                    FROM srem_messages s
                    JOIN ssem_messages ss
                      ON ss.request_station_id_ref = s.station_id
                     AND ss.generation_time BETWEEN s.generation_time - (@WindowSeconds * INTERVAL '1 second')
                                               AND s.generation_time + (@WindowSeconds * INTERVAL '1 second')
                    WHERE s.generation_time IS NOT NULL
                      AND NULLIF(TRIM(s.station_id), '') IS NOT NULL
                ),
                candidates AS (
                    SELECT * FROM strict_candidates
                    UNION ALL
                    SELECT * FROM fallback_by_intersection_id
                    UNION ALL
                    SELECT * FROM fallback_by_intersection_name
                    UNION ALL
                    SELECT * FROM fallback_by_station_ref
                ),
                ranked AS (
                    SELECT
                        c.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY c.srem_id
                            ORDER BY c.strategy_order ASC, ABS(c.time_delta_ms) ASC
                        ) AS rn
                    FROM candidates c
                ),
                chosen AS (
                    SELECT *
                    FROM ranked
                    WHERE rn = 1
                ),
                inserted AS (
                    INSERT INTO obu_rsu_correlations (
                        srem_id, ssem_id, mapem_id, spatem_id,
                        obu_station_id, rsu_intersection_id, request_id, correlation_type,
                        match_confidence, srem_timestamp, ssem_timestamp, time_delta_ms,
                        request_type, status_code, granted_duration)
                    SELECT
                        c.srem_id,
                        c.ssem_id,
                        NULL,
                        NULL,
                        c.obu_station_id,
                        c.rsu_intersection_id,
                        c.request_id,
                        c.correlation_type,
                        c.match_confidence,
                        c.srem_timestamp,
                        c.ssem_timestamp,
                        c.time_delta_ms,
                        'signal_request',
                        c.status_code,
                        c.granted_duration
                    FROM chosen c
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM obu_rsu_correlations existing
                        WHERE existing.srem_id = c.srem_id
                          AND existing.ssem_id = c.ssem_id
                    )
                    RETURNING correlation_type
                )
                SELECT
                    (SELECT COUNT(*) FROM srem_messages s WHERE s.generation_time IS NOT NULL) AS ""TotalSrems"",
                    (SELECT COUNT(*) FROM chosen) AS ""SelectedCandidates"",
                    (SELECT COUNT(*) FROM inserted) AS ""InsertedTotal"",
                    (SELECT COUNT(*) FROM inserted WHERE correlation_type = 'strict') AS ""InsertedStrict"",
                    (SELECT COUNT(*) FROM inserted WHERE correlation_type = 'fallback') AS ""InsertedFallback"";
            ";

            var summary = await connection.QuerySingleAsync<CorrelationSummaryRow>(
                correlationQuery,
                new { WindowSeconds = timeWindowSeconds });

            Console.WriteLine(
                $"Correlation summary (set-based): scanned={summary.TotalSrems}, selected={summary.SelectedCandidates}, " +
                $"inserted={summary.InsertedTotal}, strict={summary.InsertedStrict}, fallback={summary.InsertedFallback}, " +
                $"duplicatesSkipped={summary.SelectedCandidates - summary.InsertedTotal}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording OBU-RSU correlations: {ex.Message}");
            throw;
        }
    }

    private sealed class CorrelationSummaryRow
    {
        public int TotalSrems { get; init; }
        public int SelectedCandidates { get; init; }
        public int InsertedTotal { get; init; }
        public int InsertedStrict { get; init; }
        public int InsertedFallback { get; init; }
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

    public async Task<PagedResult<MapEntityDto>> GetMapEntitiesPagedAsync(
        DateTime? fromTime = null,
        DateTime? toTime = null,
        IEnumerable<string>? entityTypes = null,
        IEnumerable<string>? messageTypes = null,
        IEnumerable<string>? vehicleCategories = null,
        IEnumerable<int>? stationTypes = null,
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

        var filtered = all
            .Where(entity => normalizedEntityTypes.Count == 0 || normalizedEntityTypes.Contains(entity.EntityType))
            .Where(entity => normalizedMessageTypes.Count == 0 || normalizedMessageTypes.Contains(entity.MessageType))
            .Where(entity => normalizedVehicleCategories.Count == 0 || normalizedVehicleCategories.Contains(GetVehicleCategory(entity)))
            .Where(entity => selectedStationTypes.Count == 0 || !entity.StationType.HasValue || selectedStationTypes.Contains(entity.StationType.Value))
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

    private static string GetVehicleCategory(MapEntityDto entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.VehicleRole))
        {
            var normalizedRole = entity.VehicleRole.Trim().ToLowerInvariant();
            if (int.TryParse(normalizedRole, out var roleCode))
            {
                if (VehicleRoleNames.TryGetValue(roleCode, out var roleName))
                {
                    return roleName;
                }

                return roleCode == 0
                    ? "0"
                    : $"Vehicle Role {roleCode}";
            }

            return normalizedRole switch
            {
                "publictransport" => "Public Transport",
                "public_transport" => "Public Transport",
                "emergency" => "Emergency",
                "specialtransport" => "Special Transport",
                "dangerousgoods" => "Dangerous Goods",
                "roadwork" => "Road Work",
                _ => char.ToUpperInvariant(normalizedRole[0]) + normalizedRole[1..]
            };
        }

        if (!entity.StationType.HasValue)
        {
            return "Unknown";
        }

        return StationTypeNames.TryGetValue(entity.StationType.Value, out var stationName)
            ? stationName
            : "Unknown";
    }
}
