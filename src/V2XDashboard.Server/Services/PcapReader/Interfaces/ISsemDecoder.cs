using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface ISsemDecoder
{
    SSEM DecodeSSEM(Packet packet);
}
