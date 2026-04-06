using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using V2XDashboard.Server;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public sealed class TileDiagnosticsEndpointTests : IClassFixture<Phase0SmokeTests.TestAppFactory>
{
    private readonly Phase0SmokeTests.TestAppFactory _factory;

    public TileDiagnosticsEndpointTests(Phase0SmokeTests.TestAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DiagnosticsCache_WhenProductionAndFlagDisabled_ReturnsNotFound()
    {
        var client = CreateClient("Production", diagnosticsEnabled: false);

        var response = await client.GetAsync("/api/tiles/diagnostics/cache");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsCache_WhenProductionAndFlagEnabled_ReturnsOk()
    {
        var client = CreateClient("Production", diagnosticsEnabled: true);

        var response = await client.GetAsync("/api/tiles/diagnostics/cache");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsInvalidate_WithInvalidLayer_ReturnsBadRequest()
    {
        var client = CreateClient("Production", diagnosticsEnabled: true);

        var response = await client.PostAsync("/api/tiles/diagnostics/cache/invalidate?layer=invalid", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsInvalidate_WithCamLayer_ReturnsOk()
    {
        var client = CreateClient("Production", diagnosticsEnabled: true);

        var response = await client.PostAsync($"/api/tiles/diagnostics/cache/invalidate?layer={TileLayerNames.Cam}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsInvalidateAll_IncrementsAllLayerGenerations()
    {
        var client = CreateClient("Production", diagnosticsEnabled: true, useStatefulMapTileService: true);

        var before = await client.GetFromJsonAsync<TileCacheDiagnosticsDto>("/api/tiles/diagnostics/cache");
        var invalidateResponse = await client.PostAsync("/api/tiles/diagnostics/cache/invalidate", null);
        var after = await invalidateResponse.Content.ReadFromJsonAsync<TileCacheInvalidateResultDto>();

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.OK, invalidateResponse.StatusCode);
        Assert.NotNull(after);

        foreach (var key in before!.LayerGenerations.Keys)
        {
            Assert.Equal(before.LayerGenerations[key] + 1, after!.LayerGenerations[key]);
        }
    }

    [Fact]
    public async Task DiagnosticsInvalidateCam_IncrementsOnlyCamGeneration()
    {
        var client = CreateClient("Production", diagnosticsEnabled: true, useStatefulMapTileService: true);

        var before = await client.GetFromJsonAsync<TileCacheDiagnosticsDto>("/api/tiles/diagnostics/cache");
        var invalidateResponse = await client.PostAsync($"/api/tiles/diagnostics/cache/invalidate?layer={TileLayerNames.Cam}", null);
        var after = await invalidateResponse.Content.ReadFromJsonAsync<TileCacheInvalidateResultDto>();

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.OK, invalidateResponse.StatusCode);
        Assert.NotNull(after);

        foreach (var key in before!.LayerGenerations.Keys)
        {
            var expected = string.Equals(key, TileLayerNames.Cam, StringComparison.OrdinalIgnoreCase)
                ? before.LayerGenerations[key] + 1
                : before.LayerGenerations[key];

            Assert.Equal(expected, after!.LayerGenerations[key]);
        }
    }

    private HttpClient CreateClient(string environmentName, bool diagnosticsEnabled, bool useStatefulMapTileService = false)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environmentName);
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["TileDiagnostics:Enabled"] = diagnosticsEnabled ? "true" : "false"
                    });
                });

                if (useStatefulMapTileService)
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IMapTileService>();
                        services.AddSingleton<IMapTileService, StatefulMapTileService>();
                    });
                }
            })
            .CreateClient();
    }

    private sealed class StatefulMapTileService : IMapTileService
    {
        private readonly Dictionary<string, int> _generations = new(StringComparer.OrdinalIgnoreCase)
        {
            [TileLayerNames.Cam] = 0,
            [TileLayerNames.Denm] = 0,
            [TileLayerNames.Mapem] = 0,
            [TileLayerNames.Spatem] = 0,
            [TileLayerNames.Srem] = 0,
            [TileLayerNames.Ssem] = 0,
            [TileLayerNames.Correlations] = 0
        };

        public Task<byte[]> GetTileAsync(string layer, int z, int x, int y, TileFilterParams filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public TileCacheDiagnosticsDto GetCacheDiagnostics()
            => new()
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                LayerGenerations = new Dictionary<string, int>(_generations, StringComparer.OrdinalIgnoreCase)
            };

        public void InvalidateAllTiles()
        {
            foreach (var key in _generations.Keys.ToList())
            {
                _generations[key]++;
            }
        }

        public void InvalidateLayer(string layer)
        {
            var normalizedLayer = TileLayerNames.Normalize(layer);
            if (!_generations.ContainsKey(normalizedLayer))
            {
                throw new ArgumentException($"Unsupported tile layer '{layer}'.", nameof(layer));
            }

            _generations[normalizedLayer]++;
        }
    }
}
