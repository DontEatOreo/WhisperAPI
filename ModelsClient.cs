using ILogger = Serilog.ILogger;

namespace WhisperAPI;

public class DownloadModelsClient
{
    private readonly Globals _globals;
    private readonly ILogger _logger;
    private readonly HttpClient _client;

    public DownloadModelsClient(Globals globals, ILogger logger, HttpClient client)
    {
        _globals = globals;
        _logger = logger;
        _client = client;
    }

    public async Task Get(string url, string path)
    {
        using var response = await _client.GetAsync(url);
        var redirectUrl = response.RequestMessage?.RequestUri;
        var model = await _client.GetByteArrayAsync(redirectUrl);
        await using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.WriteAsync(model);

        _logger.Information("Downloaded {WhisperModel} model", path);
    }
}