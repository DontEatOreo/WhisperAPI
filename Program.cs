using System.Net;
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

var builder = WebApplication.CreateBuilder();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

#region HttpClients

builder.Services.AddHttpClient<ModelsClient>();
builder.Services.AddHttpClient<WhisperClient>();

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = long.MaxValue;
});

#endregion HttpClients

#region RateLimiting

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

#endregion RateLimiting

builder.Services.AddSingleton<FileExtensionContentTypeProvider>();
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
await checks.Whisper();
await checks.Make();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<Middleware>();
app.UseRateLimiter();
static string GetTicks() => (DateTime.Now.Ticks & 0x11111).ToString("00000");
app.MapGet("/", () => Results.Ok($"Token Limiter {GetTicks()}"))
    .RequireRateLimiting(tokenPolicy);
app.Run();