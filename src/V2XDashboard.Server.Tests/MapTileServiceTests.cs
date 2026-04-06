using Microsoft.Extensions.Caching.Memory;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Server.Services.MapTile;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public sealed class MapTileServiceTests
{
    [Fact]
    public async Task GetTileAsync_SameRequest_UsesCache()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000_000 });
        var repository = new CountingMapTileRepository();
        var service = new MapTileService(repository, memoryCache);
        var filters = new TileFilterParams();

        var first = await service.GetTileAsync(TileLayerNames.Cam, 0, 0, 0, filters);
        var second = await service.GetTileAsync(TileLayerNames.Cam, 0, 0, 0, filters);

        Assert.Equal(1, repository.CamCalls);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetTileAsync_AfterLayerInvalidation_RefetchesTile()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000_000 });
        var repository = new CountingMapTileRepository();
        var service = new MapTileService(repository, memoryCache);
        var filters = new TileFilterParams();

        _ = await service.GetTileAsync(TileLayerNames.Cam, 0, 0, 0, filters);
        service.InvalidateLayer(TileLayerNames.Cam);
        _ = await service.GetTileAsync(TileLayerNames.Cam, 0, 0, 0, filters);

        Assert.Equal(2, repository.CamCalls);
    }

    [Fact]
    public async Task GetTileAsync_AfterGlobalInvalidation_RefetchesTile()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000_000 });
        var repository = new CountingMapTileRepository();
        var service = new MapTileService(repository, memoryCache);
        var filters = new TileFilterParams();

        _ = await service.GetTileAsync(TileLayerNames.Denm, 0, 0, 0, filters);
        service.InvalidateAllTiles();
        _ = await service.GetTileAsync(TileLayerNames.Denm, 0, 0, 0, filters);

        Assert.Equal(2, repository.DenmCalls);
    }

    [Fact]
    public async Task GetTileAsync_VehicleRoleOrderNormalization_ReusesCacheKey()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000_000 });
        var repository = new CountingMapTileRepository();
        var service = new MapTileService(repository, memoryCache);

        var firstFilters = new TileFilterParams
        {
            VehicleRoles = ["truck", "passenger"]
        };

        var secondFilters = new TileFilterParams
        {
            VehicleRoles = ["passenger", "truck"]
        };

        _ = await service.GetTileAsync(TileLayerNames.Cam, 0, 0, 0, firstFilters);
        _ = await service.GetTileAsync(TileLayerNames.Cam, 0, 0, 0, secondFilters);

        Assert.Equal(1, repository.CamCalls);
    }

    [Fact]
    public void GetCacheDiagnostics_AfterInvalidation_ReflectsGenerationIncrements()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000_000 });
        var repository = new CountingMapTileRepository();
        var service = new MapTileService(repository, memoryCache);

        var before = service.GetCacheDiagnostics();
        service.InvalidateAllTiles();
        service.InvalidateLayer(TileLayerNames.Cam);
        var after = service.GetCacheDiagnostics();

        Assert.Equal(before.LayerGenerations[TileLayerNames.Denm] + 1, after.LayerGenerations[TileLayerNames.Denm]);
        Assert.Equal(before.LayerGenerations[TileLayerNames.Cam] + 2, after.LayerGenerations[TileLayerNames.Cam]);
    }

    private sealed class CountingMapTileRepository : IMapTileRepository
    {
        public int CamCalls { get; private set; }
        public int DenmCalls { get; private set; }

        public Task<byte[]> GetCamTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
        {
            CamCalls++;
            return Task.FromResult(new byte[] { 0x01, 0x02, 0x03 });
        }

        public Task<byte[]> GetDenmTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
        {
            DenmCalls++;
            return Task.FromResult(new byte[] { 0x01, 0x02 });
        }

        public Task<byte[]> GetMapemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> GetSpatemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> GetSremTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> GetSsemTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> GetCorrelationsTileAsync(int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
    }
}
