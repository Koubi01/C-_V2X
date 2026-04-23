using V2XDashboard.Server.Services.PcapReader.Interfaces;
using Microsoft.AspNetCore.Mvc;
using V2XDashboard.Shared;
using V2XDashboard.Server.Api;

namespace V2XDashboard.Server.Api.Endpoints;

public static class MapEndpoints
{
    private static readonly HashSet<string> VehicleSummarySupportedLayers = new(StringComparer.OrdinalIgnoreCase)
    {
        TileLayerNames.Cam,
        TileLayerNames.Denm,
        TileLayerNames.Mapem,
        TileLayerNames.Spatem,
        TileLayerNames.Srem,
        TileLayerNames.Ssem,
        TileLayerNames.Correlations,
        "TrafficIntensity"
    };

    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/map").WithTags("Map");

        group.MapGet("/entities", async (IMapEntityService mapEntityService, DateTime? fromTime, DateTime? toTime) =>
        {
            var dateRangeValidation = ApiRequestValidation.ValidateDateRange(fromTime, toTime);
            if (dateRangeValidation is not null)
            {
                return dateRangeValidation;
            }

            var entities = await mapEntityService.GetMapEntitiesAsync(fromTime, toTime);
            return Results.Ok(entities);
        });

        group.MapGet(
            "/entities/paged",
            async (
                IMapEntityService mapEntityService,
                DateTime? fromTime,
                DateTime? toTime,
                [FromQuery] string[]? entityTypes,
                [FromQuery] string[]? messageTypes,
                [FromQuery] string[]? vehicleCategories,
                [FromQuery] int[]? stationTypes,
                bool? isSecureSigned,
                bool? isSecureEncrypted,
                int pageNumber = 1,
                int pageSize = 100) =>
            {
                var dateRangeValidation = ApiRequestValidation.ValidateDateRange(fromTime, toTime);
                if (dateRangeValidation is not null)
                {
                    return dateRangeValidation;
                }

                var pagingValidation = ApiRequestValidation.ValidatePaging(pageNumber, pageSize, 500);
                if (pagingValidation is not null)
                {
                    return pagingValidation;
                }

                var result = await mapEntityService.GetMapEntitiesPagedAsync(
                    fromTime,
                    toTime,
                    entityTypes,
                    messageTypes,
                    vehicleCategories,
                    stationTypes,
                    isSecureSigned,
                    isSecureEncrypted,
                    pageNumber,
                    pageSize);

                return Results.Ok(result);
            });

        group.MapGet(
            "/vehicles/summary",
            async (
                [AsParameters] MapVehicleSummaryQueryParams filters,
                IV2XMessageQueryService messageQueryService) =>
            {
                var dateRangeValidation = ApiRequestValidation.ValidateDateRange(filters.FromTime, filters.ToTime);
                if (dateRangeValidation is not null)
                {
                    return dateRangeValidation;
                }

                var layersValidation = ValidateVehicleSummaryLayers(filters.VisibleLayers);
                if (layersValidation is not null)
                {
                    return layersValidation;
                }

                var spatialValidation = ValidateVehicleSummarySpatialScope(filters);
                if (spatialValidation is not null)
                {
                    return spatialValidation;
                }

                var summary = await messageQueryService.GetMapVehicleFilterSummaryAsync(filters);
                return Results.Ok(summary);
            });

        group.MapGet("/config", (IConfiguration configuration) =>
        {
            var mapSection = configuration.GetSection("MapConfig");
            var mapConfig = new MapConfigDto
            {
                TileStyleUrl = mapSection.GetValue<string>("TileStyleUrl") ?? "http://localhost:8081/styles/basic/style.json",
                DefaultCenterLatitude = mapSection.GetValue<double?>("DefaultCenterLatitude") ?? 50.0755,
                DefaultCenterLongitude = mapSection.GetValue<double?>("DefaultCenterLongitude") ?? 14.4378,
                DefaultZoom = mapSection.GetValue<double?>("DefaultZoom") ?? 12,
                MinZoom = mapSection.GetValue<double?>("MinZoom") ?? 3,
                MaxZoom = mapSection.GetValue<double?>("MaxZoom") ?? 20
            };

            return Results.Ok(mapConfig);
        });

