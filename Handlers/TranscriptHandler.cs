using MediatR;
using Whisper.net;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Queries;

namespace WhisperAPI.Handlers;

public sealed class TranscriptHandler : IRequestHandler<TranscriptQuery, ResponseRoot>
{
    private readonly Globals _globals;

    public TranscriptHandler(Globals globals)
    {
        _globals = globals;
    }

    public async Task<ResponseRoot> Handle(TranscriptQuery request, CancellationToken token)
    {
        var modelPath = _globals.ModelFilePaths[request.Options.WhisperModel];
        using var whisperFactory = WhisperFactory.FromPath(modelPath);
        var builder = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount);

        var notNull = request.Options.Language is not null;
        var withLanguage = notNull && request.Options.Language is not "auto";
        var autoLanguage = notNull && request.Options.Language is "auto";
        if (withLanguage)
            builder = builder.WithLanguage(request.Options.Language!);
        if (autoLanguage)
            builder = builder.WithLanguageDetection();

        if (request.Options.Translate)
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

        await using var fileStream = File.OpenRead(request.Options.WavFile);

        List<Response> responses = new();
        await foreach (var result in processor.ProcessAsync(fileStream, token))
        {
            Response response = new(
                result.Start.TotalSeconds,
                result.End.TotalSeconds,
                result.Text.Trim(),
                result.Probability);
            responses.Add(response);
        }

        ResponseRoot root = new(responses.ToArray(), responses.Count);
        return root;
    }
}