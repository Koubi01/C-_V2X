using V2XDashboard.Server.Services.PcapReader.Interfaces;

namespace V2XDashboard.Server.Api.Endpoints;

public static class PacketsEndpoints
{
    public static IEndpointRouteBuilder MapPacketsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/packets").WithTags("Packets");

        group.MapGet("/", async (IPacketQueryService packetQueryService, string? filter, int? limit) =>
        {
            var packets = await packetQueryService.GetPacketsAsync(filter, limit);
            return Results.Ok(packets);
        });

        group.MapGet("/paged", async (IPacketQueryService packetQueryService, string? filter, int pageNumber = 1, int pageSize = 25) =>
        {
            var packets = await packetQueryService.GetPacketsPagedAsync(filter, pageNumber, pageSize);
            return Results.Ok(packets);
        });

        group.MapGet("/{id:int}", async (IPacketQueryService packetQueryService, int id) =>
        {
            var packet = await packetQueryService.GetPacketByIdAsync(id);
            return packet is null ? Results.NotFound() : Results.Ok(packet);
        });

        return app;
    }
}
