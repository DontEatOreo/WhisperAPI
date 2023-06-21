using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public class TimeStamp
{
    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}