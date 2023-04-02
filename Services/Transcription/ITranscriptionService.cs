using JetBrains.Annotations;
using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public interface ITranscriptionService
{
    Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request,
        CancellationToken token);

    [UsedImplicitly]
    Task<(string? transcription, string? errorCode, string? errorMessage)> ProcessAudioTranscription(string fileName,
        string wavFile,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp,
        CancellationToken token);

    PostResponse FailResponse(string? errorCode, string? errorMessage);
}