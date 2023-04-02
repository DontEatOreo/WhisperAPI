using JetBrains.Annotations;
using WhisperAPI.Models;

namespace WhisperAPI.Services;

public interface ITranscriptionService
{
    [UsedImplicitly]
    Task<(string? transcription, string? errorCode, string? errorMessage)> ProcessAudioTranscription(string fileName,
        string wavFile,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp);

    Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request);

    PostResponse FailResponse(string? errorCode, string? errorMessage);
}