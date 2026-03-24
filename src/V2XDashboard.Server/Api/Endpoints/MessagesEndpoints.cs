using V2XDashboard.Server.Services.PcapReader.Interfaces;

namespace V2XDashboard.Server.Api.Endpoints;

public static class MessagesEndpoints
{
    public static IEndpointRouteBuilder MapMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messages").WithTags("Messages");

        group.MapGet("/", async (IV2XMessageQueryService messageQueryService, string? messageType, int? limit) =>
        {
            var messages = await messageQueryService.GetV2XMessagesAsync(messageType, limit);
            return Results.Ok(messages);
        });

        group.MapGet("/counts", async (IV2XMessageQueryService messageQueryService) =>
        {
            var counts = await messageQueryService.GetMessageCountsAsync();
            return Results.Ok(counts);
        });

        group.MapGet("/paged", async (IV2XMessageQueryService messageQueryService, string messageType = "CAM", int pageNumber = 1, int pageSize = 25) =>
        {
            var messages = await messageQueryService.GetMessageListPagedAsync(messageType, pageNumber, pageSize);
            return Results.Ok(messages);
        });

        group.MapGet("/{id:int}", async (IV2XMessageQueryService messageQueryService, int id) =>
        {
            var message = await messageQueryService.GetV2XMessageByIdAsync(id);
            return message is null ? Results.NotFound() : Results.Ok(message);
        });

        group.MapGet("/cam", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var messages = await messageQueryService.GetCAMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/denm", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var messages = await messageQueryService.GetDENMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/mapem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var messages = await messageQueryService.GetMAPEMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/spatem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var messages = await messageQueryService.GetSPATEMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/srem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var messages = await messageQueryService.GetSREMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/ssem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var messages = await messageQueryService.GetSSEMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        return app;
    }
}
