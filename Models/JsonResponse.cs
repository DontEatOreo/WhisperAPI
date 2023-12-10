using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace WhisperAPI.Models;

/// <summary>
/// Represents a JSON response.
/// </summary>
[UsedImplicitly]
public sealed class JsonResponse
{
    /// <summary>
    /// Array of response data.
    /// </summary>
    /// <value>
    /// The list of response data.
    /// </value>
    [JsonPropertyName("data")]
    public List<ResponseData> Data { get; init; } = [];

    /// <summary>
    /// Count of sentences in the response.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// The base response data.
/// </summary>
/// <param name="start">Start time of the sentence.</param>
/// <param name="end">End time of the sentence.</param>
/// <param name="text">The literal text of the sentence.</param>
public sealed class ResponseData(double start, double end, string text)
{
    /// <summary>
    /// Start time of the sentence.
    /// </summary>
    [UsedImplicitly] public double Start { get; init; } = start;

    /// <summary>
    /// End time of the sentence.
    /// </summary>
    [UsedImplicitly] public double End { get; init; } = end;

    /// <summary>
    /// The literal text of the sentence.
    /// </summary>
    [UsedImplicitly] public string Text { get; init; } = text;

    /// <summary>
    /// Empty constructor for XML serialization.
    /// </summary>
    public ResponseData() : this(0, 0, string.Empty) { }
}