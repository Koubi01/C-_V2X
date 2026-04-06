using Microsoft.AspNetCore.Mvc;
using V2XDashboard.Server.Api;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Api.Endpoints;

public static class MapTileEndpoints
{
    public static IEndpointRouteBuilder MapMapTileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tiles").WithTags("Map Tiles");

        group.MapGet("/{layer}/{z:int}/{x:int}/{y:int}", async (
            string layer,
            int z,
            int x,
            int y,
            [AsParameters] TileFilterParams filters,
            IMapTileService tileService,
            CancellationToken cancellationToken) =>
        {
            var layerValidation = ValidateLayer(layer);
            if (layerValidation is not null)
            {
                return layerValidation;
            }

            var tileValidation = ValidateTileCoordinates(z, x, y);
            if (tileValidation is not null)
            {
                return tileValidation;
            }

            var dateRangeValidation = ApiRequestValidation.ValidateDateRange(filters.FromTime, filters.ToTime);
            if (dateRangeValidation is not null)
            {
                return dateRangeValidation;
            }

            var tile = await tileService.GetTileAsync(layer, z, x, y, filters, cancellationToken);
            return Results.File(tile, contentType: "application/x-protobuf");
        });

        group.MapGet("/diagnostics/cache", (IMapTileService tileService, IHostEnvironment environment, IConfiguration configuration) =>
        {
            if (!IsDiagnosticsEnabled(environment, configuration))
            {
                return Results.NotFound();
            }

            var diagnostics = tileService.GetCacheDiagnostics();
            return Results.Ok(diagnostics);
        });

        group.MapPost("/diagnostics/cache/invalidate", (
            [FromQuery] string? layer,
            IMapTileService tileService,
            IHostEnvironment environment,
            IConfiguration configuration) =>
        {
            if (!IsDiagnosticsEnabled(environment, configuration))
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(layer))
            {
                tileService.InvalidateAllTiles();

                return Results.Ok(new TileCacheInvalidateResultDto
                {
                    Scope = "All",
                    Layer = null,
                    InvalidatedAt = DateTimeOffset.UtcNow,
                    LayerGenerations = tileService.GetCacheDiagnostics().LayerGenerations
                });
            }

            var layerValidation = ValidateLayer(layer);
            if (layerValidation is not null)
            {
                return layerValidation;
            }

            var normalizedLayer = TileLayerNames.Normalize(layer);
            tileService.InvalidateLayer(normalizedLayer);

            return Results.Ok(new TileCacheInvalidateResultDto
            {
                Scope = "Layer",
                Layer = normalizedLayer,
                InvalidatedAt = DateTimeOffset.UtcNow,
                LayerGenerations = tileService.GetCacheDiagnostics().LayerGenerations
            });
        });

        return app;
    }

    private static bool IsDiagnosticsEnabled(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            return true;
        }

        return configuration.GetValue<bool>("TileDiagnostics:Enabled");
    }

    private static IResult? ValidateLayer(string layer)
    {
        return TileLayerNames.IsValid(layer)
            ? null
            : Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(layer)] = [$"Unsupported tile layer '{layer}'."]
                },
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult? ValidateTileCoordinates(int z, int x, int y)
    {
        if (z < 0 || z > 22)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(z)] = ["z must be between 0 and 22."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var maxIndex = (1 << z) - 1;
        if (x < 0 || x > maxIndex)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(x)] = [$"x must be between 0 and {maxIndex} for zoom level {z}."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (y < 0 || y > maxIndex)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(y)] = [$"y must be between 0 and {maxIndex} for zoom level {z}."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }
}