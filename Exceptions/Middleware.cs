using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace WhisperAPI.Exceptions;

public class Middleware(RequestDelegate next, ReplenishingRateLimiter rateLimiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next.Invoke(context);
        }
        catch (Exception e)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            context.Response.StatusCode = e switch
            {
                InvalidFileTypeException => (int)HttpStatusCode.UnsupportedMediaType,
                InvalidLanguageException => (int)HttpStatusCode.BadRequest,
                InvalidModelException => (int)HttpStatusCode.UnprocessableEntity,
                NoFileException => (int)HttpStatusCode.NotFound,
                FileProcessingException => (int)HttpStatusCode.UnprocessableEntity,
                _ => (int)HttpStatusCode.InternalServerError
            };

            _ = rateLimiter.TryReplenish();

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = e.Message
            }));
        }
    }
}