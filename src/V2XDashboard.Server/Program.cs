
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

        // Register PCAP services
        builder.Services.AddScoped<V2XDashboard.Server.Services.PcapReader.Interfaces.IPcapService, V2XDashboard.Server.Services.PcapReader.PcapService>();
        builder.Services.AddScoped<V2XDashboard.Server.Services.PcapReader.Interfaces.IV2XMessageDecoder, V2XDashboard.Server.Services.PcapReader.V2XMessageDecoder>();

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

        // serve the Blazor client static assets (published into wwwroot during build)
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseCors("MapClient");

        //app.UseHttpsRedirection();

        app.UseAuthorization();

        // map the API controllers/endpoints
        app.MapControllers();
        app.MapRazorPages();

        // fallback to index.html to allow client-side routing
        app.MapFallbackToFile("index.html");

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", (HttpContext httpContext) =>
        {
            var forecast =  Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = summaries[Random.Shared.Next(summaries.Length)]
                })
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast");

        app.MapGet("/test", () => "Backend bezi a zdravi vas z Dockeru!");

        app.Run();
    }
}
