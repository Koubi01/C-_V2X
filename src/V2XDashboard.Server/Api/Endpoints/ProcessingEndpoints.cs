using System.Diagnostics;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Api.Endpoints;

public static class ProcessingEndpoints
{
    public static IEndpointRouteBuilder MapProcessingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pcap").WithTags("Pcap Processing");

        group.MapGet("/files", async (IPcapIngestionService ingestionService) =>
        {
            var files = await ingestionService.GetPcapFilesAsync();
            return Results.Ok(files);
        });

        group.MapPost("/process/{fileName}", async (IPcapIngestionService ingestionService, string fileName) =>
        {
            var success = await ingestionService.ProcessPcapFileAsync(fileName);
            return success
                ? Results.Ok($"Successfully processed {fileName}")
                : Results.BadRequest($"Failed to process {fileName}");
        });

        group.MapGet("/process/allFiles", async (IPcapIngestionService ingestionService, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PcapProcessingEndpoints");
            var stopwatch = Stopwatch.StartNew();
            var success = await ingestionService.ProcessAllPcapFilesAsync();
            stopwatch.Stop();

            if (success)
            {
                logger.LogInformation(
                    "Total processing time for all PCAP files: {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);
                return Results.Ok("Successfully processed all PCAP files");
            }

            return Results.BadRequest("Failed to process some PCAP files");
        });

        group.MapPost("/correlations/record", async (ICorrelationService correlationService) =>
        {
            await correlationService.RecordOBUToRSUCorrelationAsync();
            return Results.Ok("Correlations recorded successfully");
        });

        group.MapGet("/map-config", (IConfiguration configuration) =>
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