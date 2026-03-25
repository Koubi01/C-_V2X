using System.Threading.Tasks;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IPacketQueryService
{
    Task<List<Packet>> GetPacketsAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int? limit = null);
    Task<PagedResult<Packet>> GetPacketsPagedAsync(string? filter = null, bool? isSecureSigned = null, bool? isSecureEncrypted = null, string? signerId = null, int pageNumber = 1, int pageSize = 25);
    Task<Packet?> GetPacketByIdAsync(int id);
    Task<SecurityMetadataSummaryDto> GetSecurityMetadataSummaryAsync();
}
