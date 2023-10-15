using System.Globalization;
using JetBrains.Annotations;
using MediatR;
using Whisper.net.Ggml;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Queries;

namespace WhisperAPI.Handlers;

[UsedImplicitly]
public sealed class FormDataHandler : IRequestHandler<FormDataQuery, WhisperOptions>
{
    private const string InvalidLanguageError = "Invalid language";
    private const string InvalidModelError = "Invalid model";

    /// <summary>
    /// Handles the FormDataQuery request by validating the language and model, and returning a WhisperOptions object.
    /// </summary>
    /// <param name="request">The FormDataQuery request.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the WhisperOptions object.</returns>
    public Task<WhisperOptions> Handle(FormDataQuery request, CancellationToken token)
    {
        string? lang = null;
        if (request.Query.Lang is not null)
            lang = ValidateLanguage(request.Query.Lang);
        var modelEnum = ValidateModel(request.Query.Model.ToLower());
        
        WhisperOptions whisperOptions = new(request.File, lang, request.Query.Translate, modelEnum);
        return Task.FromResult(whisperOptions);
    }
    
    /// <summary>
    /// Validates the provided language string by checking if it is a valid CultureInfo name or "auto".
    /// </summary>
    /// <param name="lang">The language string to validate.</param>
    /// <returns>The validated language string.</returns>
    /// <exception cref="InvalidLanguageException">Thrown when the provided language string is not a valid CultureInfo name or "auto".</exception>
    private static string ValidateLanguage(string lang)
    {
        lang = lang.Trim().ToLower();
        var isAuto = lang is "auto";
        if (isAuto)
            return lang;
        
        var hasLang = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .Any(culture => culture.EnglishName == lang
                            || culture.NativeName == lang 
                            || culture.TwoLetterISOLanguageName == lang
                            || culture.ThreeLetterWindowsLanguageName == lang);
        if (hasLang)
            return lang;
        
        throw new InvalidLanguageException(InvalidLanguageError);
    }

    /// <summary>
    /// Validates the provided model string by checking if it is a valid GgmlType enum value.
    /// </summary>
    /// <param name="model">The model string to validate.</param>
    /// <returns>The validated GgmlType enum value.</returns>
    /// <exception cref="InvalidModelException">Thrown when the provided model string is not a valid GgmlType enum value.</exception>
    private static GgmlType ValidateModel(string model)
    {
        var parse = Enum.TryParse(model, true, out GgmlType type);
        if (type.ToString().ToLower().Contains("v1"))
            return GgmlType.Base; // v1 model exists but we don't want to use it
        if (parse)
            return type;

        throw new InvalidModelException(InvalidModelError);
    }
}