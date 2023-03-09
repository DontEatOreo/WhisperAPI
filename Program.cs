using System.Net;
using AsyncKeyedLock;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using WhisperAPI;

var builder = WebApplication.CreateBuilder();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton(new AsyncKeyedLocker<string>(o =>
{
    o.PoolSize = Globals.ThreadCount * 4;
    o.PoolInitialFill = o.PoolSize / 2;
    // We determine the number of threads available to the CPU,
    // then divide it by two to calculate the maximum number of Whisper instances that can run simultaneously.
    // This ensures each instance has a minimum of two threads to work with, unless the CPU only possesses a single thread.
    o.MaxCount = Globals.ThreadCount > 1 ? Globals.ThreadCount / 2 : 1;
}));
builder.Services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = 53248000); // 50 Mib + 100 kib
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

await GlobalChecks.CheckForFFmpeg();
await GlobalChecks.CheckForWhisper();
await GlobalChecks.CheckForMake();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();