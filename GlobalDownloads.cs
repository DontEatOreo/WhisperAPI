using System.Diagnostics;
using System.IO.Compression;

namespace WhisperAPI;

public class GlobalDownloads
{
    #region Ctor

    private readonly Globals _globals;
    private readonly ModelsClient _modelsClient;
    private readonly WhisperClient _whisperClient;
    private readonly Serilog.ILogger _logger;

    public GlobalDownloads(Globals globals, ModelsClient modelsClient, Serilog.ILogger logger, WhisperClient whisperClient)
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
        // Source: https://huggingface.co/ggerganov/whisper.cpp/resolve/main/
        var modelName = $"ggml-{modelString}.bin";
        Uri modelUri = new($"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{modelName}");
        var modelPath = Path.Combine(_globals.WhisperFolder, modelName);

        _logger.Information("Downloading {WhisperModel} model...", whisperModel);
        await _modelsClient.Get(modelUri, modelPath);
        _logger.Information("Downloaded {WhisperModel} model", whisperModel);
    }

    public async Task Whisper()
    {
        var fileName = Path.GetFileName(_globals.WhisperUrl.ToString());

        var tempPath = Path.GetTempPath();
        var zipPath = Path.Combine(tempPath, fileName);
        var unzipPath = Path.Combine(tempPath, "whisper.cpp-master");
        if (Directory.Exists(unzipPath))
            Directory.Delete(unzipPath, true);

        _logger.Information("Downloading Whisper...");
        await _whisperClient.Get(_globals.WhisperUrl, zipPath);
        _logger.Information("Finish downloading: {WhisperModel} model", fileName);

        _logger.Information("Unzipping Whisper...");
        ZipFile.ExtractToDirectory(zipPath, Path.GetTempPath());

        _logger.Information("Compiling Whisper...");
        ProcessStartInfo makeInfo = new()
        {
            FileName = "make",
            WorkingDirectory = unzipPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using Process process = new() { StartInfo = makeInfo };
        process.Start();
        await process.WaitForExitAsync();
        _logger.Information("Finished compiling Whisper");

        if (File.Exists(_globals.WhisperExecPath))
            File.Delete(_globals.WhisperExecPath);

        if (!Directory.Exists(_globals.WhisperFolder))
            Directory.CreateDirectory(_globals.WhisperFolder);

        var source = Path.Combine(unzipPath, "main");
        var dest = Path.Combine(_globals.WhisperFolder, "main");
        File.Move(source, dest);

        File.Delete(zipPath);
        Directory.Delete(unzipPath, true);
    }

    #endregion Methods
}