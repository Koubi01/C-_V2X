namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

// Compatibility facade kept during interface-segregation migration.
public interface IPcapService :
    IPcapIngestionService,
    IPacketQueryService,
    IV2XMessageQueryService,
    ICorrelationService,
    IMapEntityService,
    IStationProfileService
{
}