        return app;
    }

    private static IResult? ValidateVehicleSummaryLayers(string[]? visibleLayers)
    {
        if (visibleLayers is null)
        {
            return null;
        }

        var invalidLayers = visibleLayers
            .Where(layer => !string.IsNullOrWhiteSpace(layer))
            .Select(layer => layer.Trim())
            .Where(layer => !VehicleSummarySupportedLayers.Contains(layer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidLayers.Length == 0)
        {
            return null;
        }

        return Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [nameof(MapVehicleSummaryQueryParams.VisibleLayers)] =
                [
                    $"Unsupported visibleLayers value(s): {string.Join(", ", invalidLayers)}."
                ]
            },
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult? ValidateVehicleSummarySpatialScope(MapVehicleSummaryQueryParams filters)
    {
        var hasAnyBounds = filters.MinLatitude.HasValue
            || filters.MaxLatitude.HasValue
            || filters.MinLongitude.HasValue
            || filters.MaxLongitude.HasValue;

        var hasAllBounds = filters.MinLatitude.HasValue
            && filters.MaxLatitude.HasValue
            && filters.MinLongitude.HasValue
            && filters.MaxLongitude.HasValue;

        if (hasAnyBounds && !hasAllBounds)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(MapVehicleSummaryQueryParams.MinLatitude)] =
                    ["minLatitude, maxLatitude, minLongitude, and maxLongitude must all be provided together."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var hasAnyTile = filters.TileZ.HasValue
            || filters.TileX.HasValue
            || filters.TileY.HasValue;

        var hasAllTile = filters.TileZ.HasValue
            && filters.TileX.HasValue
            && filters.TileY.HasValue;

        if (hasAnyTile && !hasAllTile)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(MapVehicleSummaryQueryParams.TileZ)] =
                    ["tileZ, tileX, and tileY must all be provided together."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (hasAllBounds && hasAllTile)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(MapVehicleSummaryQueryParams.MinLatitude)] =
                    ["Provide either viewport bounds or tile coordinates, not both."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (hasAllBounds)
        {
            if (filters.MinLatitude is < -85 or > 85 || filters.MaxLatitude is < -85 or > 85)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(MapVehicleSummaryQueryParams.MinLatitude)] = ["Latitude must be between -85 and 85."]
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (filters.MinLongitude is < -180 or > 180 || filters.MaxLongitude is < -180 or > 180)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(MapVehicleSummaryQueryParams.MinLongitude)] = ["Longitude must be between -180 and 180."]
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (filters.MinLatitude >= filters.MaxLatitude)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(MapVehicleSummaryQueryParams.MinLatitude)] = ["minLatitude must be < maxLatitude."]
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (filters.MinLongitude >= filters.MaxLongitude)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        [nameof(MapVehicleSummaryQueryParams.MinLongitude)] = ["minLongitude must be < maxLongitude."]
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        if (!hasAllTile)
        {
            return null;
        }

        var z = filters.TileZ!.Value;
        var x = filters.TileX!.Value;
        var y = filters.TileY!.Value;

        if (z < 0 || z > 22)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(MapVehicleSummaryQueryParams.TileZ)] = ["tileZ must be between 0 and 22."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var maxIndex = (1 << z) - 1;
        if (x < 0 || x > maxIndex)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(MapVehicleSummaryQueryParams.TileX)] = [$"tileX must be between 0 and {maxIndex} for zoom level {z}."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (y < 0 || y > maxIndex)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    [nameof(MapVehicleSummaryQueryParams.TileY)] = [$"tileY must be between 0 and {maxIndex} for zoom level {z}."]
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }
}
