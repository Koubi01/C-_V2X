
using V2XDashboard.Server.Api.Endpoints;
using V2XDashboard.Server.Extensions;
using V2XDashboard.Server.Services.PcapReader.Scheduling;

namespace V2XDashboard.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorPages();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("MapClient", policy =>
            {
                policy.WithOrigins(
                        "http://localhost:8080",
                        "http://localhost:8081",
                        "http://localhost:5000",
                        "https://localhost:5001")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        builder.Services.AddSwaggerGen();
        builder.Services.Configure<IngestionSchedulerOptions>(
            builder.Configuration.GetSection("Ingestion:Scheduler"));

        builder.Services
            .AddPcapApplication()
            .AddPcapInfrastructure();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "V2X PCAP Dashboard API v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseExceptionHandler();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseCors("MapClient");

        app.UseAuthorization();

        app.MapIngestionEndpoints();
        app.MapPcapQueryEndpoints();
        app.MapMapTileEndpoints();
        app.MapRazorPages();

        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
