using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Requests;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe : ControllerBase
{
    private readonly IContentTypeProvider _provider;
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly IMediator _mediator;

    public Transcribe(IContentTypeProvider provider,
        TokenBucketRateLimiter rateLimiter, IMediator mediator)
    {
        _provider = provider;
        _rateLimiter = rateLimiter;
        _mediator = mediator;
    }

    [EnableRateLimiting("token")]
    [HttpGet]
    public async Task<IActionResult> Post([FromForm] Request request, [FromForm] IFormFile file, CancellationToken token)
    {
        // Return if no file is provided
        if (file is null || file.Length is 0)
            throw new NoFileException("No file provided");

        // Get file extension
        var fileExtension = _provider.TryGetContentType(file.FileName, out var contentType)
            ? contentType
            : file.ContentType;

        // Return if file is not audio or video
        var hasAudio = fileExtension.StartsWith("audio/");
        var hasVideo = fileExtension.StartsWith("video/");
        const string error = "File is not audio or video";
        if (!hasAudio && !hasVideo)
            throw new InvalidFileTypeException(error);
        
        TranscribeRequest audioRequest = new(file, request);
        var result = await _mediator.Send(audioRequest, token);
        _ = _rateLimiter.TryReplenish(); // Replenish the token bucket
        
        return Ok(result);
    }
}