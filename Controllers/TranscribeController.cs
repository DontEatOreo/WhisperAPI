using System.Net.Mime;
using System.Text;
using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Whisper.net;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using WhisperAPI.Queries;
// ReSharper disable SuggestBaseTypeForParameterInConstructor

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
    [Produces(MediaTypeNames.Text.Plain, MediaTypeNames.Application.Xml, MediaTypeNames.Application.Json, "application/x-subrip")]
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

        var acceptHeader = headers.Accept.FirstOrDefault();

        switch (acceptHeader)
        {
            /*
            * By default, `text/plain` shouldn't change the response body,
            * but we're overriding this behavior to return
            * the literal text of the transcript.
            */
            case MediaTypeNames.Text.Plain:
                return Ok(string.Join(" ", result.Select(data => data.Text.Trim())));
            
            case MediaTypeNames.Application.Json:
                JsonResponse jsonResponse = new()
                {
                    Data = result.Select(data => new ResponseData(
                        data.Start.TotalSeconds,
                        data.End.TotalSeconds,
                        data.Text.Trim())).ToList(),
                    Count = result.Count
                };
                return Ok(jsonResponse);

            case "application/x-subrip":
                var srtContent = GenerateSrtSubs(result);
                return File(Encoding.UTF8.GetBytes(srtContent), "application/x-subrip");

            case "application/xml":
                // If the user has made a request with `application/xml` it will convert the response to XML automatically
                return Ok(result);

            default:
                return BadRequest("Unsupported media type");
        }
    }

    private static string GenerateSrtSubs(IEnumerable<SegmentData> transcriptData)
    {
        StringBuilder sb = new();
        var transcriptList = transcriptData.ToList();

        for (var i = 0; i < transcriptList.Count; i++)
        {
            var data = transcriptList[i];
            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatTime(data.Start)} --> {FormatTime(data.End)}");
            sb.AppendLine(data.Text.Trim());
            sb.AppendLine();
        }


        return sb.ToString().TrimEnd();
    }

    private static string FormatTime(TimeSpan time) =>
        $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
}
