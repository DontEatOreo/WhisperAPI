using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncKeyedLock;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WhisperAPI.Models;
using static WhisperAPI.Globals;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe : ControllerBase
{
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;

    public Transcribe(AsyncKeyedLocker<string> asyncKeyedLocker)
        => _asyncKeyedLocker = asyncKeyedLocker;

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
                    ? JsonSerializer.Deserialize<List<TimeStamp>>(result.transcription, Options)
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