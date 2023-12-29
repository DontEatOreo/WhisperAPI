using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace WhisperAPI;

public class Globals
{
    public readonly string WhisperFolder;
    public readonly string AudioFilesFolder;

    private readonly ILogger _logger = Log.ForContext<Globals>();

    public Globals(IOptions<WhisperSettings> options)
    {
        if (string.IsNullOrEmpty(options.Value.Folder))
            throw new DirectoryNotFoundException("Whisper folder not found. Please set the Whisper folder in appsettings.json");

        var whisperFolder = Path.GetFullPath(options.Value.Folder);
        if (Directory.Exists(whisperFolder) is false)
        {
            _logger.Warning("Whisper folder not found");
            _logger.Information("Creating Whisper folder");
            Directory.CreateDirectory(whisperFolder);
            _logger.Information("Whisper folder created");
        }

        WhisperFolder = whisperFolder;
        AudioFilesFolder = Path.Combine(whisperFolder, "AudioFiles");

        if (Directory.Exists(AudioFilesFolder)) return;
        _logger.Information("Creating AudioFiles folder");
        Directory.CreateDirectory(AudioFilesFolder);
    }
}
