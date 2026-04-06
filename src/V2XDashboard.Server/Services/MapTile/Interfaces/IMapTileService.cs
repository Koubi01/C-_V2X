using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.MapTile.Interfaces;

public interface IMapTileService
{
    Task<byte[]> GetTileAsync(string layer, int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default);
    TileCacheDiagnosticsDto GetCacheDiagnostics();
    void InvalidateAllTiles();
    void InvalidateLayer(string layer);
}