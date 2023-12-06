using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using WhisperAPI.Exceptions;
using WhisperAPI.Queries;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe(
    ReplenishingRateLimiter rateLimiter,
    ISender mediator,
    FileExtensionContentTypeProvider typeProvider)
    : ControllerBase
{
    /// <summary>
    /// Retrieves a transcript of the audio or video file provided in the request.
    /// </summary>
    /// <param name="request">The transcript query parameters.</param>
    /// <param name="file">The audio or video file to transcribe.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The transcript of the audio or video file.</returns>
    [EnableRateLimiting("token")]
    [HttpPost]
    public async Task<IActionResult> Post([FromForm] TranscriptQuery request, [FromForm] IFormFile file, CancellationToken token)
    {
        // Return if no file is provided
        if (file is null || file.Length is 0)
            throw new NoFileException("No file provided");

        var isAudio = typeProvider.TryGetContentType(file.FileName, out var contentType) && contentType.StartsWith("audio/");
        var isVideo = typeProvider.TryGetContentType(file.FileName, out contentType) && contentType.StartsWith("video/");
        if (!isAudio && !isVideo)
            throw new InvalidFileTypeException("Invalid file type");

        WavConvertQuery wavRequest = new(file);
        var (wavFile, policy) = await mediator.Send(wavRequest, token);

        FormDataQuery formDataQuery = new(wavFile, request);
        var whisperOptions = await mediator.Send(formDataQuery, token);

        var result = await mediator.Send(whisperOptions, token);
        _ = rateLimiter.TryReplenish(); // Replenish the token bucket

        HttpContext.Response.OnCompleted(policy);
        return Ok(result);
    }
}