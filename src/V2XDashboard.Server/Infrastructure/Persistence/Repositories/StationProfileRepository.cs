using Dapper;
using Npgsql;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class StationProfileRepository : IStationProfileRepository
{
    private readonly string _connectionString;

    public StationProfileRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public async Task RebuildStationProfilesAsync()
    {
        const string truncateSql = @"
            TRUNCATE TABLE station_profile_message_types;
            TRUNCATE TABLE station_profiles;";

        const string insertProfilesSql = @"
            WITH source AS (
                SELECT
                    station_id,
                    'OBU'::VARCHAR AS entity_type,
                    station_type,
                    COALESCE(NULLIF(vehicle_role, ''), CAST(station_type AS VARCHAR), 'Unknown') AS vehicle_category,
                    is_secure_signed,
                    is_secure_encrypted,
                    generation_time,
                    'CAM'::VARCHAR AS message_type
                FROM cam_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL

                SELECT
                    station_id,
                    'EVENT'::VARCHAR AS entity_type,
                    station_type,
                    COALESCE(CAST(station_type AS VARCHAR), 'Unknown') AS vehicle_category,
                    is_secure_signed,
                    is_secure_encrypted,
                    generation_time,
                    'DENM'::VARCHAR AS message_type
                FROM denm_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL

                SELECT
                    station_id,
                    'RSU'::VARCHAR AS entity_type,
                    NULL::INTEGER AS station_type,
                    NULL::VARCHAR AS vehicle_category,
                    is_secure_signed,
                    is_secure_encrypted,
                    generation_time,
                    'MAPEM'::VARCHAR AS message_type
                FROM mapem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL

                SELECT
                    station_id,
                    'RSU'::VARCHAR AS entity_type,
                    NULL::INTEGER AS station_type,
                    NULL::VARCHAR AS vehicle_category,
                    is_secure_signed,
                    is_secure_encrypted,
                    generation_time,
                    'SPATEM'::VARCHAR AS message_type
                FROM spatem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL

                SELECT
                    station_id,
                    'OBU'::VARCHAR AS entity_type,
                    NULL::INTEGER AS station_type,
                    COALESCE(NULLIF(vehicle_type, ''), 'Unknown') AS vehicle_category,
                    is_secure_signed,
                    is_secure_encrypted,
                    generation_time,
                    'SREM'::VARCHAR AS message_type
                FROM srem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL

                SELECT
                    station_id,
                    'RSU'::VARCHAR AS entity_type,
                    NULL::INTEGER AS station_type,
                    NULL::VARCHAR AS vehicle_category,
                    is_secure_signed,
                    is_secure_encrypted,
                    generation_time,
                    'SSEM'::VARCHAR AS message_type
                FROM ssem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''
            )
            INSERT INTO station_profiles (
                station_id,
                entity_type,
                station_type,
                vehicle_category,
                supports_secure_comm,
                supports_signed,
                supports_encrypted,
                first_seen_at,
                last_seen_at,
                updated_at)
            SELECT
                station_id,
                CASE
                    WHEN BOOL_OR(entity_type = 'RSU') THEN 'RSU'
                    WHEN BOOL_OR(entity_type = 'OBU') THEN 'OBU'
                    ELSE 'EVENT'
                END AS entity_type,
                MAX(station_type) FILTER (WHERE station_type IS NOT NULL) AS station_type,
                (ARRAY_AGG(vehicle_category ORDER BY generation_time DESC)
                    FILTER (WHERE vehicle_category IS NOT NULL AND vehicle_category <> ''))[1] AS vehicle_category,
                BOOL_OR(COALESCE(is_secure_signed, FALSE) OR COALESCE(is_secure_encrypted, FALSE)) AS supports_secure_comm,
                BOOL_OR(COALESCE(is_secure_signed, FALSE)) AS supports_signed,
                BOOL_OR(COALESCE(is_secure_encrypted, FALSE)) AS supports_encrypted,
                MIN(generation_time) AS first_seen_at,
                MAX(generation_time) AS last_seen_at,
                NOW() AS updated_at
            FROM source
            GROUP BY station_id;";

        const string insertMessageTypesSql = @"
            WITH source AS (
                SELECT station_id, generation_time, 'CAM'::VARCHAR AS message_type
                FROM cam_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL
                SELECT station_id, generation_time, 'DENM'::VARCHAR AS message_type
                FROM denm_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL
                SELECT station_id, generation_time, 'MAPEM'::VARCHAR AS message_type
                FROM mapem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL
                SELECT station_id, generation_time, 'SPATEM'::VARCHAR AS message_type
                FROM spatem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL
                SELECT station_id, generation_time, 'SREM'::VARCHAR AS message_type
                FROM srem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''

                UNION ALL
                SELECT station_id, generation_time, 'SSEM'::VARCHAR AS message_type
                FROM ssem_messages
                WHERE station_id IS NOT NULL AND TRIM(station_id) <> ''
            )
            INSERT INTO station_profile_message_types (station_id, message_type, first_seen_at, last_seen_at)
            SELECT
                station_id,
                message_type,
                MIN(generation_time) AS first_seen_at,
                MAX(generation_time) AS last_seen_at
            FROM source
            GROUP BY station_id, message_type;";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(truncateSql, transaction: transaction);
        await connection.ExecuteAsync(insertProfilesSql, transaction: transaction);
        await connection.ExecuteAsync(insertMessageTypesSql, transaction: transaction);

        await transaction.CommitAsync();
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
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);
        var offset = (safePageNumber - 1) * safePageSize;

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(stationId))
        {
            whereClauses.Add("sp.station_id ILIKE @StationId");
            parameters.Add("@StationId", $"%{stationId.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            whereClauses.Add("sp.entity_type = @EntityType");
            parameters.Add("@EntityType", entityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(vehicleCategory))
        {
            whereClauses.Add("sp.vehicle_category ILIKE @VehicleCategory");
            parameters.Add("@VehicleCategory", $"%{vehicleCategory.Trim()}%");
        }

        if (stationType.HasValue)
        {
            whereClauses.Add("sp.station_type = @StationType");
            parameters.Add("@StationType", stationType.Value);
        }

        if (supportsSecureComm.HasValue)
        {
            whereClauses.Add("sp.supports_secure_comm = @SupportsSecureComm");
            parameters.Add("@SupportsSecureComm", supportsSecureComm.Value);
        }

        if (!string.IsNullOrWhiteSpace(messageType))
        {
            whereClauses.Add(@"EXISTS (
                SELECT 1
                FROM station_profile_message_types spmt
                WHERE spmt.station_id = sp.station_id
                  AND spmt.message_type = @MessageType)");
            parameters.Add("@MessageType", messageType.Trim());
        }

        var whereSql = whereClauses.Count == 0
            ? string.Empty
            : $"WHERE {string.Join(" AND ", whereClauses)}";

        var countSql = $"SELECT COUNT(*) FROM station_profiles sp {whereSql};";

        var dataSql = $@"
            SELECT
                sp.station_id AS StationId,
                sp.entity_type AS EntityType,
                sp.station_type AS StationType,
                sp.vehicle_category AS VehicleCategory,
                sp.supports_secure_comm AS SupportsSecureComm,
                sp.supports_signed AS SupportsSigned,
                sp.supports_encrypted AS SupportsEncrypted,
                sp.first_seen_at AS FirstSeenAt,
                sp.last_seen_at AS LastSeenAt,
                COALESCE(mt.message_types_csv, '') AS MessageTypesCsv
            FROM station_profiles sp
            LEFT JOIN (
                SELECT station_id, STRING_AGG(message_type, ',' ORDER BY message_type) AS message_types_csv
                FROM station_profile_message_types
                GROUP BY station_id
            ) mt ON mt.station_id = sp.station_id
            {whereSql}
            ORDER BY sp.last_seen_at DESC, sp.station_id
            OFFSET @Offset LIMIT @Limit;";

        parameters.Add("@Offset", offset);
        parameters.Add("@Limit", safePageSize);

        await using var connection = new NpgsqlConnection(_connectionString);

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var rows = await connection.QueryAsync<StationProfileRow>(dataSql, parameters);

        return new PagedResult<StationProfileDto>
        {
            Items = rows.Select(MapRow).ToList(),
            TotalCount = totalCount,
            PageNumber = safePageNumber,
            PageSize = safePageSize
        };
    }

    public async Task<StationProfileDto?> GetStationProfileByStationIdAsync(string stationId)
    {
        const string sql = @"
            SELECT
                sp.station_id AS StationId,
                sp.entity_type AS EntityType,
                sp.station_type AS StationType,
                sp.vehicle_category AS VehicleCategory,
                sp.supports_secure_comm AS SupportsSecureComm,
                sp.supports_signed AS SupportsSigned,
                sp.supports_encrypted AS SupportsEncrypted,
                sp.first_seen_at AS FirstSeenAt,
                sp.last_seen_at AS LastSeenAt,
                COALESCE(mt.message_types_csv, '') AS MessageTypesCsv
            FROM station_profiles sp
            LEFT JOIN (
                SELECT station_id, STRING_AGG(message_type, ',' ORDER BY message_type) AS message_types_csv
                FROM station_profile_message_types
                GROUP BY station_id
            ) mt ON mt.station_id = sp.station_id
            WHERE sp.station_id = @StationId
            LIMIT 1;";

        await using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<StationProfileRow>(sql, new { StationId = stationId });
        return row is null ? null : MapRow(row);
    }

    public async Task<StationCapabilitiesSummaryDto> GetStationCapabilitiesSummaryAsync()
    {
        const string baseSummarySql = @"
            SELECT
                COUNT(*) AS TotalStations,
                COUNT(*) FILTER (WHERE entity_type = 'OBU') AS ObuStations,
                COUNT(*) FILTER (WHERE entity_type = 'RSU') AS RsuStations,
                COUNT(*) FILTER (WHERE entity_type = 'EVENT') AS EventStations,
                COUNT(*) FILTER (WHERE supports_secure_comm) AS SecureStations,
                COUNT(*) FILTER (WHERE supports_signed) AS SignedStations,
                COUNT(*) FILTER (WHERE supports_encrypted) AS EncryptedStations
            FROM station_profiles;";

        const string coverageSql = @"
            SELECT message_type AS MessageType, COUNT(*) AS Count
            FROM station_profile_message_types
            GROUP BY message_type;";

        await using var connection = new NpgsqlConnection(_connectionString);
        var summary = await connection.QuerySingleAsync<StationCapabilitiesSummaryDto>(baseSummarySql);

        var coverageRows = await connection.QueryAsync<MessageCoverageRow>(coverageSql);
        summary.MessageTypeCoverage = coverageRows
            .ToDictionary(static row => row.MessageType, static row => row.Count, StringComparer.OrdinalIgnoreCase);

        return summary;
    }

    public async Task<HashSet<string>> GetStationIdsByProfileFiltersAsync(
        IEnumerable<string>? vehicleCategories = null,
        IEnumerable<int>? stationTypes = null)
    {
        var normalizedVehicleCategories = (vehicleCategories ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();

        var normalizedStationTypes = (stationTypes ?? Array.Empty<int>())
            .Distinct()
            .ToArray();

        if (normalizedVehicleCategories.Length == 0 && normalizedStationTypes.Length == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (normalizedVehicleCategories.Length > 0)
        {
            whereClauses.Add("sp.vehicle_category = ANY(@VehicleCategories)");
            parameters.Add("@VehicleCategories", normalizedVehicleCategories);
        }

        if (normalizedStationTypes.Length > 0)
        {
            whereClauses.Add("sp.station_type = ANY(@StationTypes)");
            parameters.Add("@StationTypes", normalizedStationTypes);
        }

        var sql = $@"
            SELECT sp.station_id
            FROM station_profiles sp
            WHERE {string.Join(" OR ", whereClauses)};";

        await using var connection = new NpgsqlConnection(_connectionString);
        var stationIds = await connection.QueryAsync<string>(sql, parameters);

        return stationIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static StationProfileDto MapRow(StationProfileRow row)
    {
        return new StationProfileDto
        {
            StationId = row.StationId,
            EntityType = row.EntityType,
            StationType = row.StationType,
            VehicleCategory = row.VehicleCategory,
            SupportsSecureComm = row.SupportsSecureComm,
            SupportsSigned = row.SupportsSigned,
            SupportsEncrypted = row.SupportsEncrypted,
            FirstSeenAt = row.FirstSeenAt,
            LastSeenAt = row.LastSeenAt,
            ObservedMessageTypes = string.IsNullOrWhiteSpace(row.MessageTypesCsv)
                ? new List<string>()
                : row.MessageTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        };
    }

    private sealed class StationProfileRow
    {
        public string StationId { get; init; } = string.Empty;
        public string EntityType { get; init; } = "Unknown";
        public int? StationType { get; init; }
        public string? VehicleCategory { get; init; }
        public bool SupportsSecureComm { get; init; }
        public bool SupportsSigned { get; init; }
        public bool SupportsEncrypted { get; init; }
        public DateTime FirstSeenAt { get; init; }
        public DateTime LastSeenAt { get; init; }
        public string MessageTypesCsv { get; init; } = string.Empty;
    }

    private sealed class MessageCoverageRow
    {
        public string MessageType { get; init; } = string.Empty;
        public int Count { get; init; }
    }
}
