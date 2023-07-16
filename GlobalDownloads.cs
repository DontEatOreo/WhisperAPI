using Microsoft.Extensions.Options;
using Whisper.net.Ggml;

namespace WhisperAPI;

public class GlobalDownloads
{
    private readonly WhisperSettings _whisperSettings;
    private readonly Serilog.ILogger _logger;
    
    private const string ErrorMessage = "Whisper folder not set in appsettings.json";

    public GlobalDownloads(IOptions<WhisperSettings> options, Serilog.ILogger logger)
    {
        _whisperSettings = options.Value;
        _logger = logger;
    }

    public async Task DownloadModel(GgmlType type)
    {
        _logger.Information("Downloading {WhisperModel} model...", type.ToString());
        await using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(type);
        
        if (_whisperSettings.Folder is null)
        {
            _logger.Error(ErrorMessage);
            throw new DirectoryNotFoundException(ErrorMessage);
        }
        
        var whisperFolder = Path.GetFullPath(_whisperSettings.Folder);
        if (!Directory.Exists(whisperFolder))
            throw new DirectoryNotFoundException(ErrorMessage);
        
        var modelPath = Path.Combine(whisperFolder, $"ggml-{type.ToString().ToLower()}.bin");
        await using var fileWriter = File.OpenWrite(modelPath);
        await modelStream.CopyToAsync(fileWriter);
        _logger.Information("Downloaded {WhisperModel} model", type.ToString());
    }
}