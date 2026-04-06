using System.Data;
using Dapper;
using Npgsql;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class PacketRepository : IPacketRepository
{
    private readonly string _connectionString;

    public PacketRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public async Task InsertPacketsAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<Packet> packets)
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
            sql.AppendLine("INSERT INTO packets (timestamp, source_mac, destination_mac, packet_type, length, source_port, destination_port, is_secure_signed, is_secure_encrypted, security_protocol, signer_id, certificate_id, payload, pcap_file_name)");
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

                sql.Append($"(@Timestamp{suffix}, @SourceMac{suffix}, @DestinationMac{suffix}, @PacketType{suffix}, @Length{suffix}, @SourcePort{suffix}, @DestinationPort{suffix}, @IsSecureSigned{suffix}, @IsSecureEncrypted{suffix}, @SecurityProtocol{suffix}, @SignerId{suffix}, @CertificateId{suffix}, @Payload{suffix}, @PcapFileName{suffix})");

                parameters.Add($"@Timestamp{suffix}", packet.Timestamp);
                parameters.Add($"@SourceMac{suffix}", packet.SourceMac);
                parameters.Add($"@DestinationMac{suffix}", packet.DestinationMac);
                parameters.Add($"@PacketType{suffix}", packet.PacketType);
                parameters.Add($"@Length{suffix}", packet.Length);
                parameters.Add($"@SourcePort{suffix}", packet.SourcePort);
                parameters.Add($"@DestinationPort{suffix}", packet.DestinationPort);
                parameters.Add($"@IsSecureSigned{suffix}", packet.IsSecureSigned);
                parameters.Add($"@IsSecureEncrypted{suffix}", packet.IsSecureEncrypted);
                parameters.Add($"@SecurityProtocol{suffix}", packet.SecurityProtocol);
                parameters.Add($"@SignerId{suffix}", packet.SignerId);
                parameters.Add($"@CertificateId{suffix}", packet.CertificateId);
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

    public async Task<List<Packet>> GetPacketsAsync(
        string? filter = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        string? signerId = null,
        int? limit = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var query = "SELECT * FROM packets";
        var parameters = new DynamicParameters();
        var whereClauses = new List<string>();

        if (!string.IsNullOrEmpty(filter))
        {
            whereClauses.Add("packet_type = @Filter");
            parameters.Add("@Filter", filter);
        }

        if (isSecureSigned.HasValue)
        {
            whereClauses.Add("is_secure_signed = @IsSecureSigned");
            parameters.Add("@IsSecureSigned", isSecureSigned.Value);
        }

        if (isSecureEncrypted.HasValue)
        {
            whereClauses.Add("is_secure_encrypted = @IsSecureEncrypted");
            parameters.Add("@IsSecureEncrypted", isSecureEncrypted.Value);
        }

        if (!string.IsNullOrWhiteSpace(signerId))
        {
            whereClauses.Add("signer_id ILIKE @SignerId");
            parameters.Add("@SignerId", $"%{signerId}%");
        }

        if (whereClauses.Count > 0)
        {
            query += " WHERE " + string.Join(" AND ", whereClauses);
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

    public async Task<PagedResult<Packet>> GetPacketsPagedAsync(
        string? filter = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        string? signerId = null,
        int pageNumber = 1,
        int pageSize = 25)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var (safePageNumber, safePageSize, offset) = NormalizePaging(pageNumber, pageSize);

        var parameters = new DynamicParameters();
        var whereClauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            whereClauses.Add(@"(packet_type ILIKE @LikeFilter
                    OR protocol ILIKE @LikeFilter
                    OR pcap_file_name ILIKE @LikeFilter
                    OR source_ip ILIKE @LikeFilter
                    OR destination_ip ILIKE @LikeFilter)");
            parameters.Add("@LikeFilter", $"%{filter}%");
        }

        if (isSecureSigned.HasValue)
        {
            whereClauses.Add("is_secure_signed = @IsSecureSigned");
            parameters.Add("@IsSecureSigned", isSecureSigned.Value);
        }

        if (isSecureEncrypted.HasValue)
        {
            whereClauses.Add("is_secure_encrypted = @IsSecureEncrypted");
            parameters.Add("@IsSecureEncrypted", isSecureEncrypted.Value);
        }

        if (!string.IsNullOrWhiteSpace(signerId))
        {
            whereClauses.Add("signer_id ILIKE @SignerId");
            parameters.Add("@SignerId", $"%{signerId}%");
        }

        var whereClause = whereClauses.Count == 0
            ? string.Empty
            : " WHERE " + string.Join(" AND ", whereClauses);

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

    public async Task<Packet?> GetPacketByIdAsync(int id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Packet>(
            "SELECT * FROM packets WHERE id = @Id", new { Id = id });
    }

    public async Task<SecurityMetadataSummaryDto> GetSecurityMetadataSummaryAsync()
    {
        const string sql = @"
            SELECT
                COUNT(*)::int AS total_packets,
                COUNT(*) FILTER (WHERE is_secure_signed)::int AS signed_packets,
                COUNT(*) FILTER (WHERE is_secure_encrypted)::int AS encrypted_packets,
                COUNT(*) FILTER (WHERE is_secure_signed OR is_secure_encrypted)::int AS secure_packets,
                COUNT(DISTINCT NULLIF(TRIM(signer_id), ''))::int AS distinct_signers,
                COUNT(DISTINCT NULLIF(TRIM(certificate_id), ''))::int AS distinct_certificates
            FROM packets;";

        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleAsync<SecurityMetadataSummaryDto>(sql);
    }

    private static (int PageNumber, int PageSize, int Offset) NormalizePaging(int pageNumber, int pageSize, int maxPageSize = 100)
    {
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize < 1 ? 25 : Math.Min(pageSize, maxPageSize);
        var offset = (safePageNumber - 1) * safePageSize;
        return (safePageNumber, safePageSize, offset);
    }
}
