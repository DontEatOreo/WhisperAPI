using JetBrains.Annotations;
using MediatR;
using Whisper.net;
using Whisper.net.Ggml;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;

namespace WhisperAPI.Handlers;

[UsedImplicitly]
public sealed class TranscriptHandler(Globals globals) : IRequestHandler<WhisperOptions, JsonResponse>
{
    private const string ErrorProcessing = "Couldn't process the file";
    private const string MissingFile = "File not found";

    /// <summary>
    /// Handles the request to process a transcript file and returns a JSON response.
    /// </summary>
    /// <param name="request">The request containing the options for processing the transcript file.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A JSON response containing the processed transcript data.</returns>
    public async Task<JsonResponse> Handle(WhisperOptions request, CancellationToken token)
    {
        var modelName = request.WhisperModel.ToString().ToLower();
        var modelPath = Path.Combine(globals.WhisperFolder, $"{modelName}.bin");
        var modelExists = File.Exists(modelPath);

        if (modelExists is false)
        {
            await using var stream = await WhisperGgmlDownloader.GetGgmlModelAsync(request.WhisperModel,
                QuantizationType.NoQuantization, token);
            await using var modelStream = File.Create(Path.Combine(globals.WhisperFolder, $"{modelName}.bin"));

            await stream.CopyToAsync(modelStream, token);
        }

        WhisperFactory whisperFactory;
        try
        {
            whisperFactory = WhisperFactory.FromPath(modelPath);
        }
        catch (Exception)
        {
            throw new FileProcessingException(ErrorProcessing);
        }

        var builder = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount);

        var notNull = request.Language is not null;
        var withLanguage = notNull && request.Language is not "auto";
        var autoLanguage = notNull && request.Language is "auto";
        if (withLanguage)
            builder = builder.WithLanguage(request.Language!);
        if (autoLanguage)
            builder = builder.WithLanguageDetection();

        if (request.Translate)
            builder = builder.WithTranslate();

        WhisperProcessor processor;
        try
        {
            processor = builder.Build();
        }
        catch (Exception)
        {
            throw new FileProcessingException(ErrorProcessing);
        }

        var wavExists = File.Exists(request.WavFile);
        if (!wavExists)
            throw new FileNotFoundException(MissingFile, request.WavFile);
        await using var fileStream = File.OpenRead(request.WavFile);

        List<ResponseData> responses = [];
        try
        {
            await foreach (var data in processor.ProcessAsync(fileStream, token))
            {
                ResponseData jsonResponse = new(
                data.Start.TotalSeconds,
                data.End.TotalSeconds,
                data.Text.TrimEnd());
                responses.Add(jsonResponse);
            }
        }
        catch (Exception)
        {
            throw new FileProcessingException(ErrorProcessing);
        }
        finally
        {
            await processor.DisposeAsync();
        }

        JsonResponse root = new()
        {
            Data = responses,
            Count = responses.Count
        };
        return root;
    }
}