namespace WhisperAPI;

public class Globals
{
    #region Strings

    /// <summary>
    /// Where will WhisperModels be downloaded to and where the audio files will be stored.
    /// </summary>
    public readonly string WhisperFolder = Environment.GetEnvironmentVariable("WHISPER_FOLDER")
                                                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperAPI");

    public readonly string AudioFilesFolder;

    /// <summary>
    /// This variable is responsible for where the Whisper executable is located.
    /// If null it will default to the WhisperFolder and look for "main" file
    /// </summary>
    public readonly string WhisperExecPath;

    /// <summary>
    /// This is the key for AsyncKeyedLocker
    /// </summary>
    public readonly string Key = "GlobalKey";

    /// <summary>
    /// This is the URL for the Whisper source code
    /// </summary>
    public readonly string WhisperUrl = "https://github.com/ggerganov/whisper.cpp/archive/refs/heads/master.zip";

    #endregion

    /// <summary>
    /// Path to WhisperModels
    /// </summary>
    public readonly Dictionary<WhisperModel, string> ModelFilePaths;

    public readonly Dictionary<bool, string> OutputFormatMapping = new()
    {
        { true, "-ocsv" },
        { false, "-otxt" }
    };

    public Globals()
    {
        AudioFilesFolder = Path.Combine(WhisperFolder, "AudioFiles");
        ModelFilePaths = new Dictionary<WhisperModel, string>
        {
            { WhisperModel.Tiny, Path.Combine(WhisperFolder, "ggml-tiny.bin") },
            { WhisperModel.Base, Path.Combine(WhisperFolder, "ggml-base.bin") },
            { WhisperModel.Small, Path.Combine(WhisperFolder, "ggml-small.bin") },
            { WhisperModel.Medium, Path.Combine(WhisperFolder, "ggml-medium.bin") },
            { WhisperModel.Large, Path.Combine(WhisperFolder, "ggml-large.bin") }
        };
        WhisperExecPath = Environment.GetEnvironmentVariable("WHISPER_EXEC_PATH") ?? Path.Combine(WhisperFolder, "main");
    }
}

/// <summary>
/// Supported models for Whisper
/// </summary>
public enum WhisperModel
{
    Tiny,
    Base,
    Small,
    Medium,
    Large
}

public readonly struct AudioTranscriptionOptions
{
    public string FileName { get; init; }
    public string WavFile { get; init; }
    public string Language { get; init; }
    public bool Translate { get; init; }
    public WhisperModel WhisperModel { get; init; }
    public bool TimeStamp { get; init; }
}

public readonly struct TranscriptionOptions
{
    public string AudioFile { get; init; }
    public string Language { get; init; }
    public bool Translate { get; init; }
    public string ModelPath { get; init; }
    public string OutputFormat { get; init; }
}