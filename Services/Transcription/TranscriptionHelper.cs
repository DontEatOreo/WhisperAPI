using Whisper.net;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public class TranscriptionHelper
{
    public async Task<PostResponseRoot> Transcribe(TranscriptionOptions options, CancellationToken token)
    {
        using var whisperFactory = WhisperFactory.FromPath(options.ModelPath, libraryPath: Path.Combine(Environment.CurrentDirectory, "libwhisper.dylib"));
        var builder = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount);

        var notNull = options.Language is not null;
        var withLanguage = notNull && options.Language is not "auto";
        var autoLanguage = notNull && options.Language is "auto";
        if (withLanguage)
            builder = builder.WithLanguage(options.Language!);
        if (autoLanguage)
            builder = builder.WithLanguageDetection();

        if (options.Translate)
            builder = builder.WithTranslate();
        token.ThrowIfCancellationRequested();

        WhisperProcessor processor;
        try
        {
            processor = builder.Build();
        }
        catch (Exception)
        {
            const string error = "Couldn't process the file";
            throw new FileProcessingException(error);
        }

        await using var fileStream = File.OpenRead(options.AudioFile);

        List<PostResponse> responses = new();
        await foreach (var result in processor.ProcessAsync(fileStream, token))
        {
            token.ThrowIfCancellationRequested();
            PostResponse postResponse = new(
                result.Start.TotalSeconds,
                result.End.TotalSeconds,
                result.Text.Trim(),
                result.Probability);
            responses.Add(postResponse);
        }

        PostResponseRoot root = new(responses.ToArray(), responses.Count);
        return root;
    }
}