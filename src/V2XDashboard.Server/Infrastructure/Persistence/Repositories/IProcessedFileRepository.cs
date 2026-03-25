using System.Data;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public interface IProcessedFileRepository
{
    Task<bool> IsFileHashProcessedAsync(string fileHash);
    Task<bool> TryInsertProcessedFileAsync(IDbConnection connection, IDbTransaction transaction, ProcessedFileRecord record);
}

public sealed class ProcessedFileRecord
{
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string FileHash { get; init; } = string.Empty;
    public int PacketCount { get; init; }
    public string Status { get; init; } = "Processed";
}