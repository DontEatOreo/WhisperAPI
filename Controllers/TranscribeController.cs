using System.Text;
using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
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
    [Produces("text/plain", "application/xml", "application/json")]
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

        var headers = HttpContext.Request.Headers;
        var language = headers.AcceptLanguage.FirstOrDefault();

        FormDataQuery formDataQuery = new(wavFile, language, request);
        var whisperOptions = await mediator.Send(formDataQuery, token);

        var result = await mediator.Send(whisperOptions, token);
        _ = rateLimiter.TryReplenish(); // Replenish the token bucket

        HttpContext.Response.OnCompleted(policy);

        /*
         * By default `text/plain` shouldn't change the response body,
         * but we're overriding this behavior to return
         * the literal text of the transcript.
         */
        if (headers.Accept.Contains("text/plain"))
        {
            StringBuilder sb = new();
            result.ForEach(data => sb.Append(data.Text.Trim()));
            return Ok(sb.ToString());
        }

        // If the user has made a request with `application/xml` it will convert the response to XML automatically
        JsonResponse jsonResponse = new()
        {
            Data = result.Select(data => new ResponseData(
                data.Start.TotalSeconds,
                data.End.TotalSeconds,
                data.Text.Trim())).ToList(),
            Count = result.Count
        };

        return Ok(jsonResponse);
    }
}