using JetBrains.Annotations;
using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public interface ITranscriptionService
{
    Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request,
        CancellationToken token);

    [UsedImplicitly]
    Task<string> ProcessAudioTranscription(string fileName,
        string wavFile,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp,
        CancellationToken token);
}