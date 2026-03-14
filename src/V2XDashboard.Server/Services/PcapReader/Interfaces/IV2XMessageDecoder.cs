using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IV2XMessageDecoder
{
    CAM DecodeCAM(Packet packet);
    DENM DecodeDENM(Packet packet);
    MAPEM DecodeMAPEM(Packet packet);
    SPATEM DecodeSPATEM(Packet packet);
    SREM DecodeSREM(Packet packet);
    SSEM DecodeSSEM(Packet packet);
}
