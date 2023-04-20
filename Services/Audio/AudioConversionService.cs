using CliWrap;
using ILogger = Serilog.ILogger;

namespace WhisperAPI.Services.Audio;

public class AudioConversionService : IAudioConversionService
{
    private readonly ILogger _logger;

    public AudioConversionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task ConvertToWavAsync(string inputFilePath, string outputFilePath)
    {
        string[] ffmpegArgs =
        {
            "-i",
            inputFilePath,
            "-ar",
            "16000",
            "-ac",
            "1",
            "-c:a",
            "pcm_s16le",
            outputFilePath
        };

        try
        {
            await Cli.Wrap("ffmpeg")
                .WithArguments(arg =>
                {
                    foreach (var ffmpegArg in ffmpegArgs)
                        arg.Add(ffmpegArg);
                })
                .ExecuteAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e, "[{Message}] Could not convert file to wav", e.Message);
        }
    }
}