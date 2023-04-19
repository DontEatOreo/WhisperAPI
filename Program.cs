using System.Net;
using AsyncKeyedLock;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using WhisperAPI;
using WhisperAPI.Exceptions;
using WhisperAPI.Services.Audio;
using WhisperAPI.Services.Transcription;

var builder = WebApplication.CreateBuilder();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 209715200;
});

#region Singletons

builder.Services.AddSingleton<AsyncKeyedLocker<string>>(_ =>
{
    return new AsyncKeyedLocker<string>(o =>
    {
        o.PoolSize = Globals.ThreadCount * 4;
        o.PoolInitialFill = o.PoolSize / 2;
        // We determine the number of threads available to the CPU,
        // then divide it by two to calculate the maximum number of Whisper instances that can run simultaneously.
        // This ensures each instance has a minimum of two threads to work with, unless the CPU only possesses a single thread.
        o.MaxCount = Globals.ThreadCount > 1 ? Globals.ThreadCount / 2 : 1;
    });
});
builder.Services.AddSingleton<FileExtensionContentTypeProvider>();
builder.Services.AddSingleton<TranscriptionHelper>();
builder.Services.AddSingleton<Globals>();
builder.Services.AddSingleton<GlobalChecks>();
builder.Services.AddSingleton<GlobalDownloads>();

builder.Services.AddSingleton<ITranscriptionService, TranscriptionService>();
builder.Services.AddSingleton<IAudioConversionService, AudioConversionService>();
builder.Services.AddSingleton<IGlobalDownloads, GlobalDownloads>();
builder.Services.AddSingleton<IGlobalChecks, GlobalChecks>();

#endregion

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

#region GlobalChecks

var globalService = app.Services.GetRequiredService<Globals>();
var globalDownloadService = app.Services.GetRequiredService<GlobalDownloads>();
GlobalChecks globalChecks = new(globalService, globalDownloadService);
await globalChecks.CheckForFFmpeg();
await globalChecks.CheckForWhisper();
await globalChecks.CheckForMake();

#endregion

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<Middleware>();
app.Run();