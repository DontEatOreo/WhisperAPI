using System.Text.Json.Serialization;
using CliWrap;
using Pastel;
using static WhisperAPI.Globals;

namespace WhisperAPI;

public static class Globals
{
    /// <summary>
    /// This variable is responsible for where WhisperModels will be downloaded to and where the audio files will be stored.
    /// </summary>
    public static readonly string WhisperFolder = Environment.GetEnvironmentVariable("WHISPER_FOLDER")
                                                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperAPI");
    /// <summary>
    /// This variable is responsible for where the Whisper executable is located.
    /// If null it will default to the WhisperFolder and look for "main" file
    /// </summary>
    public static readonly string WhisperExecPath = Environment.GetEnvironmentVariable("WHISPER_EXEC_PATH") ?? Path.Combine(WhisperFolder, "main");

    /// <summary>
    /// This is the key for AsyncKeyedLocker
    /// </summary>
    public const string Key = "GlobalKey";

    public static readonly int ThreadCount = Environment.ProcessorCount;

    /// <summary>
    /// Struct containing all error codes and messages
    /// </summary>
    public struct ErrorCodesAndMessages
    {
        public const string? NoFile = "NO_FILE";
        public const string? NoFileMessage = "No file was provided in the request.";

        public const string InvalidFileType = "INVALID_FILE_TYPE";
        public const string InvalidFileTypeMessage = "The provided file is not a valid audio or video file.";

        public const string InvalidModel = "INVALID_MODEL";
        public static readonly string InvalidModelMessage = "The provided model is not a valid option. Valid options are: " + string.Join(", ", Enum.GetNames(typeof(WhisperModel)));

        public const string FileSizeExceeded = "FILE_SIZE_EXCEEDED";
        public const string FileSizeExceededMessage = "The provided file is too big. Max size is 30MB.";
        public const string InvalidLang = "INVALID_LANG";
        public const string InvalidLangMessage = "The provided language is not a valid option";
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

    /// <summary>
    /// Path to WhisperModels
    /// </summary>
    public static readonly Dictionary<WhisperModel, string> ModelFilePaths = new()
    {
        { WhisperModel.Tiny, Path.Combine(WhisperFolder, "ggml-tiny.bin") },
        { WhisperModel.Base, Path.Combine(WhisperFolder, "ggml-base.bin") },
        { WhisperModel.Small, Path.Combine(WhisperFolder, "ggml-small.bin") },
        { WhisperModel.Medium, Path.Combine(WhisperFolder, "ggml-medium.bin") },
        { WhisperModel.Large, Path.Combine(WhisperFolder, "ggml-large.bin") }
    };

    public static readonly Dictionary<bool, string> OutputFormatMapping = new()
    {
        { true, "-ocsv" },
        { false, "-otxt" }
    };

    public class WhisperTimeStampJson
    {
        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("end")]
        public int End { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}

public static class GlobalDownloads
{
    public static async Task DownloadModels(WhisperModel whisperModel)
    {
        var modelString = whisperModel.ToString().ToLower();
        // https://huggingface.co/datasets/ggerganov/whisper.cpp/tree/main
        var modelUrl = $"https://huggingface.co/datasets/ggerganov/whisper.cpp/resolve/main/ggml-{modelString}.bin";
        var modelPath = Path.Combine(WhisperFolder, $"ggml-{modelString}.bin");

        string[] curlArgs = { "-L", modelUrl, "-o", modelPath };
        try
        {
            await Cli.Wrap("curl")
                .WithArguments(arg =>
                {
                    foreach (var curlArg in curlArgs)
                        arg.Add(curlArg);
                })
                .ExecuteAsync();
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("Failed to download the model".Pastel(ConsoleColor.Red));
            Environment.Exit(1);
        }
    }
}

public static class GlobalChecks
{
    public static async Task CheckForFFmpeg()
    {
        try
        {
            await Cli.Wrap("ffmpeg")
                .WithArguments("-version")
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .ExecuteAsync();
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("FFmpeg is not installed".Pastel(ConsoleColor.Red));
            Environment.Exit(1);
        }
    }

    public static async Task CheckForWhisper()
    {
        try
        {
            await Cli.Wrap(WhisperExecPath)
                .WithArguments("-h")
                .ExecuteAsync();
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("Whisper is not installed".Pastel(ConsoleColor.Red));
            Environment.Exit(1);
        }
    }
}