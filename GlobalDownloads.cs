using Microsoft.Extensions.Options;
using Whisper.net.Ggml;

namespace WhisperAPI;

public class GlobalDownloads
{
    private readonly WhisperSettings _whisperSettings;
    private readonly Serilog.ILogger _logger;

    public GlobalDownloads(IOptions<WhisperSettings> options, Serilog.ILogger logger)
    {
        _whisperSettings = options.Value;
        _logger = logger;
    }

    public async Task DownloadModel(GgmlType ggmlType)
    {
        _logger.Information("Downloading {WhisperModel} model...", ggmlType.ToString());
        await using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
        var whisperFolder = Path.GetFullPath(_whisperSettings.Folder);
        if (!Directory.Exists(whisperFolder))
            throw new DirectoryNotFoundException($"Whisper folder not found at {whisperFolder}");
        var modelPath = Path.Combine(whisperFolder, $"ggml-{ggmlType.ToString().ToLower()}.bin");
        await using var fileWriter = File.OpenWrite(modelPath);
        await modelStream.CopyToAsync(fileWriter);
        _logger.Information("Downloaded {WhisperModel} model", ggmlType.ToString());
    }
}