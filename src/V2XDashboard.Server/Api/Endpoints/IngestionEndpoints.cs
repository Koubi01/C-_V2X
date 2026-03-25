using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Api;

namespace V2XDashboard.Server.Api.Endpoints;

public static class IngestionEndpoints
{
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingestion").WithTags("Ingestion");

        group.MapGet("/files", async (IPcapIngestionService ingestionService) =>
        {
            var files = await ingestionService.GetPcapFilesAsync();
            return Results.Ok(files);
        });

        group.MapPost("/process/{fileName}", async (IPcapIngestionService ingestionService, string fileName) =>
        {
            var fileNameValidation = ApiRequestValidation.ValidateRequiredNonEmpty(fileName, "fileName");
            if (fileNameValidation is not null)
            {
                return fileNameValidation;
            }

            var success = await ingestionService.ProcessPcapFileAsync(fileName);
            return success
                ? Results.Ok($"Successfully processed {fileName}")
                : Results.BadRequest($"Failed to process {fileName}");
        });

        group.MapPost("/process/all", async (IPcapIngestionService ingestionService) =>
        {
            var success = await ingestionService.ProcessAllPcapFilesAsync();
            return success
                ? Results.Ok("Successfully processed all PCAP files")
                : Results.BadRequest("Failed to process some PCAP files");
        });

        group.MapGet("/scheduler/status", async (IIngestionScheduler scheduler, CancellationToken cancellationToken) =>
        {
            var status = await scheduler.GetStatusAsync(cancellationToken);
            return Results.Ok(status);
        });

        group.MapPost("/scheduler/start", async (IIngestionScheduler scheduler, CancellationToken cancellationToken) =>
        {
            var status = await scheduler.StartSchedulingAsync(cancellationToken);
            return Results.Ok(status);
        });

        group.MapPost("/scheduler/stop", async (IIngestionScheduler scheduler, CancellationToken cancellationToken) =>
        {
            var status = await scheduler.StopSchedulingAsync(cancellationToken);
            return Results.Ok(status);
        });

        return app;
    }
}
