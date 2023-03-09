using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public struct TimeStamp
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}