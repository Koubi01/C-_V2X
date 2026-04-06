using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Api;

namespace V2XDashboard.Server.Api.Endpoints;

public static class PacketsEndpoints
{
    public static IEndpointRouteBuilder MapPacketsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/packets").WithTags("Packets");

        group.MapGet("/", async (IPacketQueryService packetQueryService, string? filter, bool? isSecureSigned, bool? isSecureEncrypted, string? signerId, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var packets = await packetQueryService.GetPacketsAsync(filter, isSecureSigned, isSecureEncrypted, signerId, limit);
            return Results.Ok(packets);
        });

        group.MapGet("/paged", async (IPacketQueryService packetQueryService, string? filter, bool? isSecureSigned, bool? isSecureEncrypted, string? signerId, int pageNumber = 1, int pageSize = 25) =>
        {
            var pagingValidation = ApiRequestValidation.ValidatePaging(pageNumber, pageSize, 500);
            if (pagingValidation is not null)
            {
                return pagingValidation;
            }

            var packets = await packetQueryService.GetPacketsPagedAsync(filter, isSecureSigned, isSecureEncrypted, signerId, pageNumber, pageSize);
            return Results.Ok(packets);
        });

        group.MapGet("/security/summary", async (IPacketQueryService packetQueryService) =>
        {
            var summary = await packetQueryService.GetSecurityMetadataSummaryAsync();
            return Results.Ok(summary);
        });

        group.MapGet("/{id:int}", async (IPacketQueryService packetQueryService, int id) =>
        {
            var packet = await packetQueryService.GetPacketByIdAsync(id);
            return packet is null ? Results.NotFound() : Results.Ok(packet);
        });

        return app;
    }
}
