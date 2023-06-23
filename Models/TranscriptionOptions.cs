namespace WhisperAPI.Models;

public record TranscriptionOptions(
    string AudioFile,
    string? Language,
    bool Translate,
    string ModelPath
);