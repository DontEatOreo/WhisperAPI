using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncKeyedLock;
using Microsoft.AspNetCore.Mvc;

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
            return BadRequest(FailResponse(Globals.ErrorCodesAndMessages.NoFile, Globals.ErrorCodesAndMessages.NoFileMessage));

        using var loc = await _asyncKeyedLocker.LockAsync(Globals.Key).ConfigureAwait(false);
        var response = await TranscribeAudioAsync(request);
        return Ok(response);
    }

    private static async Task<PostResponse> TranscribeAudioAsync(PostRequest request)
    {
        request.Lang ??= "auto";
        if (request.Lang.Length > 2)
        {
            try
            {
                CultureInfo culture = new (request.Lang);
                request.Lang = culture.TwoLetterISOLanguageName;
            }
            catch (Exception)
            {
                return FailResponse(Globals.ErrorCodesAndMessages.InvalidLang, Globals.ErrorCodesAndMessages.InvalidLangMessage);
            }
        }

        request.TimeStamps ??= false;
        request.Model ??= "base";
        request.Translate ??= false;

        if (!Enum.TryParse(request.Model, true, out Globals.WhisperModel modelEnum))
            return FailResponse(Globals.ErrorCodesAndMessages.InvalidModel, Globals.ErrorCodesAndMessages.InvalidModelMessage);

        var result = await Transcription.TranscribeAudio(request.File!,
            request.Lang,
            (bool)request.Translate,
            modelEnum,
            (bool)request.TimeStamps);
        if (result.transcription is null)
            return FailResponse(result.errorCode, result.errorMessage);

        return new PostResponse
        {
            Success = true,
            Result = (bool)request.TimeStamps
                ? JsonSerializer.Deserialize<List<Globals.WhisperTimeStampJson>>(result.transcription, Options)
                : result.transcription
        };
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