using AsyncKeyedLock;
using Microsoft.AspNetCore.Mvc;
using WhisperAPI.Models;
using WhisperAPI.Services;
using static WhisperAPI.Globals;

namespace WhisperAPI.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class Transcribe : ControllerBase
{
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;

    private readonly ITranscriptionService _transcriptionService;

    public Transcribe(AsyncKeyedLocker<string> asyncKeyedLocker, ITranscriptionService transcriptionService)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _transcriptionService = transcriptionService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PostRequest request)
    {
        if (string.IsNullOrEmpty(request.File))
            return BadRequest(_transcriptionService.FailResponse(ErrorCodesAndMessages.NoFile,
                ErrorCodesAndMessages.NoFileMessage));

        using var loc = await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false);
        var response = await _transcriptionService.HandleTranscriptionRequest(request);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}