using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

// Compatibility decoder facade composed from focused message decoders.
public interface IV2XMessageDecoder :
    ICamDecoder,
    IDenmDecoder,
    IMapemDecoder,
    ISpatemDecoder,
    ISremDecoder,
    ISsemDecoder
{
}
