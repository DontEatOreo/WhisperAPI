using System.Diagnostics;

namespace WhisperAPI;

public class GlobalChecks
{
    #region Ctor

    private readonly Globals _globals;
    private readonly GlobalDownloads _globalDownloads;
    private readonly Serilog.ILogger _logger;

    public GlobalChecks(Globals globals, GlobalDownloads globalDownloads, Serilog.ILogger logger)
    {
        _globals = globals;
        _globalDownloads = globalDownloads;
        _logger = logger;
    }

    #endregion Ctor

    #region Methods

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

    /// <summary>
    /// Check if Whisper is installed
    /// </summary>
    public async Task Whisper()
    {
        ProcessStartInfo whisperInfo = new()
        {
            FileName = _globals.WhisperExecPath,
            Arguments = "-h",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using Process process = new() { StartInfo = whisperInfo };
            process.Start();
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(),
                process.StandardError.ReadToEndAsync());
            await process.WaitForExitAsync();
            if (process.ExitCode is not 0)
            {
                _logger.Information("Whisper is not installed");
                await _globalDownloads.Whisper();
            }
        }
        catch (Exception)
        {
            _logger.Information("Whisper is not installed");
            await _globalDownloads.Whisper();
        }
    }

    /// <summary>
    /// Checks if make is installed
    /// </summary>
    public async Task Make()
    {
        ProcessStartInfo makeInfo = new()
        {
            FileName = "make",
            Arguments = "-v",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using Process process = new() { StartInfo = makeInfo };
        process.Start();

        await process.WaitForExitAsync();
        if (process.ExitCode is not 0)
        {
            _logger.Error("Make is not installed");
            Environment.Exit(1);
        }
    }

    #endregion Methods
}