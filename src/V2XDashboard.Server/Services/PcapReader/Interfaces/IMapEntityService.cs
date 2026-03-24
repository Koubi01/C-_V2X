using System.Collections.Generic;
using System.Threading.Tasks;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IMapEntityService
{
    Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null);

    Task<PagedResult<MapEntityDto>> GetMapEntitiesPagedAsync(
        DateTime? fromTime = null,
        DateTime? toTime = null,
        IEnumerable<string>? entityTypes = null,
        IEnumerable<string>? messageTypes = null,
        IEnumerable<string>? vehicleCategories = null,
        IEnumerable<int>? stationTypes = null,
        int pageNumber = 1,
        int pageSize = 100);
}
