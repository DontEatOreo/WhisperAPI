using System.Globalization;
using System.Text.Json;
using MediatR;
using Whisper.net.Ggml;
using WhisperAPI.Commands;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Requests;

namespace WhisperAPI.Handlers;

public sealed class TranscribeAudioRequestHandler : IRequestHandler<TranscribeAudioRequest, JsonDocument>
{
    private readonly Globals _globals;
    private readonly GlobalDownloads _downloads;
    private readonly Serilog.ILogger _logger;
    private readonly IMediator _mediator;

    public TranscribeAudioRequestHandler(Globals globals,
        Serilog.ILogger logger, GlobalDownloads downloads, IMediator mediator)
    {
        _globals = globals;
        _logger = logger;
        _downloads = downloads;
        _mediator = mediator;
    }

    public async Task<JsonDocument> Handle(TranscribeAudioRequest request, CancellationToken token)
    {
        string? lang = null;
        if (request.Request.Lang is not null)
            lang = ValidateLanguage(request.Request.Lang);
        var modelEnum = ValidateModel(request.Request.Model);

        var filePath = await SaveAudioFileAsync(request.File, token);

        var filePathNoExt = $"{Path.GetFileNameWithoutExtension(filePath)}.wav";
        var wavFilePath = Path.Combine(_globals.AudioFilesFolder, filePathNoExt);
        AudioOptions options = new(filePath, wavFilePath, lang, request.Request.Translate, modelEnum);

        var result = await TranscribeAudio(options, token);
        var json = JsonSerializer.Serialize(result);
        var jsonDocument = JsonDocument.Parse(json);
        return jsonDocument;
    }

    private string ValidateLanguage(string lang)
    {
        lang = lang.Trim().ToLower();
        var isAuto = lang is "auto";
        if (isAuto)
            return lang;

        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
        if (cultures.Any(culture => culture.TwoLetterISOLanguageName == lang || culture.NativeName.Contains(lang)))
            return lang;

        _logger.Warning("Invalid language: {Lang}", lang);
        throw new InvalidLanguageException("Invalid language");
    }

    private static GgmlType ValidateModel(string model)
    {
        var parse = Enum.TryParse(model, true, out GgmlType ggmlType);
        if (ggmlType.ToString().ToLower().Contains("v1"))
            return GgmlType.Base; // v1 model exists but we don't want to use it
        if (parse)
            return ggmlType;

        throw new InvalidModelException("Invalid model");
    }

    private async Task<string> SaveAudioFileAsync(IFormFile file, CancellationToken token)
    {
        if (!Directory.Exists(_globals.AudioFilesFolder))
            Directory.CreateDirectory(_globals.AudioFilesFolder);

        // Create the files
        var fileId = Guid.NewGuid().ToString();
        var fileExt = Path.GetExtension(file.FileName);
        var filePath = Path.Combine(_globals.AudioFilesFolder, $"{fileId}{fileExt}");

        await using FileStream fs = new(filePath, FileMode.Create);
        await file.CopyToAsync(fs, token);

        return filePath;
    }

    private async Task<JsonDocument> TranscribeAudio(AudioOptions o, CancellationToken token)
    {
        var model = _globals.ModelFilePaths[o.WhisperModel];

        // check if model exists
        if (!File.Exists(model))
            await _downloads.DownloadModel(o.WhisperModel);

        var distFile = $"{o.WavFile}.wav";
        var transcribedFilePath = Path.Combine(_globals.AudioFilesFolder, distFile);

        try
        {
            token.ThrowIfCancellationRequested();
            ConvertToWavCommand convert = new(o.FileName, o.WavFile);
            await _mediator.Send(convert, token);
            token.ThrowIfCancellationRequested();

            AudioOptions options = new(o.FileName, o.WavFile, o.Language, o.Translate, o.WhisperModel);
            TranscribeAudioCommand audioCommand = new(options);

            var transcribe = await _mediator.Send(audioCommand, token);
            var json = JsonSerializer.Serialize(transcribe);
            var jsonDocument = JsonDocument.Parse(json);
            token.ThrowIfCancellationRequested();

            return jsonDocument;
        }
        catch (OperationCanceledException)
        {
            const string error = "Transcription failed";
            throw new FileProcessingException(error);
        }
        finally
        {
            DeleteFilesAsync(o.FileName, o.WavFile, transcribedFilePath);
        }
    }

    private static void DeleteFilesAsync(params string[] files)
    {
        foreach (var file in files.Where(File.Exists)) File.Delete(file);
    }
}