using System.Text.Json.Serialization;

namespace WhisperAPI.Models;

/// <summary>
/// Represents a time stamp with start and end times, probability, and text.
/// </summary>
/// <param name="Start">The start time of the time stamp.</param>
/// <param name="End">The end time of the time stamp.</param>
/// <param name="Text">The text associated with the time stamp.</param>
public sealed record TimeStamp(
    [property: JsonPropertyName("start")] TimeSpan Start,
    [property: JsonPropertyName("end")] TimeSpan End,
    [property: JsonPropertyName("text")] string? Text
);