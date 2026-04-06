namespace V2XDashboard.Shared;

public sealed class IngestionSchedulerStatusDto
{
    public bool IsSchedulerNode { get; set; }
    public bool IsRunning { get; set; }
    public int IntervalSeconds { get; set; }
    public DateTime? LastRunStartedAtUtc { get; set; }
    public DateTime? LastRunFinishedAtUtc { get; set; }
    public bool? LastRunSucceeded { get; set; }
    public long? LastRunDurationMs { get; set; }
    public string? LastError { get; set; }
    public long TotalRuns { get; set; }
}
