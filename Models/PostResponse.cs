using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public class PostResponse
{
    [JsonPropertyName("result")]
    public object? Result { get; set; }
}