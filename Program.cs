using AsyncKeyedLock;
using WhisperAPI;

await GlobalChecks.CheckForFFmpeg();
await GlobalChecks.CheckForWhisper();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 443;
});
builder.Services.AddSingleton(new AsyncKeyedLocker<string>(o =>
{
    o.PoolSize = Globals.ThreadCount * 4;
    o.PoolInitialFill = o.PoolSize / 2;
    // We determine the number of threads available to the CPU, then divide it by two to calculate the maximum number of Whisper instances that can run simultaneously.
    // This ensures each instance has a minimum of two threads to work with, unless the CPU only possesses a single thread.
    o.MaxCount = Globals.ThreadCount > 1 ? Globals.ThreadCount / 2 : 1;
}));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();