using MediatR;
using Whisper.net;
using Whisper.net.Ggml;

namespace WhisperAPI.Models;

/// <summary>
/// Represents the options for a Whisper request.
/// </summary>
/// <param name="WavFile">The path to the WAV file to be processed.</param>
/// <param name="Language">The language of the audio file. Can be null if the language is unknown.</param>
/// <param name="Translate">Whether to translate the audio file to English.</param>
/// <param name="WhisperModel">The type of GGML model to use for processing the audio file.</param>
/// <returns>A <see cref="JsonResponse"/> object representing the response from the Whisper API.</returns>
public sealed record WhisperOptions(
    string WavFile, string? Language, bool Translate, GgmlType WhisperModel) : IRequest<List<SegmentData>>;