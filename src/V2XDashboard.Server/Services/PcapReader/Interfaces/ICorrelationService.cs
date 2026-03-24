using System.Threading.Tasks;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface ICorrelationService
{
    Task<List<CorrelationDto>> GetCorrelationsAsync(
        string? obuStationId = null,
        string? rsuIntersectionId = null,
        string? requestId = null,
        string? correlationType = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        int? limit = null);

    Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(
        string? obuStationId = null,
        string? rsuIntersectionId = null,
        string? requestId = null,
        string? correlationType = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        int pageNumber = 1,
        int pageSize = 10);

    Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId);
    Task RecordOBUToRSUCorrelationAsync();
}
