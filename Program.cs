using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Serilog;
using WhisperAPI;
using WhisperAPI.Exceptions;
using WhisperAPI.Services.Audio;
using WhisperAPI.Services.Transcription;

const string html = @"
<!DOCTYPE html>
<html lang=""en"">
<a href=""https://github.com/DontEatOreo/WhisperAPI"" target=""_blank"">Docs</a>
<style>
a {
    font-size: 100px;
}
</style>
</html>
";

var builder = WebApplication.CreateBuilder();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpClient<ModelsClient>();
builder.Services.AddHttpClient<WhisperClient>();

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = long.MaxValue;
});

const string tokenPolicy = "token";
RateLimitOptions tokenBucketOptions = new();
builder.Configuration.GetSection(RateLimitOptions.RateLimit).Bind(tokenBucketOptions);
builder.Services.AddRateLimiter(_ => _
    .AddTokenBucketLimiter(policyName: tokenPolicy, options =>
    {
        options.TokenLimit = tokenBucketOptions.TokenLimit;
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = tokenBucketOptions.QueueLimit;
        options.ReplenishmentPeriod = tokenBucketOptions.ReplenishmentPeriod;
        options.TokensPerPeriod = tokenBucketOptions.TokensPerPeriod;
        options.AutoReplenishment = tokenBucketOptions.AutoReplenishment;
    })
);
builder.Services.AddSingleton<TokenBucketRateLimiter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TokenBucketRateLimiterOptions>>();
    return new TokenBucketRateLimiter(options.Value);
});
builder.Services.Configure<TokenBucketRateLimiterOptions>(options =>
{
    options.TokenLimit = tokenBucketOptions.TokenLimit;
    options.QueueLimit = tokenBucketOptions.QueueLimit;
    options.TokensPerPeriod = tokenBucketOptions.TokensPerPeriod;
    options.ReplenishmentPeriod = tokenBucketOptions.ReplenishmentPeriod;
    options.AutoReplenishment = tokenBucketOptions.AutoReplenishment;
});

builder.Services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();
builder.Services.AddSingleton<TranscriptionHelper>();
builder.Services.AddSingleton<Globals>();

builder.Services.AddTransient<GlobalChecks>();
builder.Services.AddTransient<GlobalDownloads>();
builder.Services.AddTransient<ITranscriptionService, TranscriptionService>();
builder.Services.AddTransient<IAudioConversionService, AudioConversionService>();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = (int)HttpStatusCode.PermanentRedirect;
    options.HttpsPort = 443;
});

var app = builder.Build();

var checks = app.Services.GetRequiredService<GlobalChecks>();
await checks.FFmpeg();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<Middleware>();
app.UseRateLimiter();
app.MapGet("/", () => Results.Extensions.Html(html));
app.Run();

internal static class ResultsExtensions
{
    public static IResult Html(this IResultExtensions resultExtensions, string html)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);
        return new HtmlResult(html);
    }
}

internal class HtmlResult : IResult
{
    private readonly string _html;

    public HtmlResult(string html)
    {
        _html = html;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = MediaTypeNames.Text.Html;
        httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
        return httpContext.Response.WriteAsync(_html);
    }
}