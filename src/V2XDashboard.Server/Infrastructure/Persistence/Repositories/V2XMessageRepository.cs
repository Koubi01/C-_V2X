using System.Data;
using Dapper;
using Npgsql;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class V2XMessageRepository : IV2XMessageRepository
{
    private readonly string _connectionString;

    public V2XMessageRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public async Task InsertCAMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<CAM> messages)
    {
        if (messages.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < messages.Count; start += batchSize)
        {
            var chunk = messages.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();

            var sql = @"INSERT INTO cam_messages (packet_id, generation_time, station_id,
                latitude, longitude, altitude, speed, heading, station_type,
                vehicle_role, acceleration, curvature, yaw_rate,
                lateral_acceleration, vertical_acceleration, decode_status, vehicle_length, vehicle_width,
                is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id)
            VALUES ";

            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var message = chunk[i];
                var p = i.ToString();

                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @lat{p}, @lon{p}, @alt{p}, @spd{p}, @hed{p}, @stype{p}, @vrole{p}, @acc{p}, @cur{p}, @yaw{p}, @lacc{p}, @vacc{p}, @dstat{p}, @vlen{p}, @vwid{p}, @issigned{p}, @isencrypted{p}, @sproto{p}, @signer{p}, @cert{p})");

                parameters.Add("@pid" + p, message.PacketId);
                parameters.Add("@gen" + p, message.GenerationTime);
                parameters.Add("@sid" + p, message.StationId);
                parameters.Add("@lat" + p, message.Latitude);
                parameters.Add("@lon" + p, message.Longitude);
                parameters.Add("@alt" + p, message.Altitude);
                parameters.Add("@spd" + p, message.Speed);
                parameters.Add("@hed" + p, message.Heading);
                parameters.Add("@stype" + p, message.StationType);
                parameters.Add("@vrole" + p, string.IsNullOrWhiteSpace(message.VehicleRole) ? "unknown" : message.VehicleRole);
                parameters.Add("@acc" + p, message.Acceleration);
                parameters.Add("@cur" + p, message.Curvature);
                parameters.Add("@yaw" + p, message.YawRate);
                parameters.Add("@lacc" + p, message.LateralAcceleration);
                parameters.Add("@vacc" + p, message.VerticalAcceleration);
                parameters.Add("@dstat" + p, message.DecodeStatus);
                parameters.Add("@vlen" + p, message.VehicleLength);
                parameters.Add("@vwid" + p, message.VehicleWidth);
                parameters.Add("@issigned" + p, message.IsSecureSigned);
                parameters.Add("@isencrypted" + p, message.IsSecureEncrypted);
                parameters.Add("@sproto" + p, message.SecurityProtocol);
                parameters.Add("@signer" + p, message.SignerId);
                parameters.Add("@cert" + p, message.CertificateId);
            }

            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    public async Task InsertDENMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<DENM> messages)
    {
        if (messages.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < messages.Count; start += batchSize)
        {
            var chunk = messages.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();

            var sql = @"INSERT INTO denm_messages (packet_id, generation_time, station_id,
                cause_code, detection_time, reference_time,
                latitude, longitude, altitude,
                relevance_traffic_direction, validity_duration, station_type, awareness_traffic_direction, original_station_type,
                is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id)
            VALUES ";

            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var message = chunk[i];
                var p = i.ToString();

                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @cc{p}, @det{p}, @ref{p}, @lat{p}, @lon{p}, @alt{p}, @rtd{p}, @vd{p}, @st{p}, @atd{p}, @ost{p}, @issigned{p}, @isencrypted{p}, @sproto{p}, @signer{p}, @cert{p})");

                parameters.Add("@pid" + p, message.PacketId);
                parameters.Add("@gen" + p, message.GenerationTime);
                parameters.Add("@sid" + p, message.StationId);
                parameters.Add("@cc" + p, string.IsNullOrWhiteSpace(message.CauseCode) ? "unknown" : message.CauseCode);
                parameters.Add("@det" + p, message.DetectionTime);
                parameters.Add("@ref" + p, message.ReferenceTime);
                parameters.Add("@lat" + p, message.Latitude);
                parameters.Add("@lon" + p, message.Longitude);
                parameters.Add("@alt" + p, message.Altitude);
                parameters.Add("@rtd" + p, message.RelevanceTrafficDirection);
                parameters.Add("@vd" + p, message.ValidityDuration);
                parameters.Add("@st" + p, message.StationType);
                parameters.Add("@atd" + p, message.AwarenessTrafficDirection);
                parameters.Add("@ost" + p, message.OriginalStationType);
                parameters.Add("@issigned" + p, message.IsSecureSigned);
                parameters.Add("@isencrypted" + p, message.IsSecureEncrypted);
                parameters.Add("@sproto" + p, message.SecurityProtocol);
                parameters.Add("@signer" + p, message.SignerId);
                parameters.Add("@cert" + p, message.CertificateId);
            }

            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    public async Task InsertMAPEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<MAPEM> messages)
    {
        if (messages.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < messages.Count; start += batchSize)
        {
            var chunk = messages.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();

            var sql = @"INSERT INTO mapem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                lane_count, road_width, speed_limit, map_version, publisher_id,
                is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id)
            VALUES ";

            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var message = chunk[i];
                var p = i.ToString();

                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @iid{p}, @in{p}, @lat{p}, @lon{p}, @lc{p}, @rw{p}, @sl{p}, @mv{p}, @pub{p}, @issigned{p}, @isencrypted{p}, @sproto{p}, @signer{p}, @cert{p})");

                parameters.Add("@pid" + p, message.PacketId);
                parameters.Add("@gen" + p, message.GenerationTime);
                parameters.Add("@sid" + p, message.StationId);
                parameters.Add("@iid" + p, message.IntersectionId);
                parameters.Add("@in" + p, message.IntersectionName);
                parameters.Add("@lat" + p, message.Latitude);
                parameters.Add("@lon" + p, message.Longitude);
                parameters.Add("@lc" + p, message.LaneCount);
                parameters.Add("@rw" + p, message.RoadWidth);
                parameters.Add("@sl" + p, message.SpeedLimit);
                parameters.Add("@mv" + p, string.IsNullOrWhiteSpace(message.MapVersion) ? "1.0" : message.MapVersion);
                parameters.Add("@pub" + p, message.PublisherId);
                parameters.Add("@issigned" + p, message.IsSecureSigned);
                parameters.Add("@isencrypted" + p, message.IsSecureEncrypted);
                parameters.Add("@sproto" + p, message.SecurityProtocol);
                parameters.Add("@signer" + p, message.SignerId);
                parameters.Add("@cert" + p, message.CertificateId);
            }

            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    public async Task InsertSPATEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SPATEM> messages)
    {
        if (messages.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < messages.Count; start += batchSize)
        {
            var chunk = messages.Skip(start).Take(batchSize).ToList();

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
                publisher_id,
                is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id)");
            sql.AppendLine("VALUES");

            var parameters = new DynamicParameters();
            for (var i = 0; i < chunk.Count; i++)
            {
                var message = chunk[i];
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
                    "@PublisherId" + suffix,
                    "@IsSecureSigned" + suffix,
                    "@IsSecureEncrypted" + suffix,
                    "@SecurityProtocol" + suffix,
                    "@SignerId" + suffix,
                    "@CertificateId" + suffix
                };

                sql.Append("(" + string.Join(", ", paramList) + ")");

                parameters.Add("@PacketId" + suffix, message.PacketId);
                parameters.Add("@GenerationTime" + suffix, message.GenerationTime);
                parameters.Add("@StationId" + suffix, message.StationId);
                parameters.Add("@IntersectionId" + suffix, message.IntersectionId);
                parameters.Add("@IntersectionName" + suffix, message.IntersectionName);
                parameters.Add("@Latitude" + suffix, message.Latitude);
                parameters.Add("@Longitude" + suffix, message.Longitude);
                parameters.Add("@CurrentPhase" + suffix, message.CurrentPhase);
                parameters.Add("@PhaseState" + suffix, string.IsNullOrWhiteSpace(message.PhaseState) ? "unknown" : message.PhaseState);
                parameters.Add("@ConnectionManeuverAssistId" + suffix, message.ConnectionManeuverAssistId);
                parameters.Add("@Phase0SignalGroup" + suffix, message.Phase0SignalGroup);
                parameters.Add("@Phase1SignalGroup" + suffix, message.Phase1SignalGroup);
                parameters.Add("@Phase2SignalGroup" + suffix, message.Phase2SignalGroup);
                parameters.Add("@Phase3SignalGroup" + suffix, message.Phase3SignalGroup);
                parameters.Add("@Phase4SignalGroup" + suffix, message.Phase4SignalGroup);
                parameters.Add("@Phase5SignalGroup" + suffix, message.Phase5SignalGroup);
                parameters.Add("@Phase0EventState" + suffix, message.Phase0EventState);
                parameters.Add("@Phase1EventState" + suffix, message.Phase1EventState);
                parameters.Add("@Phase2EventState" + suffix, message.Phase2EventState);
                parameters.Add("@Phase3EventState" + suffix, message.Phase3EventState);
                parameters.Add("@Phase4EventState" + suffix, message.Phase4EventState);
                parameters.Add("@Phase5EventState" + suffix, message.Phase5EventState);
                parameters.Add("@Phase0ConnectionManeuverAssistId0" + suffix, message.Phase0ConnectionManeuverAssistId0);
                parameters.Add("@Phase0ConnectionManeuverAssistId1" + suffix, message.Phase0ConnectionManeuverAssistId1);
                parameters.Add("@Phase1ConnectionManeuverAssistId0" + suffix, message.Phase1ConnectionManeuverAssistId0);
                parameters.Add("@Phase1ConnectionManeuverAssistId1" + suffix, message.Phase1ConnectionManeuverAssistId1);
                parameters.Add("@Phase2ConnectionManeuverAssistId0" + suffix, message.Phase2ConnectionManeuverAssistId0);
                parameters.Add("@Phase2ConnectionManeuverAssistId1" + suffix, message.Phase2ConnectionManeuverAssistId1);
                parameters.Add("@Phase3ConnectionManeuverAssistId0" + suffix, message.Phase3ConnectionManeuverAssistId0);
                parameters.Add("@Phase3ConnectionManeuverAssistId1" + suffix, message.Phase3ConnectionManeuverAssistId1);
                parameters.Add("@Phase4ConnectionManeuverAssistId0" + suffix, message.Phase4ConnectionManeuverAssistId0);
                parameters.Add("@Phase4ConnectionManeuverAssistId1" + suffix, message.Phase4ConnectionManeuverAssistId1);
                parameters.Add("@Phase5ConnectionManeuverAssistId0" + suffix, message.Phase5ConnectionManeuverAssistId0);
                parameters.Add("@Phase5ConnectionManeuverAssistId1" + suffix, message.Phase5ConnectionManeuverAssistId1);
                parameters.Add("@PublisherId" + suffix, message.PublisherId);
                parameters.Add("@IsSecureSigned" + suffix, message.IsSecureSigned);
                parameters.Add("@IsSecureEncrypted" + suffix, message.IsSecureEncrypted);
                parameters.Add("@SecurityProtocol" + suffix, message.SecurityProtocol);
                parameters.Add("@SignerId" + suffix, message.SignerId);
                parameters.Add("@CertificateId" + suffix, message.CertificateId);
            }

            await connection.ExecuteAsync(sql.ToString(), parameters, transaction);
        }
    }

    public async Task InsertSREMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SREM> messages)
    {
        if (messages.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < messages.Count; start += batchSize)
        {
            var chunk = messages.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();

            var sql = @"INSERT INTO srem_messages (packet_id, generation_time, station_id,
                intersection_name, intersection_id, latitude, longitude,
                requested_phase, vehicle_type, request_reason,
                request_id, requestor_id, required_accuracy,
                in_bound_lane_id, out_bound_lane_id, heading, speed, transmission_power,
                route_names, transit_schedule, requestor_name,
                is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id)
            VALUES ";

            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var message = chunk[i];
                var p = i.ToString();

                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @in{p}, @iid{p}, @lat{p}, @lon{p}, @rp{p}, @vt{p}, @rr{p}, @rid{p}, @reqid{p}, @ra{p}, @ibli{p}, @obli{p}, @h{p}, @s{p}, @tp{p}, @rn{p}, @ts{p}, @rname{p}, @issigned{p}, @isencrypted{p}, @sproto{p}, @signer{p}, @cert{p})");

                parameters.Add("@pid" + p, message.PacketId);
                parameters.Add("@gen" + p, message.GenerationTime);
                parameters.Add("@sid" + p, message.StationId);
                parameters.Add("@in" + p, message.IntersectionName);
                parameters.Add("@iid" + p, message.IntersectionId);
                parameters.Add("@lat" + p, message.Latitude);
                parameters.Add("@lon" + p, message.Longitude);
                parameters.Add("@rp" + p, message.RequestedPhase);
                parameters.Add("@vt" + p, message.VehicleType);
                parameters.Add("@rr" + p, message.RequestReason);
                parameters.Add("@rid" + p, message.RequestId);
                parameters.Add("@reqid" + p, message.RequestorId);
                parameters.Add("@ra" + p, message.RequiredAccuracy);
                parameters.Add("@ibli" + p, message.InBoundLaneId);
                parameters.Add("@obli" + p, message.OutBoundLaneId);
                parameters.Add("@h" + p, message.Heading);
                parameters.Add("@s" + p, message.Speed);
                parameters.Add("@tp" + p, message.TransmissionPower);
                parameters.Add("@rn" + p, message.RouteNames);
                parameters.Add("@ts" + p, message.TransitSchedule);
                parameters.Add("@rname" + p, message.RequestorName);
                parameters.Add("@issigned" + p, message.IsSecureSigned);
                parameters.Add("@isencrypted" + p, message.IsSecureEncrypted);
                parameters.Add("@sproto" + p, message.SecurityProtocol);
                parameters.Add("@signer" + p, message.SignerId);
                parameters.Add("@cert" + p, message.CertificateId);
            }

            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    public async Task InsertSSEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SSEM> messages)
    {
        if (messages.Count == 0) return;

        const int batchSize = 500;
        for (var start = 0; start < messages.Count; start += batchSize)
        {
            var chunk = messages.Skip(start).Take(batchSize).ToList();
            var parameters = new DynamicParameters();

            var sql = @"INSERT INTO ssem_messages (packet_id, generation_time, station_id,
                intersection_id, intersection_name, latitude, longitude,
                status_code, granted_duration, request_id_ref, responder_id, request_station_id_ref,
                is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id)
            VALUES ";

            var valueClauses = new List<string>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var message = chunk[i];
                var p = i.ToString();

                valueClauses.Add($"(@pid{p}, @gen{p}, @sid{p}, @iid{p}, @in{p}, @lat{p}, @lon{p}, @sc{p}, @gd{p}, @rid{p}, @rid2{p}, @rsid{p}, @issigned{p}, @isencrypted{p}, @sproto{p}, @signer{p}, @cert{p})");

                parameters.Add("@pid" + p, message.PacketId);
                parameters.Add("@gen" + p, message.GenerationTime);
                parameters.Add("@sid" + p, message.StationId);
                parameters.Add("@iid" + p, message.IntersectionId);
                parameters.Add("@in" + p, message.IntersectionName);
                parameters.Add("@lat" + p, message.Latitude);
                parameters.Add("@lon" + p, message.Longitude);
                parameters.Add("@sc" + p, string.IsNullOrWhiteSpace(message.StatusCode) ? "pending" : message.StatusCode);
                parameters.Add("@gd" + p, message.GrantedDuration);
                parameters.Add("@rid" + p, message.RequestIdRef);
                parameters.Add("@rid2" + p, message.ResponderId);
                parameters.Add("@rsid" + p, message.RequestStationIdRef);
                parameters.Add("@issigned" + p, message.IsSecureSigned);
                parameters.Add("@isencrypted" + p, message.IsSecureEncrypted);
                parameters.Add("@sproto" + p, message.SecurityProtocol);
                parameters.Add("@signer" + p, message.SignerId);
                parameters.Add("@cert" + p, message.CertificateId);
            }

            sql += string.Join(", ", valueClauses);
            await connection.ExecuteAsync(sql, parameters, transaction);
        }
    }

    public async Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
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

        await using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleAsync(sql);

        return new MessageCountsDto
        {
            TotalPackets = (int)row.total_packets,
            CAM = (int)row.cam,
            DENM = (int)row.denm,
            MAPEM = (int)row.mapem,
            SPATEM = (int)row.spatem,
            SREM = (int)row.srem,
            SSEM = (int)row.ssem,
            TotalCorrelations = (int)row.total_correlations
        };
    }

    public async Task<DistinctVehicleWindowStatsDto> GetDistinctVehicleWindowStatsAsync()
    {
        const string dedupCte = """
            WITH all_messages AS (
                SELECT station_id, generation_time FROM cam_messages
                UNION ALL
                SELECT station_id, generation_time FROM denm_messages
                UNION ALL
                SELECT station_id, generation_time FROM mapem_messages
                UNION ALL
                SELECT station_id, generation_time FROM spatem_messages
                UNION ALL
                SELECT station_id, generation_time FROM srem_messages
                UNION ALL
                SELECT station_id, generation_time FROM ssem_messages
            ),
            normalized AS (
                SELECT
                    TRIM(station_id) AS station_id,
                    generation_time
                FROM all_messages
                WHERE generation_time IS NOT NULL
                  AND station_id IS NOT NULL
                  AND NULLIF(TRIM(station_id), '') IS NOT NULL
            ),
            deduplicated AS (
                SELECT DISTINCT
                    station_id,
                    date_trunc('hour', generation_time)
                    + (((extract(minute FROM generation_time)::int / 15) * 15) * interval '1 minute') AS bucket_start
                FROM normalized
            )
            """;

        var totalSql = dedupCte + "SELECT COUNT(*) FROM deduplicated;";
        var bucketsSql = dedupCte + """
            SELECT
                bucket_start AS BucketStart,
                COUNT(*)::int AS DistinctVehicles
            FROM deduplicated
            GROUP BY bucket_start
            ORDER BY bucket_start DESC;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);

        var totalDistinctVehicleWindows = await connection.ExecuteScalarAsync<int>(totalSql);
        var buckets = (await connection.QueryAsync<DistinctVehicleWindowBucketDto>(bucketsSql)).ToList();

        return new DistinctVehicleWindowStatsDto
        {
            WindowMinutes = 15,
            TotalDistinctVehicleWindows = totalDistinctVehicleWindows,
            Buckets = buckets
        };
    }

    public async Task<MapVehicleFilterSummaryDto> GetMapVehicleFilterSummaryAsync(MapVehicleSummaryQueryParams filters)
    {
        var normalizedFilters = NormalizeMapVehicleSummaryFilters(filters);
        if (!normalizedFilters.IncludeCam && !normalizedFilters.IncludeDenm)
        {
            return new MapVehicleFilterSummaryDto
            {
                DistinctVehicleCount = 0,
                Scope = normalizedFilters.Scope,
                StationTypeShares = Array.Empty<StationTypeShareDto>()
            };
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = BuildMapVehicleSummarySql(normalizedFilters);
        var parameters = BuildMapVehicleSummaryParameters(normalizedFilters);
        using var reader = await connection.QueryMultipleAsync(sql, parameters);

        var distinctVehicleCount = await reader.ReadSingleAsync<int>();
        var stationTypeRows = (await reader.ReadAsync<StationTypeCountRow>()).ToList();

        var stationTypeShares = stationTypeRows
            .Select(row => new StationTypeShareDto
            {
                StationType = row.StationType,
                DistinctVehicles = row.DistinctVehicles,
                Percentage = distinctVehicleCount == 0
                    ? 0
                    : Math.Round(row.DistinctVehicles * 100d / distinctVehicleCount, 2)
            })
            .ToList();

        return new MapVehicleFilterSummaryDto
        {
            DistinctVehicleCount = distinctVehicleCount,
            Scope = normalizedFilters.Scope,
            StationTypeShares = stationTypeShares
        };
    }

    public Task<List<CAM>> GetCAMMessagesAsync(int? limit = null) => GetMessagesAsync<CAM>("cam_messages", limit);
    public Task<List<DENM>> GetDENMMessagesAsync(int? limit = null) => GetMessagesAsync<DENM>("denm_messages", limit);
    public Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null) => GetMessagesAsync<MAPEM>("mapem_messages", limit);
    public Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null) => GetMessagesAsync<SPATEM>("spatem_messages", limit);
    public Task<List<SREM>> GetSREMMessagesAsync(int? limit = null) => GetMessagesAsync<SREM>("srem_messages", limit);
    public Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null) => GetMessagesAsync<SSEM>("ssem_messages", limit);

    public async Task<V2XMessage?> GetV2XMessageByIdAsync(int id, string? messageType = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        if (!string.IsNullOrWhiteSpace(messageType))
        {
            return messageType.Trim().ToUpperInvariant() switch
            {
                "CAM" => await connection.QueryFirstOrDefaultAsync<CAM>("SELECT * FROM cam_messages WHERE id = @Id", new { Id = id }),
                "DENM" => await connection.QueryFirstOrDefaultAsync<DENM>("SELECT * FROM denm_messages WHERE id = @Id", new { Id = id }),
                "MAPEM" => await connection.QueryFirstOrDefaultAsync<MAPEM>("SELECT * FROM mapem_messages WHERE id = @Id", new { Id = id }),
                "SPATEM" => await connection.QueryFirstOrDefaultAsync<SPATEM>("SELECT * FROM spatem_messages WHERE id = @Id", new { Id = id }),
                "SREM" => await connection.QueryFirstOrDefaultAsync<SREM>("SELECT * FROM srem_messages WHERE id = @Id", new { Id = id }),
                "SSEM" => await connection.QueryFirstOrDefaultAsync<SSEM>("SELECT * FROM ssem_messages WHERE id = @Id", new { Id = id }),
                _ => throw new ArgumentException($"Unsupported messageType '{messageType}'.", nameof(messageType))
            };
        }

        var hasCam = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM cam_messages WHERE id = @Id)", new { Id = id });
        var hasDenm = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM denm_messages WHERE id = @Id)", new { Id = id });
        var hasMapem = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM mapem_messages WHERE id = @Id)", new { Id = id });
        var hasSpatem = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM spatem_messages WHERE id = @Id)", new { Id = id });
        var hasSrem = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM srem_messages WHERE id = @Id)", new { Id = id });
        var hasSsem = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM ssem_messages WHERE id = @Id)", new { Id = id });

        var matchCount = (hasCam ? 1 : 0)
            + (hasDenm ? 1 : 0)
            + (hasMapem ? 1 : 0)
            + (hasSpatem ? 1 : 0)
            + (hasSrem ? 1 : 0)
            + (hasSsem ? 1 : 0);

        if (matchCount == 0)
        {
            return null;
        }

        if (matchCount > 1)
        {
            throw new InvalidOperationException(
                $"Message id '{id}' is ambiguous across message tables. Specify messageType.");
        }

        if (hasCam)
        {
            return await connection.QueryFirstOrDefaultAsync<CAM>(
                "SELECT * FROM cam_messages WHERE id = @Id", new { Id = id });
        }

        if (hasDenm)
        {
            return await connection.QueryFirstOrDefaultAsync<DENM>(
                "SELECT * FROM denm_messages WHERE id = @Id", new { Id = id });
        }

        if (hasMapem)
        {
            return await connection.QueryFirstOrDefaultAsync<MAPEM>(
                "SELECT * FROM mapem_messages WHERE id = @Id", new { Id = id });
        }

        if (hasSpatem)
        {
            return await connection.QueryFirstOrDefaultAsync<SPATEM>(
                "SELECT * FROM spatem_messages WHERE id = @Id", new { Id = id });
        }

        if (hasSrem)
        {
            return await connection.QueryFirstOrDefaultAsync<SREM>(
                "SELECT * FROM srem_messages WHERE id = @Id", new { Id = id });
        }

        return await connection.QueryFirstOrDefaultAsync<SSEM>(
            "SELECT * FROM ssem_messages WHERE id = @Id", new { Id = id });
    }

    private async Task<List<TMessage>> GetMessagesAsync<TMessage>(string tableName, int? limit)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var query = $"SELECT * FROM {tableName} ORDER BY generation_time DESC";

        var parameters = new DynamicParameters();
        if (limit.HasValue)
        {
            query += " LIMIT @Limit";
            parameters.Add("@Limit", limit.Value);
        }

        var messages = await connection.QueryAsync<TMessage>(query, parameters);
        return messages.ToList();
    }

    private static NormalizedMapVehicleSummaryFilters NormalizeMapVehicleSummaryFilters(MapVehicleSummaryQueryParams? filters)
    {
        var normalizedLayers = (filters?.VisibleLayers ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var includeCam = filters?.VisibleLayers is null || normalizedLayers.Contains(TileLayerNames.Cam);
        var includeDenm = filters?.VisibleLayers is null || normalizedLayers.Contains(TileLayerNames.Denm);

        var stationTypes = filters?.StationTypes?
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();

        if (stationTypes is { Length: 0 })
        {
            stationTypes = null;
        }

        var vehicleRoles = filters?.VehicleRoles?
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (vehicleRoles is { Length: 0 })
        {
            vehicleRoles = null;
        }

        var hasBounds = filters?.MinLatitude.HasValue == true
            && filters.MaxLatitude.HasValue
            && filters.MinLongitude.HasValue
            && filters.MaxLongitude.HasValue;

        var hasTile = filters?.TileZ.HasValue == true
            && filters.TileX.HasValue
            && filters.TileY.HasValue;

        var scope = hasBounds
            ? "Viewport"
            : hasTile
                ? "Tile"
                : "Global";

        return new NormalizedMapVehicleSummaryFilters(
            IncludeCam: includeCam,
            IncludeDenm: includeDenm,
            FromTime: filters?.FromTime,
            ToTime: filters?.ToTime,
            IsSecureSigned: filters?.IsSecureSigned,
            IsSecureEncrypted: filters?.IsSecureEncrypted,
            StationTypes: stationTypes,
            VehicleRoles: vehicleRoles,
            MinLatitude: hasBounds ? filters!.MinLatitude : null,
            MaxLatitude: hasBounds ? filters!.MaxLatitude : null,
            MinLongitude: hasBounds ? filters!.MinLongitude : null,
            MaxLongitude: hasBounds ? filters!.MaxLongitude : null,
            TileZ: hasTile ? filters!.TileZ : null,
            TileX: hasTile ? filters!.TileX : null,
            TileY: hasTile ? filters!.TileY : null,
            Scope: scope);
    }

    private static string BuildMapVehicleSummarySql(NormalizedMapVehicleSummaryFilters filters)
    {
        var sourceQueries = new List<string>();

        if (filters.IncludeCam)
        {
            sourceQueries.Add(BuildCamVehicleSummarySourceSql());
        }

        if (filters.IncludeDenm)
        {
            sourceQueries.Add(BuildDenmVehicleSummarySourceSql());
        }

        var unionSql = string.Join("\n                UNION ALL\n", sourceQueries);
        var commonCte = $"""
            WITH spatial_scope AS (
                SELECT
                    CASE
                        WHEN @UseBounds::boolean THEN ST_Transform(ST_MakeEnvelope(@MinLongitude::double precision, @MinLatitude::double precision, @MaxLongitude::double precision, @MaxLatitude::double precision, 4326), 3857)
                        WHEN @UseTile::boolean THEN ST_TileEnvelope(@TileZ::integer, @TileX::integer, @TileY::integer)
                        ELSE NULL
                    END AS geom
            ),
            filtered_rows AS (
                {unionSql}
            ),
            ranked_rows AS (
                SELECT
                    station_id,
                    station_type,
                    generation_time,
                    ROW_NUMBER() OVER (PARTITION BY station_id ORDER BY generation_time DESC) AS rn
                FROM filtered_rows
            ),
            latest_per_station AS (
                SELECT station_id, station_type
                FROM ranked_rows
                WHERE rn = 1
            )
            """;

        return $"""
            {commonCte}
            SELECT COUNT(*)::int
            FROM latest_per_station;

            {commonCte}
            SELECT
                station_type AS StationType,
                COUNT(*)::int AS DistinctVehicles
            FROM latest_per_station
            WHERE station_type IS NOT NULL
            GROUP BY station_type
            ORDER BY station_type;
            """;
    }

    private static string BuildCamVehicleSummarySourceSql()
    {
        return """
            SELECT
                TRIM(station_id) AS station_id,
                station_type,
                generation_time
            FROM cam_messages
            CROSS JOIN spatial_scope
            WHERE station_id IS NOT NULL
              AND NULLIF(TRIM(station_id), '') IS NOT NULL
              AND latitude IS NOT NULL
              AND longitude IS NOT NULL
              AND latitude BETWEEN -85 AND 85
              AND longitude BETWEEN -180 AND 180
              AND (@FromTime::timestamp IS NULL OR generation_time >= @FromTime::timestamp)
              AND (@ToTime::timestamp IS NULL OR generation_time <= @ToTime::timestamp)
              AND (@IsSecureSigned::boolean IS NULL OR is_secure_signed = @IsSecureSigned::boolean)
              AND (@IsSecureEncrypted::boolean IS NULL OR is_secure_encrypted = @IsSecureEncrypted::boolean)
              AND (@StationTypes::integer[] IS NULL OR station_type = ANY(@StationTypes::integer[]))
              AND (@VehicleRoles::text[] IS NULL OR vehicle_role = ANY(@VehicleRoles::text[]))
              AND (
                  spatial_scope.geom IS NULL
                  OR ST_Intersects(
                      ST_Transform(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), 3857),
                      spatial_scope.geom
                  )
              )
            """;
    }

    private static string BuildDenmVehicleSummarySourceSql()
    {
        return """
            SELECT
                TRIM(station_id) AS station_id,
                station_type,
                generation_time
            FROM denm_messages
            CROSS JOIN spatial_scope
            WHERE station_id IS NOT NULL
              AND NULLIF(TRIM(station_id), '') IS NOT NULL
              AND latitude IS NOT NULL
              AND longitude IS NOT NULL
              AND latitude BETWEEN -85 AND 85
              AND longitude BETWEEN -180 AND 180
              AND (@FromTime::timestamp IS NULL OR generation_time >= @FromTime::timestamp)
              AND (@ToTime::timestamp IS NULL OR generation_time <= @ToTime::timestamp)
              AND (@IsSecureSigned::boolean IS NULL OR is_secure_signed = @IsSecureSigned::boolean)
              AND (@IsSecureEncrypted::boolean IS NULL OR is_secure_encrypted = @IsSecureEncrypted::boolean)
              AND (@StationTypes::integer[] IS NULL OR station_type = ANY(@StationTypes::integer[]))
              AND (
                  spatial_scope.geom IS NULL
                  OR ST_Intersects(
                      ST_Transform(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), 3857),
                      spatial_scope.geom
                  )
              )
            """;
    }

    private static DynamicParameters BuildMapVehicleSummaryParameters(NormalizedMapVehicleSummaryFilters filters)
    {
        var parameters = new DynamicParameters();
        parameters.Add("FromTime", filters.FromTime);
        parameters.Add("ToTime", filters.ToTime);
        parameters.Add("IsSecureSigned", filters.IsSecureSigned);
        parameters.Add("IsSecureEncrypted", filters.IsSecureEncrypted);
        parameters.Add("StationTypes", filters.StationTypes);
        parameters.Add("VehicleRoles", filters.VehicleRoles);
        parameters.Add("UseBounds", filters.MinLatitude.HasValue && filters.MaxLatitude.HasValue && filters.MinLongitude.HasValue && filters.MaxLongitude.HasValue);
        parameters.Add("MinLatitude", filters.MinLatitude);
        parameters.Add("MaxLatitude", filters.MaxLatitude);
        parameters.Add("MinLongitude", filters.MinLongitude);
        parameters.Add("MaxLongitude", filters.MaxLongitude);
        parameters.Add("UseTile", filters.TileZ.HasValue && filters.TileX.HasValue && filters.TileY.HasValue);
        parameters.Add("TileZ", filters.TileZ);
        parameters.Add("TileX", filters.TileX);
        parameters.Add("TileY", filters.TileY);
        return parameters;
    }

    private static (int PageNumber, int PageSize, int Offset) NormalizePaging(int pageNumber, int pageSize, int maxPageSize = 100)
    {
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize < 1 ? 25 : Math.Min(pageSize, maxPageSize);
        var offset = (safePageNumber - 1) * safePageSize;
        return (safePageNumber, safePageSize, offset);
    }

    private sealed class StationTypeCountRow
    {
        public int StationType { get; set; }

        public int DistinctVehicles { get; set; }
    }

    private sealed record NormalizedMapVehicleSummaryFilters(
        bool IncludeCam,
        bool IncludeDenm,
        DateTime? FromTime,
        DateTime? ToTime,
        bool? IsSecureSigned,
        bool? IsSecureEncrypted,
        int[]? StationTypes,
        string[]? VehicleRoles,
        double? MinLatitude,
        double? MaxLatitude,
        double? MinLongitude,
        double? MaxLongitude,
        int? TileZ,
        int? TileX,
        int? TileY,
        string Scope);
}
