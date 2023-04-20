using System.Globalization;
using System.Text.Json;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Services.Audio;
using ILogger = Serilog.ILogger;

namespace WhisperAPI.Services.Transcription;

public class TranscriptionService : ITranscriptionService
{
    #region Constructor

    private readonly Globals _globals;
    private readonly TranscriptionHelper _transcriptionHelper;
    private readonly IAudioConversionService _audioConversionService;
    private readonly ILogger _logger;

    #endregion

    #region Methods

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

    public async Task<PostResponse> HandleTranscriptionRequest(IFormFile file, PostRequest request, CancellationToken token)
    {
        var lang = request.Lang.Trim().ToLower();
        if (lang is not "auto")
        {
            var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
            if (lang.Length is 2)
            {
                foreach (var culture in cultures)
                {
                    if (culture.TwoLetterISOLanguageName != lang)
                        continue;

                    lang = culture.EnglishName;
                    break;
                }
            }

            foreach (var culture in cultures)
            {
                if (!lang.Contains(culture.NativeName))
                    continue;

                lang = culture.EnglishName;
                break;
            }

            if (cultures.All(c => !lang.Contains(c.EnglishName) || !lang.Contains(c.NativeName)))
            {
                _logger.Warning("Invalid language: {Lang}", lang);
                throw new InvalidLanguageException("Invalid language");
            }

            lang = new CultureInfo(lang).TwoLetterISOLanguageName;
        }

        if (!Enum.TryParse(request.Model, true, out WhisperModel modelEnum))
        {
            _logger.Warning("Invalid model: {Model}", request.Model);
            throw new InvalidModelException("Invalid model");
        }

        // Check if the audio files folder exists, if not create it
        if (!Directory.Exists(_globals.AudioFilesFolder))
            Directory.CreateDirectory(_globals.AudioFilesFolder);

        // Create the files
        var fileId = Guid.NewGuid().ToString();
        var fileExt = Path.GetExtension(file.FileName);
        var filePath = Path.Combine(_globals.AudioFilesFolder, $"{fileId}{fileExt}");
        var wavFilePath = Path.Combine(_globals.AudioFilesFolder, $"{fileId}.wav");
        await using FileStream fs = new(filePath, FileMode.Create);
        await file.CopyToAsync(fs, token).ConfigureAwait(false);

        var result = await ProcessAudioTranscription(
            filePath,
            wavFilePath,
            lang,
            request.Translate,
            modelEnum,
            request.TimeStamps,
            token);

        return new PostResponse
        {
            Success = true,
            Result = request.TimeStamps
                ? JsonSerializer.Deserialize<List<TimeStamp>>(result)
                : result
        };
    }

    public async Task<string> ProcessAudioTranscription(
        string fileName,
        string wavFile,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp,
        CancellationToken token)
    {
        var selectedModelPath = _globals.ModelFilePaths[whisperModel];
        var selectedOutputFormat = _globals.OutputFormatMapping[timeStamp];

        await _transcriptionHelper.DownloadModelIfNotExists(whisperModel, selectedModelPath);

        var transcribedFilePath = Path.Combine(_globals.AudioFilesFolder, $"{wavFile}.{selectedOutputFormat[2..]}");
        try
        {
            await _audioConversionService.ConvertToWavAsync(fileName, wavFile);

            token.ThrowIfCancellationRequested();

            await _transcriptionHelper.Transcribe(wavFile, lang, translate, selectedModelPath, selectedOutputFormat,
                token);

            token.ThrowIfCancellationRequested();

            if (timeStamp)
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
            _logger.Warning("Transcription cancelled");
        }
        finally
        {
            DeleteFilesAsync(fileName, wavFile, transcribedFilePath);
        }

        throw new FileProcessingException("File processing failed");
    }
    private static void DeleteFilesAsync(params string[] files)
    {
        foreach (var file in files.Where(File.Exists)) File.Delete(file);
    }

    #endregion
}