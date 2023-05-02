namespace WhisperAPI;

public class ModelsClient
{
    private readonly HttpClient _client;

    public ModelsClient(HttpClient client)
    {
        _client = client;
    }

    public async Task Get(string url, string filePath)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using FileStream fileStream = new(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);
    }
}