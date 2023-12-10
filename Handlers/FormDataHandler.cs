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

    public Task<WhisperOptions> Handle(FormDataQuery request, CancellationToken token)
    {
        var lang = ValidateLanguage(request.Lang);
        var modelEnum = ValidateModel(request.Query.Model.ToLower());

        WhisperOptions whisperOptions = new(request.File, lang, request.Query.Translate, modelEnum);
        return Task.FromResult(whisperOptions);
    }

    /// <summary>
    /// Validates the given language string and returns its two-letter ISO language name if it is a valid language.
    /// </summary>
    /// <param name="lang">The language string to validate.</param>
    /// <returns>The two-letter ISO language name of the validated language.</returns>
    /// <exception cref="InvalidLanguageException">Thrown when the given language string is not a valid language.</exception>
    private static string ValidateLanguage(string? lang)
    {
        if (string.IsNullOrEmpty(lang))
        {
            lang = "auto";
            return lang;
        }

        lang = lang.Trim().ToLower();
        var isAuto = lang is "auto";
        if (isAuto)
            return lang;

        /*
         * - `EnglishName`: The culture's name in English.
         * - `DisplayName`: The culture's name in the current UI culture.
         * - `NativeName`: The culture's name in its own language.
         * - `TwoLetterISOLanguageName`: The culture's two-letter ISO 639-1 language name.
         * - `ThreeLetterISOLanguageName`: The culture's three-letter ISO 639-2 language name.
         */

        var iso = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .FirstOrDefault(culture => culture.EnglishName.ToLower() == lang ||
                                       culture.DisplayName.ToLower() == lang ||
                                       culture.NativeName.ToLower() == lang ||
                                       culture.TwoLetterISOLanguageName.ToLower() == lang ||
                                       culture.ThreeLetterISOLanguageName.ToLower() == lang)
            ?.TwoLetterISOLanguageName;

        if (iso is not null)
            return iso;

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
        if (model.Contains("large"))
            model = model.Replace("large", "largev3");
        var parse = Enum.TryParse(model, true, out GgmlType type);

        if (type.ToString().ToLower().Contains("v1"))
            return GgmlType.LargeV3; // v1 model exists but we don't want to use it
        if (parse)
            return type;

        throw new InvalidModelException(InvalidModelError);
    }
}