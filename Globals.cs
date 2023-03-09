using System.Diagnostics;
using CliWrap;
using Serilog;
using static WhisperAPI.Globals;

namespace WhisperAPI;

public static class Globals
{
    #region Strings

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

    /// <summary>
    /// This is the URL for the Whisper source code
    /// </summary>
    public const string WhisperUrl = "https://github.com/ggerganov/whisper.cpp/archive/refs/heads/master.zip";

    #endregion

    /// <summary>
    /// CPU Thread Count
    /// </summary>
    public static readonly int ThreadCount = Environment.ProcessorCount;

    /// <summary>
    ///  A Global static instance of HttpClient
    /// </summary>
    public static readonly HttpClient HttpClient = new();

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

        public const string InvalidLanguage = "INVALID_LANG";
        public const string InvalidLanguageMessage = "The provided language is not a valid option";
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
}

public static class GlobalDownloads
{
    public static async Task DownloadModels(WhisperModel whisperModel)
    {
        var modelString = whisperModel.ToString().ToLower();
        // Source: https://huggingface.co/datasets/ggerganov/whisper.cpp/tree/main
        var modelUrl = $"https://huggingface.co/datasets/ggerganov/whisper.cpp/resolve/main/ggml-{modelString}.bin";
        var modelPath = Path.Combine(WhisperFolder, $"ggml-{modelString}.bin");

        var download = await Globals.HttpClient.GetAsync(modelUrl).ContinueWith(async task =>
        {
            using var response = await task;
            using var content = response.Content;
            await using FileStream fileStream = new(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(fileStream);
        });
        if (!download.IsCompletedSuccessfully)
            Log.Error("Failed to download the model");
    }

    public static async Task DownloadWhisper()
    {
        var fileName = Path.GetFileName(WhisperUrl);

        var tempPath = Path.GetTempPath();
        var zipPath = Path.Combine(tempPath, fileName);
        var unzipPath = Path.Combine(tempPath, "whisper.cpp-master");
        if (Directory.Exists(unzipPath))
            Directory.Delete(unzipPath, true);

        Log.Information("Downloading Whisper...");
        await Globals.HttpClient.GetAsync(WhisperUrl).ContinueWith(async task =>
        {
            using var response = await task;
            using var content = response.Content;
            await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(fileStream);
        });

        Log.Information("Unzipping Whisper...");
        await Cli.Wrap("unzip")
            .WithArguments(arg =>
            {
                arg.Add(zipPath);
            })
            .WithWorkingDirectory(tempPath)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        // For some reason using Cli.Wrap it's not possible to compile Whisper
        Log.Information("Compiling Whisper...");
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "make",
                WorkingDirectory = unzipPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        Log.Information("Finished compiling Whisper");

        if (File.Exists(WhisperExecPath))
            File.Delete(WhisperExecPath);

        if (!Directory.Exists(WhisperFolder))
            Directory.CreateDirectory(WhisperFolder);

        File.Move(Path.Combine(unzipPath, "main"), Path.Combine(WhisperFolder, "main"));
        File.Delete(zipPath);
        Directory.Delete(unzipPath, true);
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
            await Console.Error.WriteLineAsync("FFmpeg is not installed");
            Log.Error("FFmpeg is not installed");
            Environment.Exit(1);
        }
    }

    public static async Task CheckForWhisper()
    {
        try
        {
            await Cli.Wrap(WhisperExecPath)
                .WithArguments("-h")
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .ExecuteAsync();
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync("Whisper is not installed");
            Log.Error("Whisper is not installed");
            await GlobalDownloads.DownloadWhisper();
        }
    }

    /// <summary>
    /// We're using Process because Cli.Wrap doesn't work with make for some odd reason
    /// </summary>
    public static async Task CheckForMake()
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "make",
                Arguments = "-v",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            Log.Error("Make is not installed");
            Environment.Exit(1);
        }
    }
}