using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface ISremDecoder
{
    SREM DecodeSREM(Packet packet);
}
