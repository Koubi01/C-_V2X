using Dapper;
using Npgsql;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class MapTileRepository : IMapTileRepository
{
    private const int TileExtent = 4096;
    private const int TileBuffer = 64;
    private readonly string _connectionString;

    public MapTileRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public Task<byte[]> GetCamTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        return GetTileAsync(CreateStandardSpec(
            tableName: "cam_messages",
            layerName: TileLayerNames.Cam,
            selectList: "id, packet_id, generation_time, station_id, latitude, longitude, altitude, speed, heading, station_type, vehicle_role, acceleration, curvature, yaw_rate, lateral_acceleration, vertical_acceleration, decode_status, vehicle_length, vehicle_width, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id",
            supportsStationType: true,
            supportsVehicleRole: true), z, x, y, filters, cancellationToken);
    }

    public Task<byte[]> GetDenmTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        return GetTileAsync(CreateStandardSpec(
            tableName: "denm_messages",
            layerName: TileLayerNames.Denm,
            selectList: "id, packet_id, generation_time, station_id, cause_code, detection_time, reference_time, latitude, longitude, altitude, relevance_traffic_direction, validity_duration, station_type, awareness_traffic_direction, original_station_type, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id",
            supportsStationType: true,
            supportsVehicleRole: false), z, x, y, filters, cancellationToken);
    }

    public Task<byte[]> GetMapemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        return GetTileAsync(CreateStandardSpec(
            tableName: "mapem_messages",
            layerName: TileLayerNames.Mapem,
            selectList: "id, packet_id, generation_time, station_id, intersection_id, intersection_name, latitude, longitude, lane_count, road_width, speed_limit, map_version, publisher_id, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id",
            supportsStationType: false,
            supportsVehicleRole: false), z, x, y, filters, cancellationToken);
    }

    public Task<byte[]> GetSpatemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        return GetTileAsync(CreateStandardSpec(
            tableName: "spatem_messages",
            layerName: TileLayerNames.Spatem,
            selectList: "id, packet_id, generation_time, station_id, intersection_id, intersection_name, latitude, longitude, current_phase, phase_state, connection_maneuver_assist_id, phase0_signal_group, phase1_signal_group, phase2_signal_group, phase3_signal_group, phase4_signal_group, phase5_signal_group, phase0_event_state, phase1_event_state, phase2_event_state, phase3_event_state, phase4_event_state, phase5_event_state, phase0_connection_maneuver_assist_id0, phase0_connection_maneuver_assist_id1, phase1_connection_maneuver_assist_id0, phase1_connection_maneuver_assist_id1, phase2_connection_maneuver_assist_id0, phase2_connection_maneuver_assist_id1, phase3_connection_maneuver_assist_id0, phase3_connection_maneuver_assist_id1, phase4_connection_maneuver_assist_id0, phase4_connection_maneuver_assist_id1, phase5_connection_maneuver_assist_id0, phase5_connection_maneuver_assist_id1, publisher_id, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id",
            supportsStationType: false,
            supportsVehicleRole: false), z, x, y, filters, cancellationToken);
    }

    public Task<byte[]> GetSremTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        return GetTileAsync(CreateStandardSpec(
            tableName: "srem_messages",
            layerName: TileLayerNames.Srem,
            selectList: "id, packet_id, generation_time, station_id, intersection_id, intersection_name, latitude, longitude, requested_phase, vehicle_type, request_reason, request_id, requestor_id, required_accuracy, in_bound_lane_id, out_bound_lane_id, heading, speed, transmission_power, route_names, transit_schedule, requestor_name, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id",
            supportsStationType: false,
            supportsVehicleRole: false), z, x, y, filters, cancellationToken);
    }

    public Task<byte[]> GetSsemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        return GetTileAsync(CreateStandardSpec(
            tableName: "ssem_messages",
            layerName: TileLayerNames.Ssem,
            selectList: "id, packet_id, generation_time, station_id, intersection_id, intersection_name, latitude, longitude, status_code, granted_duration, request_id_ref, responder_id, request_station_id_ref, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id",
            supportsStationType: false,
            supportsVehicleRole: false), z, x, y, filters, cancellationToken);
    }

    public Task<byte[]> GetCorrelationsTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        var spec = new TileQuerySpec(
            TableName: "obu_rsu_correlations",
            LayerName: TileLayerNames.Correlations,
            SelectList: "id, srem_id, ssem_id, mapem_id, spatem_id, obu_station_id, rsu_intersection_id, request_id, correlation_type, match_confidence, srem_timestamp, ssem_timestamp, time_delta_ms, request_type, status_code, granted_duration, srem_latitude, srem_longitude, ssem_latitude, ssem_longitude, created_at, FALSE AS is_secure_signed, FALSE AS is_secure_encrypted",
            GeometryExpression: "ST_MakeLine(\n                ST_Transform(ST_SetSRID(ST_MakePoint(srem_longitude, srem_latitude), 4326), 3857),\n                ST_Transform(ST_SetSRID(ST_MakePoint(ssem_longitude, ssem_latitude), 4326), 3857)\n            )",
            SpatialFilterClause: "ST_Intersects(\n                ST_MakeLine(\n                    ST_Transform(ST_SetSRID(ST_MakePoint(srem_longitude, srem_latitude), 4326), 3857),\n                    ST_Transform(ST_SetSRID(ST_MakePoint(ssem_longitude, ssem_latitude), 4326), 3857)\n                ),\n                tile_bbox.geom\n            )",
            SupportsStationType: false,
            SupportsVehicleRole: false,
            SupportsSecurityFilters: false,
            TimestampColumn: "srem_timestamp",
            AdditionalWhereClause: "srem_latitude IS NOT NULL AND srem_longitude IS NOT NULL AND ssem_latitude IS NOT NULL AND ssem_longitude IS NOT NULL\n                AND srem_latitude BETWEEN -85 AND 85\n                AND srem_longitude BETWEEN -180 AND 180\n                AND ssem_latitude BETWEEN -85 AND 85\n                AND ssem_longitude BETWEEN -180 AND 180");

        return GetTileAsync(spec, z, x, y, filters, cancellationToken);
    }

    private async Task<byte[]> GetTileAsync(TileQuerySpec spec, int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var sql = BuildSql(spec, filters);
        var parameters = BuildParameters(spec.LayerName, z, x, y, filters);
        var tile = await connection.QueryFirstOrDefaultAsync<byte[]>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return tile ?? Array.Empty<byte>();
    }

    private static TileQuerySpec CreateStandardSpec(string tableName, string layerName, string selectList, bool supportsStationType, bool supportsVehicleRole)
    {
        return new TileQuerySpec(
            tableName,
            layerName,
            selectList,
            "ST_Transform(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), 3857)",
            "ST_Intersects(\n                ST_Transform(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), 3857),\n                tile_bbox.geom\n            )",
            supportsStationType,
                supportsVehicleRole,
                true,
                "generation_time",
            "latitude IS NOT NULL AND longitude IS NOT NULL\n                AND latitude BETWEEN -85 AND 85\n                AND longitude BETWEEN -180 AND 180");
    }

    private static string BuildSql(TileQuerySpec spec, TileFilterParams filters)
    {
        var whereClauses = new List<string>
        {
            spec.SpatialFilterClause
        };

        if (!string.IsNullOrWhiteSpace(spec.AdditionalWhereClause))
        {
            whereClauses.Add(spec.AdditionalWhereClause);
        }

        if (filters.FromTime.HasValue)
        {
            whereClauses.Add($"{spec.TimestampColumn} >= @FromTime::timestamp");
        }

        if (filters.ToTime.HasValue)
        {
            whereClauses.Add($"{spec.TimestampColumn} <= @ToTime::timestamp");
        }

        if (spec.SupportsSecurityFilters)
        {
            whereClauses.Add("(@IsSecureSigned::boolean IS NULL OR is_secure_signed = @IsSecureSigned::boolean)");
            whereClauses.Add("(@IsSecureEncrypted::boolean IS NULL OR is_secure_encrypted = @IsSecureEncrypted::boolean)");
        }

        if (spec.SupportsStationType)
        {
            whereClauses.Add("(@StationTypes::integer[] IS NULL OR station_type = ANY(@StationTypes::integer[]))");
        }

        if (spec.SupportsVehicleRole)
        {
            whereClauses.Add("(@VehicleRoles::text[] IS NULL OR vehicle_role = ANY(@VehicleRoles::text[]))");
        }

        return $"""
            WITH tile_bbox AS (
                SELECT ST_TileEnvelope(@Z, @X, @Y) AS geom
            ),
            tile_rows AS (
                SELECT
                    {spec.SelectList},
                    ST_AsMVTGeom(
                        {spec.GeometryExpression},
                        tile_bbox.geom,
                        {TileExtent},
                        {TileBuffer},
                        true
                    ) AS geom
                FROM {spec.TableName}
                CROSS JOIN tile_bbox
                WHERE {string.Join("\n                    AND ", whereClauses)}
            )
            SELECT COALESCE(
                (
                    SELECT ST_AsMVT(tile_rows, @LayerName::text, {TileExtent}, 'geom')
                    FROM tile_rows
                ),
                decode('', 'hex')
            );
            """;
    }

    private static DynamicParameters BuildParameters(string layerName, int z, int x, int y, TileFilterParams filters)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Z", z);
        parameters.Add("X", x);
        parameters.Add("Y", y);
        parameters.Add("LayerName", layerName);
        parameters.Add("FromTime", filters.FromTime);
        parameters.Add("ToTime", filters.ToTime);
        parameters.Add("IsSecureSigned", filters.IsSecureSigned);
        parameters.Add("IsSecureEncrypted", filters.IsSecureEncrypted);
        parameters.Add("StationTypes", NormalizeStationTypes(filters.StationTypes));
        parameters.Add("VehicleRoles", NormalizeVehicleRoles(filters.VehicleRoles));
        return parameters;
    }

    private static int[]? NormalizeStationTypes(int[]? stationTypes)
    {
        if (stationTypes is null)
        {
            return null;
        }

        var normalizedStationTypes = stationTypes
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        return normalizedStationTypes.Length == 0 ? null : normalizedStationTypes;
    }

    private static string[]? NormalizeVehicleRoles(string[]? vehicleRoles)
    {
        if (vehicleRoles is null)
        {
            return null;
        }

        var normalizedRoles = vehicleRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedRoles.Length == 0 ? null : normalizedRoles;
    }

    private sealed record TileQuerySpec(
        string TableName,
        string LayerName,
        string SelectList,
        string GeometryExpression,
        string SpatialFilterClause,
        bool SupportsStationType,
        bool SupportsVehicleRole,
        bool SupportsSecurityFilters,
        string TimestampColumn,
        string? AdditionalWhereClause = null);
}