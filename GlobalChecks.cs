using System.Diagnostics;

namespace WhisperAPI;

public class GlobalChecks
{
    private readonly Serilog.ILogger _logger;

    public GlobalChecks(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if FFmpeg is installed
    /// </summary>
    public async Task FFmpeg()
    {
        ProcessStartInfo ffmpegInfo = new()
        {
            FileName = "ffmpeg",
            Arguments = "-version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = ffmpegInfo };
            process.Start();
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(),
                process.StandardError.ReadToEndAsync());
            await process.WaitForExitAsync();
            if (process.ExitCode is not 0)
            {
                await Console.Error.WriteLineAsync("FFmpeg is not installed");
                _logger.Error("FFmpeg is not installed");
                Environment.Exit(1);
            }
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("FFmpeg is not installed");
            _logger.Error("FFmpeg is not installed");
            Environment.Exit(1);
        }
    }
}