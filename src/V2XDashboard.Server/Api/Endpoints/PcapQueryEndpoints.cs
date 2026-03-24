namespace V2XDashboard.Server.Api.Endpoints;

public static class PcapQueryEndpoints
{
    public static IEndpointRouteBuilder MapPcapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPacketsEndpoints();
        app.MapMessagesEndpoints();
        app.MapCorrelationsEndpoints();
        app.MapMapEndpoints();

        return app;
    }
}
