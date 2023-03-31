using System.Diagnostics;
using CliWrap;
using JetBrains.Annotations;
using Serilog;
using static WhisperAPI.Globals;

namespace WhisperAPI;

public interface IGlobalDownloads
{
    Task DownloadModels(WhisperModel whisperModel);
}

public interface IGlobalChecks
{
    [UsedImplicitly]
    Task CheckForFFmpeg();
    [UsedImplicitly]
    Task CheckForWhisper();
    [UsedImplicitly]
    Task CheckForMake();
}

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

/// <summary>
/// Struct containing all error codes and messages
/// </summary>
public static class ErrorCodesAndMessages
{
    public const string? NoFile = "NO_FILE";
    public const string? NoFileMessage = "No file was provided in the request.";

    public const string InvalidFileType = "INVALID_FILE_TYPE";
    public const string InvalidFileTypeMessage = "The provided file is not a valid audio or video file.";

    public const string InvalidModel = "INVALID_MODEL";
    public static readonly string InvalidModelMessage = "The provided model is not a valid option. Valid options are: " + string.Join(", ", Enum.GetNames(typeof(WhisperModel)));

    public const string InvalidLanguage = "INVALID_LANG";
    public const string InvalidLanguageMessage = "The provided language is not a valid option";
}

public class GlobalDownloads : IGlobalDownloads
{

    private readonly IHttpClientFactory _httpClient;
    private readonly Globals _globals;

    public GlobalDownloads(IHttpClientFactory httpClient, Globals globals)
    {
        _httpClient = httpClient;
        _globals = globals;
    }

    public async Task DownloadModels(WhisperModel whisperModel)
    {
        var modelString = whisperModel.ToString().ToLower();
        // Source: https://huggingface.co/datasets/ggerganov/whisper.cpp/tree/main
        var modelUrl = $"https://huggingface.co/datasets/ggerganov/whisper.cpp/resolve/main/ggml-{modelString}.bin";
        var modelPath = Path.Combine(_globals.WhisperFolder, $"ggml-{modelString}.bin");

        using var client = _httpClient.CreateClient();
        var download = await client.GetAsync(modelUrl);
        if (!download.IsSuccessStatusCode)
        {
            Log.Error("Failed to download the model");
            return;
        }

        using var content = download.Content;
        await using FileStream fileStream = new(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream);
    }

    public async Task DownloadWhisper()
    {
        var fileName = Path.GetFileName(WhisperUrl);

        var tempPath = Path.GetTempPath();
        var zipPath = Path.Combine(tempPath, fileName);
        var unzipPath = Path.Combine(tempPath, "whisper.cpp-master");
        if (Directory.Exists(unzipPath))
            Directory.Delete(unzipPath, true);

        Log.Information("Downloading Whisper...");
        var client = _httpClient.CreateClient();
        var download = await client.GetAsync(WhisperUrl);
        if (!download.IsSuccessStatusCode)
        {
            Log.Error("Failed to download Whisper");
            return;
        }
        var content = download.Content;
        await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream);

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

        if (File.Exists(_globals.WhisperExecPath))
            File.Delete(_globals.WhisperExecPath);

        if (!Directory.Exists(_globals.WhisperFolder))
            Directory.CreateDirectory(_globals.WhisperFolder);

        File.Move(Path.Combine(unzipPath, "main"), Path.Combine(_globals.WhisperFolder, "main"));
        File.Delete(zipPath);
        Directory.Delete(unzipPath, true);
    }
}

public class GlobalChecks : IGlobalChecks
{
    #region Consturctor

    private readonly Globals _globals;
    private readonly GlobalDownloads _globalDownloads;

    public GlobalChecks(Globals globals, GlobalDownloads globalDownloads)
    {
        _globals = globals;
        _globalDownloads = globalDownloads;
    }

    #endregion

    #region Methods

    public async Task CheckForFFmpeg()
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

    public async Task CheckForWhisper()
    {
        try
        {
            await Cli.Wrap(_globals.WhisperExecPath)
                .WithArguments("-h")
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .ExecuteAsync();
        }
        catch (Exception)
        {
            Log.Information("Whisper is not installed");
            await _globalDownloads.DownloadWhisper();
        }
    }

    /// <summary>
    /// We're using Process because Cli.Wrap doesn't work with make for some odd reason
    /// </summary>
    public async Task CheckForMake()
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

    #endregion
}