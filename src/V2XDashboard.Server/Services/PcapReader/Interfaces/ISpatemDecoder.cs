using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface ISpatemDecoder
{
    SPATEM DecodeSPATEM(Packet packet);
}
