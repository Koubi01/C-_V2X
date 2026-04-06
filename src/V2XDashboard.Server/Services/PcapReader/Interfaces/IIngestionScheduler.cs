using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IIngestionScheduler
{
    Task<IngestionSchedulerStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IngestionSchedulerStatusDto> StartSchedulingAsync(CancellationToken cancellationToken = default);
    Task<IngestionSchedulerStatusDto> StopSchedulingAsync(CancellationToken cancellationToken = default);
}
