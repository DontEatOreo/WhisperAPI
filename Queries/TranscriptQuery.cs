using MediatR;
using Microsoft.AspNetCore.Mvc;
using WhisperAPI.Models;

namespace WhisperAPI.Queries;

/// <summary>
/// Represents a query for retrieving a transcript with optional translation and language settings.
/// </summary>
/// <param name="Translate">Whether or not to translate the transcript.</param>
/// <param name="Model">The model to use for the transcript.</param>
/// <returns>A <see cref="WhisperAPI.Models.JsonResponse"/> containing the requested transcript.</returns>
public record TranscriptQuery(
    [FromForm(Name = "translate")] bool Translate,
    [FromForm(Name = "model")] string Model
) : IRequest<JsonResponse>;