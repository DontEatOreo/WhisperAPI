using Microsoft.Extensions.Options;
using Whisper.net.Ggml;

namespace WhisperAPI;

public class Globals
{
    public readonly string AudioFilesFolder;

    /// <summary>
    /// Path to WhisperModels
    /// </summary>
    public readonly Dictionary<GgmlType, string> ModelFilePaths;

    public Globals(IOptions<AppSettings> options)
    {
        var whisperFolder = Path.GetFullPath(options.Value.WhisperFolder);
        if (!Directory.Exists(whisperFolder))
            throw new DirectoryNotFoundException($"Whisper folder not found at {whisperFolder}");

        AudioFilesFolder = Path.Combine(whisperFolder, "AudioFiles");
        ModelFilePaths = new Dictionary<GgmlType, string>
        {
            { GgmlType.Tiny, Path.Combine(whisperFolder, "ggml-tiny.bin") },
            { GgmlType.Base, Path.Combine(whisperFolder, "ggml-base.bin") },
            { GgmlType.Small, Path.Combine(whisperFolder, "ggml-small.bin") },
            { GgmlType.Medium, Path.Combine(whisperFolder, "ggml-medium.bin") },
            { GgmlType.Large, Path.Combine(whisperFolder, "ggml-large.bin") }
        };
    }
}