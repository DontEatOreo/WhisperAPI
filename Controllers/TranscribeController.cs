using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Services.Transcription;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe : ControllerBase
{
    private readonly IContentTypeProvider _provider;
    private readonly ITranscriptionService _transcriptionService;
    private readonly TokenBucketRateLimiter _rateLimiter;

    public Transcribe(IContentTypeProvider provider,
        ITranscriptionService transcriptionService,
        TokenBucketRateLimiter rateLimiter)
    {
        _provider = provider;
        _transcriptionService = transcriptionService;
        _rateLimiter = rateLimiter;
    }

    [EnableRateLimiting("token")]
    [HttpPost]
    public async Task<IActionResult> Post([FromForm] PostRequest request, [FromForm] IFormFile file)
    {
        // Return if no file is provided
        if (file is null || file.Length is 0)
            throw new NoFileException("No file provided");

        // Create a linked CancellationTokenSource
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);

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

        var transcriptionRequest = await _transcriptionService.Handler(file, request, cts.Token);
        _ = _rateLimiter.TryReplenish(); // Replenish the token bucket
        return Ok(transcriptionRequest);
    }
}