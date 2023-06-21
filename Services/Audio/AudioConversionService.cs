using System.Diagnostics;
using WhisperAPI.Exceptions;

namespace WhisperAPI.Services.Audio;

public class AudioConversionService : IAudioConversionService
{
    private readonly Serilog.ILogger _logger;

    public AudioConversionService(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public async Task ConvertToWavAsync(string input, string output)
    {
        string[] ffmpegArgs =
        {
            "-i",
            input,
            "-ar",
            "16000",
            "-ac",
            "1",
            "-c:a",
            "pcm_s16le",
            output
        };

        ProcessStartInfo startInfo = new()
        {
            FileName = "ffmpeg",
            Arguments = string.Join(" ", ffmpegArgs),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(),
                process.StandardError.ReadToEndAsync());
            await process.WaitForExitAsync();
        }
        catch (Exception e)
        {
            const string error = "Could not convert file to wav";
            _logger.Error(e, "[{Message}] Could not convert file to wav", e.Message);
            throw new FileProcessingException(error);
        }
    }
}