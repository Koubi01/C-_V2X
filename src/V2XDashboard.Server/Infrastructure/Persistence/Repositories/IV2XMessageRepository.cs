using System.Data;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public interface IV2XMessageRepository
{
    Task InsertCAMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<CAM> messages);
    Task InsertDENMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<DENM> messages);
    Task InsertMAPEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<MAPEM> messages);
    Task InsertSPATEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SPATEM> messages);
    Task InsertSREMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SREM> messages);
    Task InsertSSEMBatchAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<SSEM> messages);
    Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25);
    Task<MessageCountsDto> GetMessageCountsAsync();
    Task<DistinctVehicleWindowStatsDto> GetDistinctVehicleWindowStatsAsync();
    Task<MapVehicleFilterSummaryDto> GetMapVehicleFilterSummaryAsync(MapVehicleSummaryQueryParams filters);
    Task<List<CAM>> GetCAMMessagesAsync(int? limit = null);
    Task<List<DENM>> GetDENMMessagesAsync(int? limit = null);
    Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null);
    Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null);
    Task<List<SREM>> GetSREMMessagesAsync(int? limit = null);
    Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null);
    Task<V2XMessage?> GetV2XMessageByIdAsync(int id, string? messageType = null);
}
