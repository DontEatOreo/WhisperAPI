using System.Text.Json;
using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public interface ITranscriptionService
{
    Task<JsonDocument> Handler(IFormFile file, PostRequest request, CancellationToken token);

    // ReSharper disable once UnusedMemberInSuper.Global
    Task<JsonDocument> TranscribeAudio(AudioOptions o, CancellationToken token);
}