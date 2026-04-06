using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.MapTile;

public sealed class MapTileService : IMapTileService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly string[] TileLayers =
    [
        TileLayerNames.Cam,
        TileLayerNames.Denm,
        TileLayerNames.Mapem,
        TileLayerNames.Spatem,
        TileLayerNames.Srem,
        TileLayerNames.Ssem,
        TileLayerNames.Correlations
    ];

    private readonly IMemoryCache _memoryCache;
    private readonly IMapTileRepository _repository;
    private readonly ConcurrentDictionary<string, int> _layerGenerations = new(StringComparer.OrdinalIgnoreCase);

    public MapTileService(IMapTileRepository repository, IMemoryCache memoryCache)
    {
        _repository = repository;
        _memoryCache = memoryCache;

        foreach (var layer in TileLayers)
        {
            _layerGenerations.TryAdd(layer, 0);
        }
    }

    public async Task<byte[]> GetTileAsync(string layer, int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
    {
        ValidateTileCoordinates(z, x, y);

        var normalizedLayer = NormalizeLayer(layer);
        var generation = _layerGenerations.GetOrAdd(normalizedLayer, 0);
        var cacheKey = BuildCacheKey(normalizedLayer, z, x, y, filters, generation);

        if (_memoryCache.TryGetValue(cacheKey, out byte[]? cachedTile) && cachedTile is not null)
        {
            return cachedTile;
        }

        var tile = normalizedLayer switch
        {
            TileLayerNames.Cam => await _repository.GetCamTileAsync(z, x, y, filters, cancellationToken),
            TileLayerNames.Denm => await _repository.GetDenmTileAsync(z, x, y, filters, cancellationToken),
            TileLayerNames.Mapem => await _repository.GetMapemTileAsync(z, x, y, filters, cancellationToken),
            TileLayerNames.Spatem => await _repository.GetSpatemTileAsync(z, x, y, filters, cancellationToken),
            TileLayerNames.Srem => await _repository.GetSremTileAsync(z, x, y, filters, cancellationToken),
            TileLayerNames.Ssem => await _repository.GetSsemTileAsync(z, x, y, filters, cancellationToken),
            TileLayerNames.Correlations => await _repository.GetCorrelationsTileAsync(z, x, y, filters, cancellationToken),
            _ => throw new ArgumentException($"Unsupported tile layer '{layer}'.", nameof(layer))
        };

        var cachedValue = tile.Length == 0 ? Array.Empty<byte>() : tile;
        _memoryCache.Set(
            cacheKey,
            cachedValue,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = CacheTtl,
                Size = Math.Max(1, cachedValue.Length)
            });

        return cachedValue;
    }

    public TileCacheDiagnosticsDto GetCacheDiagnostics()
    {
        var snapshot = _layerGenerations.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var layer in TileLayers)
        {
            if (!snapshot.ContainsKey(layer))
            {
                snapshot[layer] = 0;
            }
        }

        return new TileCacheDiagnosticsDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            LayerGenerations = snapshot
        };
    }

    public void InvalidateAllTiles()
    {
        foreach (var layer in TileLayers)
        {
            _layerGenerations.AddOrUpdate(layer, 1, (_, currentGeneration) => checked(currentGeneration + 1));
        }
    }

    public void InvalidateLayer(string layer)
    {
        var normalizedLayer = NormalizeLayer(layer);
        _layerGenerations.AddOrUpdate(normalizedLayer, 1, (_, currentGeneration) => checked(currentGeneration + 1));
    }

    private static void ValidateTileCoordinates(int z, int x, int y)
    {
        if (z < 0 || z > 22)
        {
            throw new ArgumentOutOfRangeException(nameof(z), z, "Tile zoom must be between 0 and 22.");
        }

        var maxIndex = (1 << z) - 1;
        if (x < 0 || x > maxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, $"Tile x must be between 0 and {maxIndex} for zoom level {z}.");
        }

        if (y < 0 || y > maxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Tile y must be between 0 and {maxIndex} for zoom level {z}.");
        }
    }

    private static string NormalizeLayer(string layer)
    {
        if (!TileLayerNames.IsValid(layer))
        {
            throw new ArgumentException($"Unsupported tile layer '{layer}'.", nameof(layer));
        }

        var normalized = TileLayerNames.Normalize(layer);
        return normalized switch
        {
            TileLayerNames.Cam => TileLayerNames.Cam,
            TileLayerNames.Denm => TileLayerNames.Denm,
            TileLayerNames.Mapem => TileLayerNames.Mapem,
            TileLayerNames.Spatem => TileLayerNames.Spatem,
            TileLayerNames.Srem => TileLayerNames.Srem,
            TileLayerNames.Ssem => TileLayerNames.Ssem,
            _ => TileLayerNames.Correlations
        };
    }

    private static string BuildCacheKey(string layer, int z, int x, int y, TileFilterParams filters, int layerGeneration)
    {
        var normalizedFilters = new
        {
            layer,
            z,
            x,
            y,
            layerGeneration,
            fromTime = filters.FromTime?.ToString("O", CultureInfo.InvariantCulture),
            toTime = filters.ToTime?.ToString("O", CultureInfo.InvariantCulture),
            isSecureSigned = filters.IsSecureSigned,
            isSecureEncrypted = filters.IsSecureEncrypted,
            stationTypes = filters.StationTypes?.Distinct().OrderBy(value => value).ToArray(),
            vehicleRoles = NormalizeVehicleRoles(filters.VehicleRoles)
        };

        var json = JsonSerializer.Serialize(normalizedFilters);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
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
}