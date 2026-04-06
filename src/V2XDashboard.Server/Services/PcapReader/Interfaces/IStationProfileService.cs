using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IStationProfileService
{
    Task RefreshStationProfilesAsync();

    Task<PagedResult<StationProfileDto>> GetStationProfilesPagedAsync(
        string? stationId = null,
        string? entityType = null,
        string? messageType = null,
        string? vehicleCategory = null,
        int? stationType = null,
        bool? supportsSecureComm = null,
        int pageNumber = 1,
        int pageSize = 50);

    Task<StationProfileDto?> GetStationProfileByStationIdAsync(string stationId);

    Task<StationCapabilitiesSummaryDto> GetStationCapabilitiesAsync();
}
