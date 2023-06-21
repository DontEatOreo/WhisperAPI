using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public interface ITranscriptionService
{
    Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request, CancellationToken token);

    // ReSharper disable once UnusedMemberInSuper.Global
    Task<string> TranscribeAudio(AudioTranscriptionOptions o, CancellationToken token);
}