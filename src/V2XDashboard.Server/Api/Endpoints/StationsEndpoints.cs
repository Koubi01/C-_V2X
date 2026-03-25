using Microsoft.AspNetCore.Mvc;
using V2XDashboard.Server.Api;
using V2XDashboard.Server.Services.PcapReader.Interfaces;

namespace V2XDashboard.Server.Api.Endpoints;

public static class StationsEndpoints
{
    public static IEndpointRouteBuilder MapStationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stations").WithTags("Stations");

        group.MapGet(
            "/profiles",
            async (
                IStationProfileService stationProfileService,
                string? stationId,
                string? entityType,
                string? messageType,
                string? vehicleCategory,
                int? stationType,
                bool? supportsSecureComm,
                int pageNumber = 1,
                int pageSize = 50) =>
            {
                var pagingValidation = ApiRequestValidation.ValidatePaging(pageNumber, pageSize, 500);
                if (pagingValidation is not null)
                {
                    return pagingValidation;
                }

                var result = await stationProfileService.GetStationProfilesPagedAsync(
                    stationId,
                    entityType,
                    messageType,
                    vehicleCategory,
                    stationType,
                    supportsSecureComm,
                    pageNumber,
                    pageSize);

                return Results.Ok(result);
            });

        group.MapGet(
            "/profiles/{stationId}",
            async (IStationProfileService stationProfileService, [FromRoute] string stationId) =>
            {
                var stationValidation = ApiRequestValidation.ValidateRequiredNonEmpty(stationId, "stationId");
                if (stationValidation is not null)
                {
                    return stationValidation;
                }

                var profile = await stationProfileService.GetStationProfileByStationIdAsync(stationId);
                return profile is null ? Results.NotFound() : Results.Ok(profile);
            });

        group.MapGet(
            "/capabilities",
            async (IStationProfileService stationProfileService) =>
            {
                var summary = await stationProfileService.GetStationCapabilitiesAsync();
                return Results.Ok(summary);
            });

        group.MapPost(
            "/profiles/refresh",
            async (IStationProfileService stationProfileService) =>
            {
                await stationProfileService.RefreshStationProfilesAsync();
                return Results.Accepted();
            });

        return app;
    }
}
