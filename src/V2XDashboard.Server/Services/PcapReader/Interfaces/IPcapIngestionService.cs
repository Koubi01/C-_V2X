using System.Collections.Generic;
using System.Threading.Tasks;

namespace V2XDashboard.Server.Services.PcapReader.Interfaces;

public interface IPcapIngestionService
{
    Task<List<string>> GetPcapFilesAsync();
    Task<bool> ProcessPcapFileAsync(string fileName);
    Task<bool> ProcessAllPcapFilesAsync();
}
