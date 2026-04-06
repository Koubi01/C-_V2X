using Microsoft.Extensions.DependencyInjection;
using V2XDashboard.Server.Infrastructure.Time;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Server.Services.MapTile;
using V2XDashboard.Server.Services.MapTile.Interfaces;
using V2XDashboard.Server.Services.PcapReader;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Server.Services.PcapReader.Scheduling;
using V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

namespace V2XDashboard.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPcapApplication(this IServiceCollection services)
    {
        services.AddScoped<PcapService>();
        services.AddScoped<IPcapService>(sp => sp.GetRequiredService<PcapService>());
        services.AddScoped<IPcapIngestionService>(sp => sp.GetRequiredService<PcapService>());
        services.AddScoped<IPacketQueryService>(sp => sp.GetRequiredService<PcapService>());
        services.AddScoped<IV2XMessageQueryService>(sp => sp.GetRequiredService<PcapService>());
        services.AddScoped<ICorrelationService>(sp => sp.GetRequiredService<PcapService>());
        services.AddScoped<IMapEntityService>(sp => sp.GetRequiredService<PcapService>());
        services.AddScoped<IStationProfileService>(sp => sp.GetRequiredService<PcapService>());

        services.AddScoped<V2XMessageDecoder>();
        services.AddScoped<IV2XMessageDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());
        services.AddScoped<ICamDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());
        services.AddScoped<IDenmDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());
        services.AddScoped<IMapemDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());
        services.AddScoped<ISpatemDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());
        services.AddScoped<ISremDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());
        services.AddScoped<ISsemDecoder>(sp => sp.GetRequiredService<V2XMessageDecoder>());

        services.AddSingleton<IngestionSchedulerService>();
        services.AddSingleton<IIngestionScheduler>(sp => sp.GetRequiredService<IngestionSchedulerService>());
        services.AddHostedService(sp => sp.GetRequiredService<IngestionSchedulerService>());
        return services;
    }

    public static IServiceCollection AddPcapInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPacketRepository, PacketRepository>();
        services.AddScoped<IV2XMessageRepository, V2XMessageRepository>();
        services.AddScoped<ICorrelationRepository, CorrelationRepository>();
        services.AddScoped<IMapEntityRepository, MapEntityRepository>();
        services.AddSingleton<IMapTileRepository, MapTileRepository>();
        services.AddScoped<IStationProfileRepository, StationProfileRepository>();
        services.AddScoped<IProcessedFileRepository, ProcessedFileRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPacketTypeClassifier, PacketTypeClassifier>();
        services.AddSingleton<ITsharkProcessRunner, TsharkProcessRunner>();
        services.AddSingleton<ITsharkPacketMapper, TsharkPacketMapper>();
        services.AddSingleton<ITsharkParser, TsharkParser>();
        services.AddSingleton<IMapTileService, MapTileService>();
        
        // Add memory cache for tile caching (CRITICAL for vector tile performance)
        services.AddMemoryCache(options => 
        {
            options.SizeLimit = 1000 * 1024 * 1024; // 1000 MiB limit for tile caching
            options.CompactionPercentage = 0.25;
        });
        
        return services;
    }
}
