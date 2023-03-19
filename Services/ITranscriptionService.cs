using JetBrains.Annotations;
using WhisperAPI.Models;
using static WhisperAPI.Globals;

namespace WhisperAPI.Services;

public interface ITranscriptionService
{
    [UsedImplicitly]
    Task<(string? transcription, string? errorCode, string? errorMessage)> ProcessAudioTranscription(
        string fileBase64,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp);
    Task<PostResponse> HandleTranscriptionRequest(PostRequest request);

    PostResponse FailResponse(string? errorCode, string? errorMessage);
}