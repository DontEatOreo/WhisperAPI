using System.Net;
using AsyncKeyedLock;
using Serilog;
using WhisperAPI;

await GlobalChecks.CheckForFFmpeg();
await GlobalChecks.CheckForWhisper();

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
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = (int)HttpStatusCode.PermanentRedirect;
        options.HttpsPort = 443;
    });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();