using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public interface IMapTileRepository
{
    Task<byte[]> GetCamTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);

    Task<byte[]> GetDenmTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);

    Task<byte[]> GetMapemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);

    Task<byte[]> GetSpatemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);

    Task<byte[]> GetSremTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);

    Task<byte[]> GetSsemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);

    Task<byte[]> GetCorrelationsTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);
}