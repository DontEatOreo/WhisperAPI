namespace WhisperAPI;

public class WhisperClient
{
    private readonly HttpClient _client;

    public WhisperClient(HttpClient client)
    {
        _client = client;
    }

    public async Task Get(Uri url, string zipPath)
    {
        using var response = await _client.GetAsync(url);
        var redirectUrl = response.RequestMessage?.RequestUri;
        var whisper = await _client.GetByteArrayAsync(redirectUrl);

        await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.WriteAsync(whisper);
        await fileStream.DisposeAsync();
    }
}