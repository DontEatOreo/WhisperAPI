using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

/// <summary>
/// Represents the JSON response returned by WhisperAPI.
/// </summary>
public sealed record JsonResponse(
    [property: JsonPropertyName("data")] IEnumerable<ResponseData> Data,
    [property: JsonPropertyName("count")] int Count);

/// <summary>
/// Represents a single response data object in the JSON response.
/// </summary>
public sealed record ResponseData(
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")] double End,
    [property: JsonPropertyName("text")] string Text
);