using MediatR;
using WhisperAPI.Exceptions;
using WhisperAPI.Queries;
using Xabe.FFmpeg;

namespace WhisperAPI.Handlers;

public sealed class WavConverterHandler : IRequestHandler<WavConvertQuery, (string, Func<Task>)>
{
    private readonly Globals _globals;

    public WavConverterHandler(Globals globals)
    {
        _globals = globals;
    }

    private const string Error = "Could not convert file to wav";

    public async Task<(string, Func<Task>)> Handle(WavConvertQuery request, CancellationToken token)
    {
        var audioFolder = _globals.AudioFilesFolder;
        var audioFolderExists = Directory.Exists(audioFolder);
        if (!audioFolderExists)
            Directory.CreateDirectory(audioFolder);

        var extension = request.Stream.ContentType[request.Stream.ContentType.IndexOf("/",
            StringComparison.Ordinal)..][1..];
        extension = extension.Insert(0, ".");

        var reqFle = Path.Combine(audioFolder, $"{Guid.NewGuid().ToString()[..4]}{extension}");
        var wavFile = Path.Combine(audioFolder, $"{Guid.NewGuid().ToString()[..4]}.wav");

        var task = () =>
        {
            File.Delete(reqFle);
            File.Delete(wavFile);
            return Task.CompletedTask;
        };

        await request.Stream.CopyToAsync(File.Create(reqFle), token);

        var mediaInfo = await FFmpeg.GetMediaInfo(reqFle, token);
        var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
        if (audioStream is null)
            throw new FileProcessingException(Error);
        audioStream.SetCodec(AudioCodec.pcm_s16le);
        audioStream.SetChannels(1);

        var conversion = FFmpeg.Conversions.New()
            .AddStream(audioStream)
            .AddParameter("-ar 16000")
            .SetOutput(wavFile);
        try
        {
            await conversion.Start(token);
        }
        catch (Exception)
        {
            throw new FileProcessingException(Error);
        }

        return (wavFile, task);
    }
}