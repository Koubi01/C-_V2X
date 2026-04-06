using System.Data;
using System.Text;
using Dapper;
using Npgsql;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class CorrelationRepository : ICorrelationRepository
{
    private readonly string _connectionString;

    public CorrelationRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public async Task PopulateIntersectionMetadataForFileAsync(string fileName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await UpdateSpatemIntersectionForFileAsync(connection, fileName);
        await UpdateSremIntersectionForFileAsync(connection, fileName);
        await UpdateSsemIntersectionForFileAsync(connection, fileName);
    }

    public async Task PopulateIntersectionMetadataForAllFilesAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await UpdateSpatemIntersectionAsync(connection);
        await UpdateSremIntersectionAsync(connection);
        await UpdateSsemIntersectionAsync(connection);
    }

    public async Task<CorrelationRecordSummary> RecordOBUToRSUCorrelationAsync(int timeWindowSeconds)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

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
                        s.latitude AS srem_latitude,
                        s.longitude AS srem_longitude,
                        ss.latitude AS ssem_latitude,
                        ss.longitude AS ssem_longitude,
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
                        s.latitude AS srem_latitude,
                        s.longitude AS srem_longitude,
                        ss.latitude AS ssem_latitude,
                        ss.longitude AS ssem_longitude,
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
                        s.latitude AS srem_latitude,
                        s.longitude AS srem_longitude,
                        ss.latitude AS ssem_latitude,
                        ss.longitude AS ssem_longitude,
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
                        s.latitude AS srem_latitude,
                        s.longitude AS srem_longitude,
                        ss.latitude AS ssem_latitude,
                        ss.longitude AS ssem_longitude,
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
                        request_type, status_code, granted_duration,
                        srem_latitude, srem_longitude, ssem_latitude, ssem_longitude)
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
                        c.granted_duration,
                        c.srem_latitude,
                        c.srem_longitude,
                        c.ssem_latitude,
                        c.ssem_longitude
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

        var row = await connection.QuerySingleAsync<CorrelationSummaryRow>(
            correlationQuery,
            new { WindowSeconds = timeWindowSeconds });

        await PopulateCorrelationCoordinatesAsync(connection);

        return new CorrelationRecordSummary
        {
            TotalSrems = row.TotalSrems,
            SelectedCandidates = row.SelectedCandidates,
            InsertedTotal = row.InsertedTotal,
            InsertedStrict = row.InsertedStrict,
            InsertedFallback = row.InsertedFallback
        };
    }

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
        await using var connection = new NpgsqlConnection(_connectionString);

        var query = new StringBuilder(@"
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
                ss.longitude AS SsemLongitude,
                (COALESCE(s.is_secure_signed, FALSE) OR COALESCE(ss.is_secure_signed, FALSE)) AS IsSecureSigned,
                (COALESCE(s.is_secure_encrypted, FALSE) OR COALESCE(ss.is_secure_encrypted, FALSE)) AS IsSecureEncrypted
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

        if (isSecureSigned.HasValue)
        {
            query.Append(" AND (COALESCE(s.is_secure_signed, FALSE) OR COALESCE(ss.is_secure_signed, FALSE)) = @IsSecureSigned");
            parameters.Add("@IsSecureSigned", isSecureSigned.Value);
        }

        if (isSecureEncrypted.HasValue)
        {
            query.Append(" AND (COALESCE(s.is_secure_encrypted, FALSE) OR COALESCE(ss.is_secure_encrypted, FALSE)) = @IsSecureEncrypted");
            parameters.Add("@IsSecureEncrypted", isSecureEncrypted.Value);
        }

        query.Append(" ORDER BY c.srem_timestamp DESC");

        if (limit.HasValue)
        {
            query.Append(" LIMIT @Limit");
            parameters.Add("@Limit", limit.Value);
        }
        else
        {
            query.Append(" LIMIT 100");
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
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var (safePageNumber, safePageSize, offset) = NormalizePaging(pageNumber, pageSize, 250);

        var baseFrom = new StringBuilder(@"
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

        if (isSecureSigned.HasValue)
        {
            baseFrom.Append(" AND (COALESCE(s.is_secure_signed, FALSE) OR COALESCE(ss.is_secure_signed, FALSE)) = @IsSecureSigned");
            parameters.Add("@IsSecureSigned", isSecureSigned.Value);
        }

        if (isSecureEncrypted.HasValue)
        {
            baseFrom.Append(" AND (COALESCE(s.is_secure_encrypted, FALSE) OR COALESCE(ss.is_secure_encrypted, FALSE)) = @IsSecureEncrypted");
            parameters.Add("@IsSecureEncrypted", isSecureEncrypted.Value);
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
                ss.longitude AS SsemLongitude,
                (COALESCE(s.is_secure_signed, FALSE) OR COALESCE(ss.is_secure_signed, FALSE)) AS IsSecureSigned,
                (COALESCE(s.is_secure_encrypted, FALSE) OR COALESCE(ss.is_secure_encrypted, FALSE)) AS IsSecureEncrypted
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

    public async Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

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

    private static Task UpdateSpatemIntersectionForFileAsync(IDbConnection connection, string fileName)
    {
        const string query = @"
            WITH target AS (
                SELECT s.id, s.generation_time, s.intersection_id
                FROM spatem_messages s
                JOIN packets p ON p.id = s.packet_id
                WHERE p.pcap_file_name = @FileName
                ),
                resolved AS (
                    SELECT
                    t.id,
                    m_best.intersection_name,
                    m_best.latitude,
                    m_best.longitude
                    FROM target t
                    LEFT JOIN LATERAL (
                    SELECT m.intersection_name, m.latitude, m.longitude
                    FROM mapem_messages m
                    WHERE m.intersection_id = t.intersection_id
                    AND m.generation_time BETWEEN t.generation_time - INTERVAL '10 minutes'
                    AND t.generation_time + INTERVAL '10 minutes'
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - t.generation_time))) ASC
                    LIMIT 1
                    ) m_best ON TRUE
                )
                UPDATE spatem_messages s
                SET
                intersection_name = COALESCE(r.intersection_name, s.intersection_name),
                latitude = COALESCE(r.latitude, s.latitude),
                longitude = COALESCE(r.longitude, s.longitude)
                FROM resolved r
                WHERE s.id = r.id
                AND (
                s.intersection_name IS DISTINCT FROM COALESCE(r.intersection_name, s.intersection_name)
                OR s.latitude IS DISTINCT FROM COALESCE(r.latitude, s.latitude)
                OR s.longitude IS DISTINCT FROM COALESCE(r.longitude, s.longitude)
                );";

        return connection.ExecuteAsync(query, new { FileName = fileName });
    }

    private static Task UpdateSremIntersectionForFileAsync(IDbConnection connection, string fileName)
    {
        const string query = @"
            WITH target AS (
                SELECT s.id, s.generation_time, s.requestor_id
                FROM srem_messages s
                JOIN packets p ON p.id = s.packet_id
                WHERE p.pcap_file_name = @FileName
                ),
                resolved AS (
                SELECT
                t.id,
                ss_best.intersection_name,
                ss_best.intersection_id
                FROM target t
                LEFT JOIN LATERAL (
                SELECT ss.intersection_name, ss.intersection_id
                FROM ssem_messages ss
                WHERE ss.request_station_id_ref = t.requestor_id
                AND ss.generation_time BETWEEN t.generation_time - INTERVAL '10 minutes'
                AND t.generation_time + INTERVAL '10 minutes'
                ORDER BY ABS(EXTRACT(EPOCH FROM (ss.generation_time - t.generation_time))) ASC
                LIMIT 1
                ) ss_best ON TRUE
                )
                UPDATE srem_messages s
                SET
                intersection_name = COALESCE(r.intersection_name, s.intersection_name),
                intersection_id = COALESCE(r.intersection_id, s.intersection_id)
                FROM resolved r
                WHERE s.id = r.id
                AND (
                s.intersection_name IS DISTINCT FROM COALESCE(r.intersection_name, s.intersection_name)
                OR s.intersection_id IS DISTINCT FROM COALESCE(r.intersection_id, s.intersection_id)
                );";

        return connection.ExecuteAsync(query, new { FileName = fileName });
    }

    private static Task UpdateSsemIntersectionForFileAsync(IDbConnection connection, string fileName)
    {
        const string query = @"
            WITH target AS (
                SELECT ss.id, ss.generation_time, ss.intersection_id
                FROM ssem_messages ss
                JOIN packets p ON p.id = ss.packet_id
                WHERE p.pcap_file_name = @FileName
                ),
                resolved AS (
                SELECT
                t.id,
                m_best.intersection_name,
                m_best.latitude,
                m_best.longitude
                FROM target t
                LEFT JOIN LATERAL (
                SELECT m.intersection_name, m.latitude, m.longitude
                FROM mapem_messages m
                WHERE m.intersection_id = t.intersection_id
                AND m.generation_time BETWEEN t.generation_time - INTERVAL '10 minutes'
                AND t.generation_time + INTERVAL '10 minutes'
                ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - t.generation_time))) ASC
                LIMIT 1
                ) m_best ON TRUE
                )
                UPDATE ssem_messages ss
                SET
                intersection_name = COALESCE(r.intersection_name, ss.intersection_name),
                latitude = COALESCE(r.latitude, ss.latitude),
                longitude = COALESCE(r.longitude, ss.longitude)
                FROM resolved r
                WHERE ss.id = r.id
                AND (
                ss.intersection_name IS DISTINCT FROM COALESCE(r.intersection_name, ss.intersection_name)
                OR ss.latitude IS DISTINCT FROM COALESCE(r.latitude, ss.latitude)
                OR ss.longitude IS DISTINCT FROM COALESCE(r.longitude, ss.longitude)
                );";

        return connection.ExecuteAsync(query, new { FileName = fileName });
    }

    private static Task UpdateSpatemIntersectionAsync(IDbConnection connection)
    {
        const string query = @"
            WITH target AS (
                SELECT s.id, s.generation_time, s.intersection_id
                FROM spatem_messages s
                ),
                resolved AS (
                    SELECT
                    t.id,
                    m_best.intersection_name,
                    m_best.latitude,
                    m_best.longitude
                    FROM target t
                    LEFT JOIN LATERAL (
                    SELECT m.intersection_name, m.latitude, m.longitude
                    FROM mapem_messages m
                    WHERE m.intersection_id = t.intersection_id
                    AND m.generation_time BETWEEN t.generation_time - INTERVAL '10 minutes'
                    AND t.generation_time + INTERVAL '10 minutes'
                    ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - t.generation_time))) ASC
                    LIMIT 1
                    ) m_best ON TRUE
                )
                UPDATE spatem_messages s
                SET
                intersection_name = COALESCE(r.intersection_name, s.intersection_name),
                latitude = COALESCE(r.latitude, s.latitude),
                longitude = COALESCE(r.longitude, s.longitude)
                FROM resolved r
                WHERE s.id = r.id
                AND (
                s.intersection_name IS DISTINCT FROM COALESCE(r.intersection_name, s.intersection_name)
                OR s.latitude IS DISTINCT FROM COALESCE(r.latitude, s.latitude)
                OR s.longitude IS DISTINCT FROM COALESCE(r.longitude, s.longitude)
                );";

        return connection.ExecuteAsync(query);
    }

    private static Task UpdateSremIntersectionAsync(IDbConnection connection)
    {
        const string query = @"
            WITH target AS (
                SELECT s.id, s.generation_time, s.requestor_id
                FROM srem_messages s
                ),
                resolved AS (
                SELECT
                t.id,
                ss_best.intersection_name,
                ss_best.intersection_id
                FROM target t
                LEFT JOIN LATERAL (
                SELECT ss.intersection_name, ss.intersection_id
                FROM ssem_messages ss
                WHERE ss.request_station_id_ref = t.requestor_id
                AND ss.generation_time BETWEEN t.generation_time - INTERVAL '10 minutes'
                AND t.generation_time + INTERVAL '10 minutes'
                ORDER BY ABS(EXTRACT(EPOCH FROM (ss.generation_time - t.generation_time))) ASC
                LIMIT 1
                ) ss_best ON TRUE
                )
                UPDATE srem_messages s
                SET
                intersection_name = COALESCE(r.intersection_name, s.intersection_name),
                intersection_id = COALESCE(r.intersection_id, s.intersection_id)
                FROM resolved r
                WHERE s.id = r.id
                AND (
                s.intersection_name IS DISTINCT FROM COALESCE(r.intersection_name, s.intersection_name)
                OR s.intersection_id IS DISTINCT FROM COALESCE(r.intersection_id, s.intersection_id)
                );";

        return connection.ExecuteAsync(query);
    }

    private static Task UpdateSsemIntersectionAsync(IDbConnection connection)
    {
        const string query = @"
            WITH target AS (
                SELECT ss.id, ss.generation_time, ss.intersection_id
                FROM ssem_messages ss
                ),
                resolved AS (
                SELECT
                t.id,
                m_best.intersection_name,
                m_best.latitude,
                m_best.longitude
                FROM target t
                LEFT JOIN LATERAL (
                SELECT m.intersection_name, m.latitude, m.longitude
                FROM mapem_messages m
                WHERE m.intersection_id = t.intersection_id
                AND m.generation_time BETWEEN t.generation_time - INTERVAL '10 minutes'
                AND t.generation_time + INTERVAL '10 minutes'
                ORDER BY ABS(EXTRACT(EPOCH FROM (m.generation_time - t.generation_time))) ASC
                LIMIT 1
                ) m_best ON TRUE
                )
                UPDATE ssem_messages ss
                SET
                intersection_name = COALESCE(r.intersection_name, ss.intersection_name),
                latitude = COALESCE(r.latitude, ss.latitude),
                longitude = COALESCE(r.longitude, ss.longitude)
                FROM resolved r
                WHERE ss.id = r.id
                AND (
                ss.intersection_name IS DISTINCT FROM COALESCE(r.intersection_name, ss.intersection_name)
                OR ss.latitude IS DISTINCT FROM COALESCE(r.latitude, ss.latitude)
                OR ss.longitude IS DISTINCT FROM COALESCE(r.longitude, ss.longitude)
                );";

        return connection.ExecuteAsync(query);
    }

    private static Task PopulateCorrelationCoordinatesAsync(IDbConnection connection)
    {
        const string query = @"
            UPDATE obu_rsu_correlations c
            SET
                                srem_latitude = COALESCE(NULLIF(c.srem_latitude, 0), NULLIF(s.latitude, 0)),
                                srem_longitude = COALESCE(NULLIF(c.srem_longitude, 0), NULLIF(s.longitude, 0)),
                                ssem_latitude = COALESCE(NULLIF(c.ssem_latitude, 0), NULLIF(ss.latitude, 0)),
                                ssem_longitude = COALESCE(NULLIF(c.ssem_longitude, 0), NULLIF(ss.longitude, 0))
                        FROM srem_messages s,
                                 ssem_messages ss
                        WHERE c.srem_id = s.id
                            AND c.ssem_id = ss.id
                            AND (
                                c.srem_latitude IS NULL OR c.srem_latitude = 0
                                OR c.srem_longitude IS NULL OR c.srem_longitude = 0
                                OR c.ssem_latitude IS NULL OR c.ssem_latitude = 0
                                OR c.ssem_longitude IS NULL OR c.ssem_longitude = 0
                            );";

        return connection.ExecuteAsync(query);
    }

    private static (int PageNumber, int PageSize, int Offset) NormalizePaging(int pageNumber, int pageSize, int maxPageSize = 100)
    {
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize < 1 ? 25 : Math.Min(pageSize, maxPageSize);
        var offset = (safePageNumber - 1) * safePageSize;
        return (safePageNumber, safePageSize, offset);
    }

    private sealed class CorrelationSummaryRow
    {
        public int TotalSrems { get; init; }
        public int SelectedCandidates { get; init; }
        public int InsertedTotal { get; init; }
        public int InsertedStrict { get; init; }
        public int InsertedFallback { get; init; }
    }
}
