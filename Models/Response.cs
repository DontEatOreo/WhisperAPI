using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

public record ResponseRoot(
    [property: JsonPropertyName("data")] IEnumerable<Response> Data,
    [property: JsonPropertyName("count")] int Count);

public record Response(
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")] double End,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("probability")] float Probability
);