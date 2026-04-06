using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Api;

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
            bool? isSecureSigned,
            bool? isSecureEncrypted,
            int? limit) =>
        {
            var dateRangeValidation = ApiRequestValidation.ValidateDateRange(fromTime, toTime);
            if (dateRangeValidation is not null)
            {
                return dateRangeValidation;
            }

            var limitValidation = ApiRequestValidation.ValidateLimit(limit, 10000);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var correlations = await correlationService.GetCorrelationsAsync(
                obuStationId,
                rsuIntersectionId,
                requestId,
                correlationType,
                fromTime,
                toTime,
                isSecureSigned,
                isSecureEncrypted,
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
            bool? isSecureSigned,
            bool? isSecureEncrypted,
            int pageNumber = 1,
            int pageSize = 10) =>
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

            var correlations = await correlationService.GetCorrelationsPagedAsync(
                obuStationId,
                rsuIntersectionId,
                requestId,
                correlationType,
                fromTime,
                toTime,
                isSecureSigned,
                isSecureEncrypted,
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
