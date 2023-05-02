using JetBrains.Annotations;
using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public interface ITranscriptionService
{
    Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request, CancellationToken token);

    [UsedImplicitly]
    Task<string> TranscribeAudio(AudioTranscriptionOptions o, CancellationToken token);
}