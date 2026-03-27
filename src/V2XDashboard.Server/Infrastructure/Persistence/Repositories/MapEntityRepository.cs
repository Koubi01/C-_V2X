using Dapper;
using Npgsql;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class MapEntityRepository : IMapEntityRepository
{
    private readonly string _connectionString;

    public MapEntityRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public async Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var entities = new List<MapEntityDto>();

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
                station_type AS StationType,
                is_secure_signed AS IsSecureSigned,
                is_secure_encrypted AS IsSecureEncrypted
            FROM cam_messages
                        WHERE latitude IS NOT NULL
                            AND longitude IS NOT NULL
                            AND latitude BETWEEN -90 AND 90
                            AND longitude BETWEEN -180 AND 180
                            AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue)
        {
            obuQuery += " AND generation_time >= @FromTime";
        }

        if (toTime.HasValue)
        {
            obuQuery += " AND generation_time <= @ToTime";
        }

        obuQuery += " ORDER BY station_id, generation_time DESC LIMIT 1000";

        var obuParameters = new DynamicParameters();
        if (fromTime.HasValue) obuParameters.Add("@FromTime", fromTime.Value);
        if (toTime.HasValue) obuParameters.Add("@ToTime", toTime.Value);

        var obuEntities = await connection.QueryAsync<MapEntityDto>(obuQuery, obuParameters);
        entities.AddRange(obuEntities);

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
                NULL::INTEGER AS StationType,
                is_secure_signed AS IsSecureSigned,
                is_secure_encrypted AS IsSecureEncrypted
            FROM mapem_messages
                        WHERE latitude IS NOT NULL
                            AND longitude IS NOT NULL
                            AND latitude BETWEEN -90 AND 90
                            AND longitude BETWEEN -180 AND 180
                            AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue)
        {
            rsuMapemQuery += " AND generation_time >= @FromTime";
        }

        if (toTime.HasValue)
        {
            rsuMapemQuery += " AND generation_time <= @ToTime";
        }

        rsuMapemQuery += " ORDER BY generation_time DESC LIMIT 500";

        var rsuParameters = new DynamicParameters();
        rsuParameters.Add("@FromTime", fromTime);
        rsuParameters.Add("@ToTime", toTime);

        var rsuMapemEntities = await connection.QueryAsync<MapEntityDto>(rsuMapemQuery, rsuParameters);
        entities.AddRange(rsuMapemEntities);

        var rsuSpatemQuery = @"
            WITH recent_spatem AS (
                SELECT *
                FROM spatem_messages
                WHERE (@FromTime IS NULL OR generation_time >= @FromTime)
                  AND (@ToTime IS NULL OR generation_time <= @ToTime)
                ORDER BY generation_time DESC
                LIMIT 500
            )
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
                NULL::INTEGER AS StationType,
                s.is_secure_signed AS IsSecureSigned,
                s.is_secure_encrypted AS IsSecureEncrypted
            FROM recent_spatem s
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

        rsuSpatemQuery += " ORDER BY generation_time DESC LIMIT 500";

        var rsuSpatemEntities = await connection.QueryAsync<MapEntityDto>(rsuSpatemQuery, rsuParameters);
        entities.AddRange(rsuSpatemEntities);

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
                NULL::INTEGER AS StationType,
                is_secure_signed AS IsSecureSigned,
                is_secure_encrypted AS IsSecureEncrypted
            FROM srem_messages
            WHERE latitude IS NOT NULL
              AND longitude IS NOT NULL
              AND latitude BETWEEN -90 AND 90
              AND longitude BETWEEN -180 AND 180
              AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue)
        {
            obuSremQuery += " AND generation_time >= @FromTime";
        }

        if (toTime.HasValue)
        {
            obuSremQuery += " AND generation_time <= @ToTime";
        }

        obuSremQuery += " ORDER BY generation_time DESC LIMIT 1000";

        var obuSremEntities = await connection.QueryAsync<MapEntityDto>(obuSremQuery, rsuParameters);
        entities.AddRange(obuSremEntities);

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
                station_type AS StationType,
                is_secure_signed AS IsSecureSigned,
                is_secure_encrypted AS IsSecureEncrypted
            FROM denm_messages
            WHERE latitude IS NOT NULL
              AND longitude IS NOT NULL
              AND latitude BETWEEN -90 AND 90
              AND longitude BETWEEN -180 AND 180
              AND NOT (latitude = 0 AND longitude = 0)";

        if (fromTime.HasValue)
        {
            denmQuery += " AND generation_time >= @FromTime";
        }

        if (toTime.HasValue)
        {
            denmQuery += " AND generation_time <= @ToTime";
        }

        denmQuery += " ORDER BY generation_time DESC LIMIT 1000";

        var denmEntities = await connection.QueryAsync<MapEntityDto>(denmQuery, rsuParameters);
        entities.AddRange(denmEntities);

        var ssemQuery = @"
            WITH recent_ssem AS (
                SELECT *
                FROM ssem_messages
                WHERE (@FromTime IS NULL OR generation_time >= @FromTime)
                  AND (@ToTime IS NULL OR generation_time <= @ToTime)
                ORDER BY generation_time DESC
                LIMIT 1000
            )
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
                NULL::INTEGER AS StationType,
                ss.is_secure_signed AS IsSecureSigned,
                ss.is_secure_encrypted AS IsSecureEncrypted
            FROM recent_ssem ss
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

        ssemQuery += " ORDER BY ss.generation_time DESC LIMIT 1000";

        var ssemEntities = await connection.QueryAsync<MapEntityDto>(ssemQuery, rsuParameters);
        entities.AddRange(ssemEntities);

        return entities;
    }
}
