using System.Diagnostics;
using System.IO.Compression;
using ILogger = Serilog.ILogger;

namespace WhisperAPI;

public class GlobalDownloads
{
    #region Ctor

    private readonly Globals _globals;
    private readonly ModelsClient _modelsClient;
    private readonly WhisperClient _whisperClient;
    private readonly ILogger _logger;

    public GlobalDownloads(Globals globals, ModelsClient modelsClient, ILogger logger, WhisperClient whisperClient)
    {
        _globals = globals;
        _modelsClient = modelsClient;
        _logger = logger;
        _whisperClient = whisperClient;
    }

    #endregion Ctor

    #region Methods

    public async Task Model(WhisperModel whisperModel)
    {
        var modelString = whisperModel.ToString().ToLower();
        // Source: https://huggingface.co/datasets/ggerganov/whisper.cpp/tree/main
        var modelUrl = $"https://huggingface.co/datasets/ggerganov/whisper.cpp/resolve/main/ggml-{modelString}.bin";
        var modelPath = Path.Combine(_globals.WhisperFolder, $"ggml-{modelString}.bin");

        _logger.Information("Downloading {WhisperModel} model...", whisperModel);
        await _modelsClient.Get(modelUrl, modelPath);
        _logger.Information("Downloaded {WhisperModel} model", whisperModel);
    }

    public async Task Whisper()
    {
        var fileName = Path.GetFileName(_globals.WhisperUrl);

        var tempPath = Path.GetTempPath();
        var zipPath = Path.Combine(tempPath, fileName);
        var unzipPath = Path.Combine(tempPath, "whisper.cpp-master");
        if (Directory.Exists(unzipPath))
            Directory.Delete(unzipPath, true);

        _logger.Information("Downloading Whisper...");
        await _whisperClient.Get(_globals.WhisperUrl, zipPath);
        _logger.Information("Downloaded {WhisperModel} model", fileName);

        _logger.Information("Unzipping Whisper...");
        ZipFile.ExtractToDirectory(zipPath, Path.GetTempPath());

        _logger.Information("Compiling Whisper...");
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
        _logger.Information("Finished compiling Whisper");

        if (File.Exists(_globals.WhisperExecPath))
            File.Delete(_globals.WhisperExecPath);

        if (!Directory.Exists(_globals.WhisperFolder))
            Directory.CreateDirectory(_globals.WhisperFolder);

        File.Move(Path.Combine(unzipPath, "main"), Path.Combine(_globals.WhisperFolder, "main"));
        File.Delete(zipPath);
        Directory.Delete(unzipPath, true);
    }

    #endregion Methods
}