using JetBrains.Annotations;
using MediatR;
using Whisper.net;
using Whisper.net.Ggml;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;

namespace WhisperAPI.Handlers;

[UsedImplicitly]
public sealed class TranscriptHandler(Globals globals) : IRequestHandler<WhisperOptions, List<SegmentData>>
{
    private const string ErrorProcessing = "Couldn't process the file";
    private const string MissingFile = "File not found";

    /// <summary>
    /// Handles the request to process a transcript file and returns a JSON response.
    /// </summary>
    /// <param name="request">The request containing the options for processing the transcript file.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A JSON response containing the processed transcript data.</returns>
    public async Task<List<SegmentData>> Handle(WhisperOptions request, CancellationToken token)
    {
        var modelType = request.WhisperModel;
        var language = request.Language?.ToLower();

        if (language?.Contains("en") is true)
        {
            modelType = modelType switch
            {
                GgmlType.Tiny => GgmlType.TinyEn,
                GgmlType.Base => GgmlType.BaseEn,
                GgmlType.Small => GgmlType.SmallEn,
                GgmlType.Medium => GgmlType.MediumEn,
                _ => modelType
            };
        }

        var modelPath = Path.Combine(globals.WhisperFolder, $"{modelType}.bin");
        var modelExists = File.Exists(modelPath);
        if (modelExists is false)
        {
            await using var stream = await WhisperGgmlDownloader
                    .GetGgmlModelAsync(modelType, cancellationToken: token);

            await using var modelStream = File.Create(modelPath);

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
        
        builder = language?.Contains("auto") is false 
            ? builder.WithLanguage(language)
            : builder.WithLanguageDetection();

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
        if (wavExists is false)
        {
            await processor.DisposeAsync();
            throw new FileNotFoundException(MissingFile, request.WavFile);
        }
        await using var fileStream = File.OpenRead(request.WavFile);

        List<SegmentData> segments = [];
        try
        {
            await foreach (var data in processor.ProcessAsync(fileStream, token))
                segments.Add(data);
        }
        catch (Exception)
        {
            throw new FileProcessingException(ErrorProcessing);
        }
        finally
        {
            await processor.DisposeAsync();
        }

        return segments;
    }
}