using System.Collections.Generic;
using System.Threading.Tasks;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IPcapService
{
    Task<List<string>> GetPcapFilesAsync();
    Task<bool> ProcessPcapFileAsync(string fileName);
    Task<List<Packet>> GetPacketsAsync(string? filter = null, int? limit = null);
    Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, int pageNumber = 1, int pageSize = 25);
    Task<List<V2XMessage>> GetV2XMessagesAsync(string? messageType = null, int? limit = null);
    Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25);
    Task<MessageCountsDto> GetMessageCountsAsync();
    Task<List<CAM>> GetCAMMessagesAsync(int? limit = null);
    Task<List<DENM>> GetDENMMessagesAsync(int? limit = null);
    Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null);
    Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null);
    Task<List<SREM>> GetSREMMessagesAsync(int? limit = null);
    Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null);
    Task<Packet?> GetPacketByIdAsync(int id);
    Task<V2XMessage?> GetV2XMessageByIdAsync(int id);
    
    // NEW: Correlation and map visualization methods
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
    
    Task RecordOBUToRSUCorrelationAsync();
    Task<bool> ProcessAllPcapFilesAsync();
}
