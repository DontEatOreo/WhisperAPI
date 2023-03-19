using System.Globalization;
using System.Text.Json;
using Serilog;
using WhisperAPI.Models;
using static WhisperAPI.Globals;

namespace WhisperAPI.Services;

public class TranscriptionService : ITranscriptionService
{
    private readonly IAudioConversionService _audioConversionService;
    private readonly FileService _fileService;
    private readonly TranscriptionHelper _transcriptionHelper;

    public TranscriptionService(
        IAudioConversionService audioConversionService,
        FileService fileService,
        TranscriptionHelper transcriptionHelper)
    {
        _audioConversionService = audioConversionService;
        _fileService = fileService;
        _transcriptionHelper = transcriptionHelper;
    }

    public async Task<PostResponse> HandleTranscriptionRequest(PostRequest request)
    {
        var lang = request.Lang?.Trim().ToLower();
        lang ??= "auto";
        if (lang != "auto")
        {
            if (lang.Length is 2)
                lang = CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .FirstOrDefault(c => c.TwoLetterISOLanguageName == lang)?.EnglishName;

            if (CultureInfo.GetCultures(CultureTypes.AllCultures).All(c => !lang!.Contains(c.EnglishName)))
            {
                Log.Warning("Invalid language: {Lang}", lang);
                return FailResponse(ErrorCodesAndMessages.InvalidLanguage,
                    ErrorCodesAndMessages.InvalidLanguageMessage);
            }

            lang = new CultureInfo(lang!).TwoLetterISOLanguageName;
        }

        request.TimeStamps ??= false;
        request.Model ??= "base";
        request.Translate ??= false;

        if (!Enum.TryParse(request.Model, true, out WhisperModel modelEnum))
        {
            Log.Warning("Invalid model: {Model}", request.Model);
            return FailResponse(ErrorCodesAndMessages.InvalidModel, ErrorCodesAndMessages.InvalidModelMessage);
        }

        var result = await ProcessAudioTranscription(request.File!, lang, (bool)request.Translate, modelEnum, (bool)request.TimeStamps);
        if (result.transcription is not null)
            return new PostResponse
            {
                Success = true,
                Result = (bool)request.TimeStamps
                    ? JsonSerializer.Deserialize<List<TimeStamp>>(result.transcription, Options)
                    : result.transcription
            };


        Log.Warning("Transcription failed: {ErrorCode} - {ErrorMessage}", result.errorCode, result.errorMessage);
        return FailResponse(result.errorCode, result.errorMessage);
    }

    public async Task<(string? transcription, string? errorCode, string? errorMessage)> ProcessAudioTranscription(
        string fileBase64,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp)
    {
        var fileId = Guid.NewGuid().ToString();
        var selectedModelPath = ModelFilePaths[whisperModel];
        var selectedOutputFormat = OutputFormatMapping[timeStamp];

        await _transcriptionHelper.DownloadModelIfNotExists(whisperModel, selectedModelPath);

        var (fileBytes, fileExtension) = _fileService.GetFileData(fileBase64);
        if (fileBytes is null || fileExtension is null)
            return (null, ErrorCodesAndMessages.InvalidFileType, ErrorCodesAndMessages.InvalidFileTypeMessage);

        var fileSize = fileBytes.Length;
        const int sizeLimit = 52428800; // 52428800 is 50 mib
        if (fileSize > sizeLimit)
            return (null, ErrorCodesAndMessages.FileSizeExceeded, ErrorCodesAndMessages.FileSizeExceededMessage);

        var fileName = Path.Combine(WhisperFolder, $"{fileId}.{fileExtension}");
        var audioFile = Path.Combine(WhisperFolder, $"{fileId}.wav"); // the output file (It needs to be wav)
        await using FileStream fileStream = new(fileName, FileMode.Create, FileAccess.Write);
        await fileStream.WriteAsync(fileBytes);


        await _audioConversionService.ConvertToWavAsync(fileName, audioFile);

        // The CLI Arguments in Whisper.cpp for the output file format are `-o<extension>` so we just parse the extension after the `-o`
        var transcribedFilePath = Path.Combine(WhisperFolder, $"{audioFile}.{selectedOutputFormat[2..]}");
        await _transcriptionHelper.Transcribe(audioFile, lang, translate, selectedModelPath, selectedOutputFormat);

        if (timeStamp)
        {
            var jsonLines = _transcriptionHelper.ConvertToJson(transcribedFilePath);
            _fileService.CleanUp(fileName, audioFile, transcribedFilePath);
            var serialized = JsonSerializer.Serialize(jsonLines).Trim();
            return (serialized, null, null);
        }

        var transcribedText = await File.ReadAllTextAsync(transcribedFilePath);
        _fileService.CleanUp(fileName, audioFile, transcribedFilePath);
        return (transcribedText.Trim(), null, null);
    }

    public PostResponse FailResponse(string? errorCode, string? errorMessage)
    {
        var result = JsonSerializer.Serialize(new PostResponse
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        });
        return JsonSerializer.Deserialize<PostResponse>(result, Options)!;
    }
}