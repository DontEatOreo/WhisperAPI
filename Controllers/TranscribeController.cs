using AsyncKeyedLock;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using WhisperAPI.Models;
using WhisperAPI.Services;
using static WhisperAPI.Globals;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe : ControllerBase
{
    #region Constructor

    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly ITranscriptionService _transcriptionService;
    private readonly FileExtensionContentTypeProvider _provider;

    public Transcribe(AsyncKeyedLocker<string> asyncKeyedLocker,
        ITranscriptionService transcriptionService,
        FileExtensionContentTypeProvider provider)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _transcriptionService = transcriptionService;
        _provider = provider;
    }

    #endregion

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] PostRequest request, [FromForm] IFormFile? file)
    {
        // Return if no file is provided
        if (file is null || file.Length is 0)
            return BadRequest(_transcriptionService.FailResponse(ErrorCodesAndMessages.NoFile,
                ErrorCodesAndMessages.NoFileMessage));

        // Get file extension
        var fileExtension = _provider.TryGetContentType(file.FileName, out var contentType)
            ? contentType
            : file.ContentType;

        // Return if file is not audio or video
        if (!fileExtension.StartsWith("audio/") && !fileExtension.StartsWith("video/"))
            return BadRequest(_transcriptionService.FailResponse(ErrorCodesAndMessages.InvalidFileType,
                ErrorCodesAndMessages.InvalidFileTypeMessage));

        // Queue the transcription request
        using var loc = await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false);
        var response = await _transcriptionService.HandleTranscriptionRequest(file, request);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}