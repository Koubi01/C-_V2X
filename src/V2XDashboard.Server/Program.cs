
using V2XDashboard.Server.Api.Endpoints;
using V2XDashboard.Server.Extensions;

namespace V2XDashboard.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // the server hosts the Blazor WebAssembly client so we need
        // MVC/Razor services and static files support.
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

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddSwaggerGen();

        builder.Services
            .AddPcapApplication()
            .AddPcapInfrastructure();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
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

        // serve the Blazor client static assets (published into wwwroot during build)
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseCors("MapClient");

        //app.UseHttpsRedirection();

        app.UseAuthorization();

        // map the API controllers/endpoints
        app.MapControllers();
        app.MapProcessingEndpoints();
        app.MapPcapQueryEndpoints();
        app.MapRazorPages();

        // fallback to index.html to allow client-side routing
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
