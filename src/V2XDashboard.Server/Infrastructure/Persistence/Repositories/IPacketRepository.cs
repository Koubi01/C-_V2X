using System.Data;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public interface IPacketRepository
{
    Task InsertPacketsAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<Packet> packets);
    Task<List<Packet>> GetPacketsAsync(string? filter = null, int? limit = null);
    Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, int pageNumber = 1, int pageSize = 25);
    Task<Packet?> GetPacketByIdAsync(int id);
}
