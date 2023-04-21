using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhisperAPI.Exceptions;

public class Middleware
{
    private readonly RequestDelegate _next;

    public Middleware(RequestDelegate next)
    {
        _next = next;
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

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        context.Response.StatusCode = exception switch
        {
            InvalidFileTypeException => (int)HttpStatusCode.UnsupportedMediaType,
            InvalidLanguageException => (int)HttpStatusCode.BadRequest,
            InvalidModelException => (int)HttpStatusCode.UnprocessableEntity,
            NoFileException => (int)HttpStatusCode.NotFound,
            _ => throw new ArgumentOutOfRangeException(nameof(exception), exception, null)
        };

        await context.Response.WriteAsync(new ErrorDetails
        {
            Success = false,
            StatusCode = context.Response.StatusCode
        }.ToString());
    }
}

internal class ErrorDetails
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("status")]
    public int StatusCode { get; set; }

    public override string ToString() => JsonSerializer.Serialize(this);
}