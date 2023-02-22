using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncKeyedLock;
using Microsoft.AspNetCore.Mvc;
using Pastel;
using Serilog;
using static WhisperAPI.Globals;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("transcribe")]
public sealed class Transcribe : ControllerBase
{
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;

    public Transcribe(AsyncKeyedLocker<string> asyncKeyedLocker)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
    }

    private static readonly JsonSerializerOptions? Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PostRequest request)
    {
        if (string.IsNullOrEmpty(request.File))
            return BadRequest(FailResponse(ErrorCodesAndMessages.NoFile, ErrorCodesAndMessages.NoFileMessage));

        using var loc = await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false);
        var response = await TranscribeAudioAsync(request);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    private static async Task<PostResponse> TranscribeAudioAsync(PostRequest request)
    {
        var lang = request.Lang?.Trim().ToLower();
        lang ??= "auto";
        if (lang != "auto")
        {
            if (lang.Length == 2)
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

        var result = await Transcription.TranscribeAudio(request.File!,
            lang,
            (bool)request.Translate,
            modelEnum,
            (bool)request.TimeStamps);
        if (result.transcription is not null)
            return new PostResponse
            {
                Success = true,
                Result = (bool)request.TimeStamps
                    ? JsonSerializer.Deserialize<List<WhisperTimeStampJson>>(result.transcription, Options)
                    : result.transcription
            };

        Log.Warning("Transcription failed: {ErrorCode} - {ErrorMessage}", result.errorCode, result.errorMessage);
        return FailResponse(result.errorCode, result.errorMessage);
    }

    private static PostResponse FailResponse(string? errorCode, string? errorMessage)
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

public class PostRequest
{
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("time_stamps")]
    public bool? TimeStamps { get; set; }

    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    [JsonPropertyName("translate")]
    public bool? Translate { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

public class PostResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("result")]
    public object? Result { get; set; }
}