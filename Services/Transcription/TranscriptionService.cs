using System.Globalization;
using System.Text.Json;
using Serilog;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Services.Audio;

namespace WhisperAPI.Services.Transcription;

public class TranscriptionService : ITranscriptionService
{
    #region Constructor

    private readonly IAudioConversionService _audioConversionService;
    private readonly TranscriptionHelper _transcriptionHelper;
    private readonly Globals _globals;

    public TranscriptionService(Globals globals,
        IAudioConversionService audioConversionService,
        TranscriptionHelper transcriptionHelper)
    {
        _globals = globals;
        _audioConversionService = audioConversionService;
        _transcriptionHelper = transcriptionHelper;
    }

    #endregion

    #region Methods

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
                Log.Warning("Invalid language: {Lang}", lang);
                throw new InvalidLanguageException("Invalid language");
            }

            lang = new CultureInfo(lang).TwoLetterISOLanguageName;
        }

        if (!Enum.TryParse(request.Model, true, out WhisperModel modelEnum))
        {
            Log.Warning("Invalid model: {Model}", request.Model);
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

        if (result.transcription is not null)
            return new PostResponse
            {
                Success = true,
                Result = request.TimeStamps
                    ? JsonSerializer.Deserialize<List<TimeStamp>>(result.transcription)
                    : result.transcription
            };

        return FailResponse(result.errorCode, result.errorMessage);
    }

    public async Task<(string? transcription, string? errorCode, string? errorMessage)> ProcessAudioTranscription(
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
                return (serialized, null, null);
            }

            var transcribedText = await File.ReadAllTextAsync(transcribedFilePath, token);
            return (transcribedText.Trim(), null, null);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Transcription cancelled");
        }
        finally
        {
            DeleteFilesAsync(fileName, wavFile, transcribedFilePath);
        }

        return default;
    }
    private static void DeleteFilesAsync(params string[] files)
    {
        foreach (var file in files)
            if (File.Exists(file))
                File.Delete(file);
    }

    public PostResponse FailResponse(string? errorCode, string? errorMessage)
    {
        return new PostResponse
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    #endregion
}