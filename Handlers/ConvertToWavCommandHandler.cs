using System.Diagnostics;
using MediatR;
using WhisperAPI.Commands;
using WhisperAPI.Exceptions;

namespace WhisperAPI.Handlers;

public sealed class ConvertToWavCommandHandler : IRequestHandler<ConvertToWavCommand>
{
    private readonly Serilog.ILogger _logger;

    public ConvertToWavCommandHandler(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public async Task Handle(ConvertToWavCommand request, CancellationToken cancellationToken)
    {
        string[] ffmpegArgs =
        {
            "-i",
            request.Input,
            "-ar",
            "16000",
            "-ac",
            "1",
            "-c:a",
            "pcm_s16le",
            request.Output
        };
        
        ProcessStartInfo startInfo = new()
        {
            FileName = "ffmpeg",
            Arguments = string.Join(" ", ffmpegArgs),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(cancellationToken),
                process.StandardError.ReadToEndAsync(cancellationToken));
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            const string error = "Could not convert file to wav";
            _logger.Error(e, "[{Message}] Could not convert file to wav", e.Message);
            throw new FileProcessingException(error);
        }
    }
}