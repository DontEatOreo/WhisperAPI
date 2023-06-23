using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public record TimeStamp(
    [property: JsonPropertyName("start")] TimeSpan Start,
    [property: JsonPropertyName("end")] TimeSpan End,
    [property: JsonPropertyName("probability")] float? Probability,
    [property: JsonPropertyName("text")] string? Text
);