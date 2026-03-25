using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public interface IStationProfileRepository
{
    Task RebuildStationProfilesAsync();

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

    Task<StationCapabilitiesSummaryDto> GetStationCapabilitiesSummaryAsync();

    Task<HashSet<string>> GetStationIdsByProfileFiltersAsync(
        IEnumerable<string>? vehicleCategories = null,
        IEnumerable<int>? stationTypes = null);
}
