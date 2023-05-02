using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public struct PostResponse
{
    [JsonPropertyName("result")]
    public object? Result { get; set; }
}