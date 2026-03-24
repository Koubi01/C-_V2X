using V2XDashboard.Server.Services.PcapReader.Interfaces;

namespace V2XDashboard.Server.Api.Endpoints;

public static class CorrelationsEndpoints
{
    public static IEndpointRouteBuilder MapCorrelationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/correlations").WithTags("Correlations");

        group.MapGet("/", async (
            ICorrelationService correlationService,
            string? obuStationId,
            string? rsuIntersectionId,
            string? requestId,
            string? correlationType,
            DateTime? fromTime,
            DateTime? toTime,
            int? limit) =>
        {
            var correlations = await correlationService.GetCorrelationsAsync(
                obuStationId,
                rsuIntersectionId,
                requestId,
                correlationType,
                fromTime,
                toTime,
                limit ?? 100);

            return Results.Ok(correlations);
        });

        group.MapGet("/paged", async (
            ICorrelationService correlationService,
            string? obuStationId,
            string? rsuIntersectionId,
            string? requestId,
            string? correlationType,
            DateTime? fromTime,
            DateTime? toTime,
            int pageNumber = 1,
            int pageSize = 10) =>
        {
            var correlations = await correlationService.GetCorrelationsPagedAsync(
                obuStationId,
                rsuIntersectionId,
                requestId,
                correlationType,
                fromTime,
                toTime,
                pageNumber,
                pageSize);

            return Results.Ok(correlations);
        });

        group.MapGet("/srem/{id:int}/ssem-match", async (ICorrelationService correlationService, int id) =>
        {
            var ssemMatch = await correlationService.GetSsemForSremAsync(id);
            return ssemMatch is null ? Results.NotFound() : Results.Ok(ssemMatch);
        });

        group.MapPost("/record", async (ICorrelationService correlationService) =>
        {
            await correlationService.RecordOBUToRSUCorrelationAsync();
            return Results.Ok("Correlations recorded successfully");
        });

        return app;
    }
}
