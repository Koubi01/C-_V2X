using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public interface ITsharkParser
{
    Task<List<Packet>> ExtractPacketsAsync(string pcapFilePath, string filter = "");
}