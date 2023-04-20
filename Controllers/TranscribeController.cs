using AsyncKeyedLock;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Services.Transcription;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe : ControllerBase
{
    #region Constructor

    private readonly Globals _globals;
    private readonly FileExtensionContentTypeProvider _provider;
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly ITranscriptionService _transcriptionService;

    #endregion

    public Transcribe(Globals globals,
        FileExtensionContentTypeProvider provider,
        AsyncKeyedLocker<string> asyncKeyedLocker,
        ITranscriptionService transcriptionService)
    {
        _globals = globals;
        _provider = provider;
        _asyncKeyedLocker = asyncKeyedLocker;
        _transcriptionService = transcriptionService;
    }

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

        using var loc = await _asyncKeyedLocker.LockAsync(_globals.Key, cts.Token).ConfigureAwait(false);
        try
        {
            var response = await _transcriptionService.HandleTranscriptionRequest(file, request, cts.Token);
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499); // 499 Client Closed Request
        }
    }
}