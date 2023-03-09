using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public struct PostRequest
{
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("time_stamps")]
    public bool? TimeStamps { get; set; }

    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    [JsonPropertyName("translate")]
    public bool? Translate { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}