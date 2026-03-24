using System.Threading.Tasks;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IV2XMessageQueryService
{
    Task<List<V2XMessage>> GetV2XMessagesAsync(string? messageType = null, int? limit = null);
    Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25);
    Task<MessageCountsDto> GetMessageCountsAsync();
    Task<List<CAM>> GetCAMMessagesAsync(int? limit = null);
    Task<List<DENM>> GetDENMMessagesAsync(int? limit = null);
    Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null);
    Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null);
    Task<List<SREM>> GetSREMMessagesAsync(int? limit = null);
    Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null);
    Task<V2XMessage?> GetV2XMessageByIdAsync(int id);
}
