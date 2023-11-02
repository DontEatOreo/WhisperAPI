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
public sealed class Transcribe : ControllerBase
{
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly IMediator _mediator;
    private readonly FileExtensionContentTypeProvider _typeProvider;

    public Transcribe(TokenBucketRateLimiter rateLimiter, IMediator mediator, FileExtensionContentTypeProvider typeProvider)
    {
        _rateLimiter = rateLimiter;
        _mediator = mediator;
        _typeProvider = typeProvider;
    }

    /// <summary>
    /// Retrieves a transcript of the audio or video file provided in the request.
    /// </summary>
    /// <param name="request">The transcript query parameters.</param>
    /// <param name="file">The audio or video file to transcribe.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The transcript of the audio or video file.</returns>
    [EnableRateLimiting("token")]
    [HttpGet]
    public async Task<IActionResult> Get([FromForm] TranscriptQuery request, [FromForm] IFormFile file, CancellationToken token)
    {
        // Return if no file is provided
        if (file is null || file.Length is 0)
            throw new NoFileException("No file provided");

        var isAudio = _typeProvider.TryGetContentType(file.FileName, out var contentType) && contentType.StartsWith("audio/");
        var isVideo = _typeProvider.TryGetContentType(file.FileName, out contentType) && contentType.StartsWith("video/");
        if (!isAudio && !isVideo)
            throw new InvalidFileTypeException("Invalid file type");

        WavConvertQuery wavRequest = new(file);
        var (wavFile, policy) = await _mediator.Send(wavRequest, token);

        FormDataQuery formDataQuery = new(wavFile, request);
        var whisperOptions = await _mediator.Send(formDataQuery, token);

        var result = await _mediator.Send(whisperOptions, token);
        _ = _rateLimiter.TryReplenish(); // Replenish the token bucket

        HttpContext.Response.OnCompleted(policy);
        return Ok(result);
    }
}