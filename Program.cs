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

// Define an HTML string to be used as the default response for the root endpoint
const string html = """
<!DOCTYPE html>
<html lang="en">
<a href="https://github.com/DontEatOreo/WhisperAPI" target="_blank">Docs</a>
<style>
a {
    font-size: 100px;
}
</style>
</html>
""";

// Create a new web application builder
var builder = WebApplication.CreateBuilder();

// Add services to the builder
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMvcCore(o => o.RespectBrowserAcceptHeader = true)
    .AddXmlSerializerFormatters();
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = long.MaxValue;
});

// Configure rate limiting options
const string tokenPolicy = "token";
RateLimitOptions tokenBucketOptions = new();
builder.Configuration.GetSection(RateLimitOptions.RateLimit).Bind(tokenBucketOptions);
builder.Services.AddRateLimiter(l => l
    .AddTokenBucketLimiter(policyName: tokenPolicy, options =>
    {
        options.TokenLimit = RateLimitOptions.TokenLimit;
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = RateLimitOptions.QueueLimit;
        options.ReplenishmentPeriod = tokenBucketOptions.ReplenishmentPeriod;
        options.TokensPerPeriod = RateLimitOptions.TokensPerPeriod;
        options.AutoReplenishment = RateLimitOptions.AutoReplenishment;
    })
);
builder.Services.AddSingleton<TokenBucketRateLimiter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TokenBucketRateLimiterOptions>>();
    return new TokenBucketRateLimiter(options.Value);
});
builder.Services.Configure<TokenBucketRateLimiterOptions>(options =>
{
    options.TokenLimit = RateLimitOptions.TokenLimit;
    options.QueueLimit = RateLimitOptions.QueueLimit;
    options.TokensPerPeriod = RateLimitOptions.TokensPerPeriod;
    options.ReplenishmentPeriod = tokenBucketOptions.ReplenishmentPeriod;
    options.AutoReplenishment = RateLimitOptions.AutoReplenishment;
});
builder.Services.AddSingleton<ReplenishingRateLimiter>(sp => sp.GetRequiredService<TokenBucketRateLimiter>());

// Add services to the builder
builder.Services.AddSingleton<Globals>();
builder.Services.AddTransient<FileExtensionContentTypeProvider>();
builder.Services.Configure<WhisperSettings>(builder.Configuration.GetSection("WhisperSettings"));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

// Configure logging
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Configure HTTPS redirection
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = (int)HttpStatusCode.PermanentRedirect;
    options.HttpsPort = 443;
});

// Build the application
var app = builder.Build();

// Configure middleware and endpoints
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<Middleware>();
app.UseRateLimiter();
app.MapGet("/", () => Results.Extensions.Html(html));
app.Run();

// Define extension methods for IResult
internal static class ResultsExtensions
{
    public static IResult Html(this IResultExtensions resultExtensions, string html)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);
        return new HtmlResult(html);
    }
}

// Define a custom IResult implementation for returning HTML responses
internal class HtmlResult(string html) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = MediaTypeNames.Text.Html;
        httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
        return httpContext.Response.WriteAsync(html);
    }
}