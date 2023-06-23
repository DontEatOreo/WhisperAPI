using Whisper.net.Ggml;

namespace WhisperAPI.Models;

public record AudioOptions(
    string FileName,
    string WavFile,
    string? Language,
    bool Translate,
    GgmlType WhisperModel
);