using System.Diagnostics;

namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public sealed class TsharkProcessRunner : ITsharkProcessRunner
{
    private readonly string _tsharkPath;

    public TsharkProcessRunner(string tsharkPath = "tshark")
    {
        _tsharkPath = tsharkPath;
    }

    public async Task<string> RunAsync(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _tsharkPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Tshark error: {error}");
        }

        return output;
    }
}
