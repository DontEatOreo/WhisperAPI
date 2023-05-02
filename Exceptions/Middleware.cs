using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace WhisperAPI.Exceptions;

public class Middleware
{
    private readonly RequestDelegate _next;
    private readonly TokenBucketRateLimiter _rateLimiter;

    public Middleware(RequestDelegate next, TokenBucketRateLimiter rateLimiter)
    {
        _next = next;
        _rateLimiter = rateLimiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next.Invoke(context);
        }
        catch (Exception e)
        {
            await HandleExceptionAsync(context, e);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        context.Response.StatusCode = exception switch
        {
            InvalidFileTypeException => (int)HttpStatusCode.UnsupportedMediaType,
            InvalidLanguageException => (int)HttpStatusCode.BadRequest,
            InvalidModelException => (int)HttpStatusCode.UnprocessableEntity,
            NoFileException => (int)HttpStatusCode.NotFound,
            FileProcessingException => (int)HttpStatusCode.UnprocessableEntity,
            _ => throw new ArgumentOutOfRangeException(nameof(exception), exception, null)
        };

        _ = _rateLimiter.TryReplenish();

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = exception.Message
        }));
    }
}