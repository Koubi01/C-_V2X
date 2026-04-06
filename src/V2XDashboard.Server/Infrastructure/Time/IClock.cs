namespace V2XDashboard.Server.Infrastructure.Time;

public interface IClock
{
    DateTime UtcNow { get; }
}