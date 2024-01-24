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
        modelType = PrepareModel(modelType, language);
        var whisperFactory = await GetWhisperFactory(modelType, token);
        var processor = CreateProcessor(language, request, whisperFactory);
        var segments = await ProcessTranscript(request, processor, token);
        return segments;
    }

    /// <summary>
    /// Adjusts modelType based on the provided language.
    /// </summary>
    /// <param name="modelType">The original GgmlType model.</param>
    /// <param name="language">The language for processing.</param>
    /// <returns>The updated model type according to the language requirements.</returns>
    private static GgmlType PrepareModel(GgmlType modelType, string? language)
    {
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
        return modelType;
    }

    /// <summary>
    /// Downloads and prepares the WhisperFactory from a provided model path.
    /// </summary>
    /// <param name="modelType">The model type to get the factory for.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A prepared WhisperFactory.</returns>
    /// <exception cref="FileProcessingException">Thrown when unable to create a WhisperFactory.</exception>
    private async Task<WhisperFactory> GetWhisperFactory(GgmlType modelType, CancellationToken token)
    {
        var modelPath = Path.Combine(globals.WhisperFolder, $"{modelType}.bin");
        var modelExists = File.Exists(modelPath);
        if (!modelExists)
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

        return whisperFactory;
    }

    /// <summary>
    /// Creates a processor for transcript using provided language, request parameters, and WhisperFactory.
    /// </summary>
    /// <param name="language">The language to be used for processing.</param>
    /// <param name="request">The request containing processing options.</param>
    /// <param name="whisperFactory">The WhisperFactory prepared for this processor.</param>
    /// <returns>A ready-to-use WhisperProcessor.</returns>
    /// <exception cref="FileProcessingException">Thrown when unable to build the processor.</exception>
    private static WhisperProcessor CreateProcessor(string? language, WhisperOptions request, WhisperFactory whisperFactory)
    {
        var builder = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount);

        builder = language?.Contains("auto") is false
            ? builder.WithLanguage(language)
            : builder.WithLanguageDetection();

        if (request.Translate)
        {
            builder = builder.WithTranslate();
        }

        WhisperProcessor processor;
        try
        {
            processor = builder.Build();
        }
        catch (Exception)
        {
            throw new FileProcessingException(ErrorProcessing);
        }

        return processor;
    }

    /// <summary>
    /// Processes the transcript using the provided request options and processor, splits the result into segments.
    /// </summary>
    /// <param name="request">The request containing options and file to process.</param>
    /// <param name="processor">The processor to be used for transcript processing.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A list of processed segment data.</returns>
    /// <exception cref="FileNotFoundException">Thrown when unable to find the WAV file.</exception>
    /// <exception cref="FileProcessingException">Thrown when the processing phase encounters any exception.</exception>
    private static async Task<List<SegmentData>> ProcessTranscript(WhisperOptions request, WhisperProcessor processor, CancellationToken token)
    {
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
