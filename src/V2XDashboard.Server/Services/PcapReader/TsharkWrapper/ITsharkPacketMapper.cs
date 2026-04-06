using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public interface ITsharkPacketMapper
{
    List<Packet> Map(string jsonOutput, string pcapFileName);
}
