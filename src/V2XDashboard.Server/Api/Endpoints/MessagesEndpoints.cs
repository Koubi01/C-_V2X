using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Api;

namespace V2XDashboard.Server.Api.Endpoints;

public static class MessagesEndpoints
{
    private static readonly HashSet<string> SupportedMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CAM",
        "DENM",
        "MAPEM",
        "SPATEM",
        "SREM",
        "SSEM"
    };

    public static IEndpointRouteBuilder MapMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messages").WithTags("Messages");

        group.MapGet("/", async (IV2XMessageQueryService messageQueryService, string? messageType, int? limit) =>
        {
            var messageTypeValidation = ValidateMessageType(messageType);
            if (messageTypeValidation is not null)
            {
                return messageTypeValidation;
            }

            var normalizedMessageType = string.IsNullOrWhiteSpace(messageType)
                ? null
                : messageType.Trim();

            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetV2XMessagesAsync(normalizedMessageType, limit);
            return Results.Ok(messages);
        });

        group.MapGet("/counts", async (IV2XMessageQueryService messageQueryService) =>
        {
            var counts = await messageQueryService.GetMessageCountsAsync();
            return Results.Ok(counts);
        });

        group.MapGet("/paged", async (IV2XMessageQueryService messageQueryService, string messageType = "CAM", int pageNumber = 1, int pageSize = 25) =>
        {
            var messageTypeValidation = ValidateMessageType(messageType);
            if (messageTypeValidation is not null)
            {
                return messageTypeValidation;
            }

            var normalizedMessageType = messageType.Trim();

            var pagingValidation = ApiRequestValidation.ValidatePaging(pageNumber, pageSize, 500);
            if (pagingValidation is not null)
            {
                return pagingValidation;
            }

            var messages = await messageQueryService.GetMessageListPagedAsync(normalizedMessageType, pageNumber, pageSize);
            return Results.Ok(messages);
        });

        group.MapGet("/{id:int}", async (IV2XMessageQueryService messageQueryService, int id, string? messageType) =>
        {
            var messageTypeValidation = ValidateMessageType(messageType);
            if (messageTypeValidation is not null)
            {
                return messageTypeValidation;
            }

            var normalizedMessageType = string.IsNullOrWhiteSpace(messageType)
                ? null
                : messageType.Trim();

            try
            {
                var message = await messageQueryService.GetV2XMessageByIdAsync(id, normalizedMessageType);
                return message is null ? Results.NotFound() : Results.Ok(message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "Ambiguous message identifier.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapGet("/cam", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetCAMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/denm", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetDENMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/mapem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetMAPEMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/spatem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetSPATEMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/srem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetSREMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        group.MapGet("/ssem", async (IV2XMessageQueryService messageQueryService, int? limit) =>
        {
            var limitValidation = ApiRequestValidation.ValidateLimit(limit);
            if (limitValidation is not null)
            {
                return limitValidation;
            }

            var messages = await messageQueryService.GetSSEMMessagesAsync(limit);
            return Results.Ok(messages);
        });

        return app;
    }

    private static IResult? ValidateMessageType(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
        {
            return null;
        }

        if (SupportedMessageTypes.Contains(messageType.Trim()))
        {
            return null;
        }

        return Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                ["messageType"] = new[] { "messageType must be one of: CAM, DENM, MAPEM, SPATEM, SREM, SSEM." }
            },
            title: "Invalid request.",
            statusCode: StatusCodes.Status400BadRequest);
    }
}
