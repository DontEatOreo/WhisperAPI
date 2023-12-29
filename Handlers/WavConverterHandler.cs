using JetBrains.Annotations;
using MediatR;
using WhisperAPI.Exceptions;
using WhisperAPI.Queries;
using Xabe.FFmpeg;

namespace WhisperAPI.Handlers;

[UsedImplicitly]
public sealed class WavConverterHandler(Globals globals) : IRequestHandler<WavConvertQuery, (string, Func<Task>)>
{
    private const string Error = "Could not convert file to wav";

    public async Task<(string, Func<Task>)> Handle(WavConvertQuery request, CancellationToken token)
    {
        var audioFolder = globals.AudioFilesFolder;
        var audioFolderExists = Directory.Exists(audioFolder);
        if (!audioFolderExists)
            Directory.CreateDirectory(audioFolder);

        var extension = request.Stream.ContentType[request.Stream.ContentType.IndexOf('/')..][1..];
        extension = extension.Insert(0, ".");

        var reqFle = Path.Combine(audioFolder, $"{Guid.NewGuid().ToString()[..4]}{extension}");
        var wavFile = Path.Combine(audioFolder, $"{Guid.NewGuid().ToString()[..4]}.wav");

        await using (var reqFileTemp = File.Create(reqFle))
        {
            await request.Stream.CopyToAsync(reqFileTemp, token);
        }

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

        var task = () =>
        {
            File.Delete(reqFle);
            File.Delete(wavFile);
            return Task.CompletedTask;
        };

        return (wavFile, task);
    }
}