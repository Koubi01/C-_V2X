using V2XDashboard.Server.Services.PcapReader.Interfaces;
using Microsoft.AspNetCore.Mvc;
using V2XDashboard.Shared;
using V2XDashboard.Server.Api;

namespace V2XDashboard.Server.Api.Endpoints;

public static class MapEndpoints
{
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
}
