namespace V2XDashboard.Server.Services.PcapReader.Scheduling;

public sealed class IngestionSchedulerOptions
{
    public bool Enabled { get; set; } = true;
    public bool IsSchedulerNode { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
}
