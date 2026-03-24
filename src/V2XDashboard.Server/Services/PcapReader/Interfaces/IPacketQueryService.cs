using System.Threading.Tasks;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IPacketQueryService
{
    Task<List<Packet>> GetPacketsAsync(string? filter = null, int? limit = null);
    Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, int pageNumber = 1, int pageSize = 25);
    Task<Packet?> GetPacketByIdAsync(int id);
}
