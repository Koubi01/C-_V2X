using System.Diagnostics;
using Microsoft.Extensions.Logging;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public class TsharkParser : ITsharkParser
{
    private readonly ITsharkProcessRunner _tsharkProcessRunner;
    private readonly ITsharkPacketMapper _tsharkPacketMapper;
    private readonly ILogger<TsharkParser> _logger;

    public TsharkParser(
        ITsharkProcessRunner tsharkProcessRunner,
        ITsharkPacketMapper tsharkPacketMapper,
        ILogger<TsharkParser> logger)
    {
        _tsharkProcessRunner = tsharkProcessRunner;
        _tsharkPacketMapper = tsharkPacketMapper;
        _logger = logger;
    }

    public async Task<List<Packet>> ExtractPacketsAsync(string pcapFilePath, string filter = "")
    {
        var parseStopwatch = Stopwatch.StartNew();
        var arguments = BuildArguments(pcapFilePath, filter);

        try
        {
            var output = await _tsharkProcessRunner.RunAsync(arguments);
            var packets = _tsharkPacketMapper.Map(output, Path.GetFileName(pcapFilePath));

            parseStopwatch.Stop();
            _logger.LogInformation(
                "Tshark extract summary for {PcapFileName}: filter={Filter}, packets={PacketCount}, duration={DurationMs}ms",
                Path.GetFileName(pcapFilePath),
                filter,
                packets.Count,
                parseStopwatch.ElapsedMilliseconds);

            return packets;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to extract packets from {pcapFilePath}: {ex.Message}", ex);
        }
    }

    private static string BuildArguments(string pcapFilePath, string filter)
    {
        var arguments = $"-r \"{pcapFilePath}\" -T json";

        if (!string.IsNullOrEmpty(filter))
        {
            arguments += $" -Y \"{filter}\"";
        }

        return arguments;
    }
}
