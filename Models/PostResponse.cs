using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public record PostResponseRoot(
    [property: JsonPropertyName("data")] PostResponse[] Data,
    [property: JsonPropertyName("count")] int Count);

public record PostResponse(
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")] double End,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("probability")] float Probability
);