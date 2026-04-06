using System.Data;
using Dapper;
using Npgsql;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public sealed class ProcessedFileRepository : IProcessedFileRepository
{
    private readonly string _connectionString;

    public ProcessedFileRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("DefaultConnection connection string not found");
    }

    public async Task<bool> IsFileHashProcessedAsync(string fileHash)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM processed_files
                WHERE file_hash = @FileHash
            );";

        return await connection.ExecuteScalarAsync<bool>(sql, new { FileHash = fileHash });
    }

    public async Task<bool> TryInsertProcessedFileAsync(IDbConnection connection, IDbTransaction transaction, ProcessedFileRecord record)
    {
        const string sql = @"
            INSERT INTO processed_files (
                file_name,
                file_size,
                file_hash,
                packet_count,
                status,
                processed_at
            )
            VALUES (
                @FileName,
                @FileSize,
                @FileHash,
                @PacketCount,
                @Status,
                CURRENT_TIMESTAMP
            )
            ON CONFLICT (file_hash) DO NOTHING;";

        var affectedRows = await connection.ExecuteAsync(sql, new
        {
            record.FileName,
            record.FileSize,
            record.FileHash,
            record.PacketCount,
            record.Status
        }, transaction);

        return affectedRows > 0;
    }
}