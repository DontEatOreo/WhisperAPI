using System.Globalization;
using System.Text.Json;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Services.Audio;
using ILogger = Serilog.ILogger;

namespace WhisperAPI.Services.Transcription;

public class TranscriptionService : ITranscriptionService
{
    #region Ctor

    private readonly Globals _globals;
    private readonly TranscriptionHelper _transcriptionHelper;
    private readonly IAudioConversionService _audioConversionService;
    private readonly ILogger _logger;

    public TranscriptionService(Globals globals,
        TranscriptionHelper transcriptionHelper,
        IAudioConversionService audioConversionService,
        ILogger logger)
    {
        _globals = globals;
        _transcriptionHelper = transcriptionHelper;
        _audioConversionService = audioConversionService;
        _logger = logger;
    }

    #endregion Ctor

    #region Methods

    public async Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request, CancellationToken token)
    {
        var lang = ValidateLanguage(request.Lang);
        var modelEnum = ValidateModel(request.Model);

        var filePath = await SaveAudioFileAsync(file, token);

        var filePathNoExt = $"{Path.GetFileNameWithoutExtension(filePath)}.wav";
        var wavFilePath = Path.Combine(_globals.AudioFilesFolder, filePathNoExt);
        AudioTranscriptionOptions options = new()
        {
            FileName = filePath,
            WavFile = wavFilePath,
            Language = lang,
            Translate = request.Translate,
            WhisperModel = modelEnum,
            TimeStamp = request.TimeStamps
        };

        var result = await TranscribeAudio(options, token);

        return new PostResponse
        {
            Result = request.TimeStamps
                ? JsonSerializer.Deserialize<List<TimeStamp>>(result)
                : result
        };
    }

    private string ValidateLanguage(string lang)
    {
        lang = lang.Trim().ToLower();
        if (lang is "auto")
        {
            return lang;
        }

        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
        var cultureInfo = cultures.FirstOrDefault(c => c.TwoLetterISOLanguageName == lang || c.NativeName.Contains(lang));

        if (cultureInfo is not null)
            return cultureInfo.TwoLetterISOLanguageName;

        _logger.Warning("Invalid language: {Lang}", lang);
        throw new InvalidLanguageException("Invalid language");
    }

    private static WhisperModel ValidateModel(string model)
    {
        if (Enum.TryParse(model, true, out WhisperModel modelEnum))
            return modelEnum;

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

    public async Task<string> TranscribeAudio(AudioTranscriptionOptions o, CancellationToken token)
    {
        var model = _globals.ModelFilePaths[o.WhisperModel];
        var format = _globals.OutputFormatMapping[o.TimeStamp];

        _transcriptionHelper.DownloadModelIfNotExists(o.WhisperModel, model);

        var distFile = $"{o.WavFile}.{format[2..]}";
        var transcribedFilePath = Path.Combine(_globals.AudioFilesFolder, distFile);
        try
        {
            await _audioConversionService.ConvertToWavAsync(o.FileName, o.WavFile);

            token.ThrowIfCancellationRequested();

            TranscriptionOptions options = new()
            {
                AudioFile = o.WavFile,
                Language = o.Language,
                Translate = o.Translate,
                ModelPath = model,
                OutputFormat = format
            };
            await _transcriptionHelper.Transcribe(options, token);

            token.ThrowIfCancellationRequested();

            if (o.TimeStamp)
            {
                var jsonLines = _transcriptionHelper.ConvertToJson(transcribedFilePath);
                var serialized = JsonSerializer.Serialize(jsonLines).Trim();
                return serialized;
            }

            var transcribedText = await File.ReadAllTextAsync(transcribedFilePath, token);
            return transcribedText.Trim();
        }
        catch (OperationCanceledException)
        {
            const string cancelled = "Transcription cancelled";
            _logger.Warning(cancelled);
        }
        finally
        {
            DeleteFilesAsync(o.FileName, o.WavFile, transcribedFilePath);
        }

        const string error = "Transcription failed";
        throw new FileProcessingException(error);
    }

    private static void DeleteFilesAsync(params string[] files)
    {
        foreach (var file in files.Where(File.Exists)) File.Delete(file);
    }

    #endregion Methods
}