namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public interface ITsharkProcessRunner
{
    Task<string> RunAsync(string arguments);
}
