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
    #region Ctor

    private readonly FileExtensionContentTypeProvider _provider;
    private readonly ITranscriptionService _transcriptionService;
    private readonly TokenBucketRateLimiter _rateLimiter;

    #endregion Ctor

    public Transcribe(FileExtensionContentTypeProvider provider, ITranscriptionService transcriptionService, TokenBucketRateLimiter rateLimiter)
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
        if (!fileExtension.StartsWith("audio/") && !fileExtension.StartsWith("video/"))
            throw new InvalidFileTypeException("File is not audio or video");

        var response = await _transcriptionService.HandleTranscriptionRequest(file, request, cts.Token);
        _= _rateLimiter.TryReplenish();
        return Ok(response);
    }
}