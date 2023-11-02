using Microsoft.Extensions.Options;
using Serilog;
using Whisper.net.Ggml;

namespace WhisperAPI;

public class Globals
{
    public readonly string WhisperFolder;
    public readonly string AudioFilesFolder;

    public Globals(IOptions<WhisperSettings> options)
    {
        if (options.Value.Folder is null)
        {
            const string errorMessage = "Whisper folder not found. Please set the Whisper folder in appsettings.json";
            throw new DirectoryNotFoundException(errorMessage);
        }

        var whisperFolder = Path.GetFullPath(options.Value.Folder);
        if (Directory.Exists(whisperFolder) is false)
        {
            Log.Warning("Whisper folder not found");
            Log.Information("Creating Whisper folder");
            Directory.CreateDirectory(whisperFolder);
            Log.Information("Whisper folder created");
        }

        WhisperFolder = whisperFolder;
        AudioFilesFolder = Path.Combine(whisperFolder, "AudioFiles");
        if (Directory.Exists(AudioFilesFolder) is false)
            Directory.CreateDirectory(AudioFilesFolder);
    }
}