using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using WhisperAPI.Exceptions;
using WhisperAPI.Models;
using ILogger = Serilog.ILogger;

namespace WhisperAPI.Services.Transcription;

public class TranscriptionHelper
{
    #region Ctor

    private readonly Globals _globals;
    private readonly ILogger _logger;
    private readonly GlobalDownloads _globalDownloads;

    public TranscriptionHelper(Globals globals, ILogger logger, GlobalDownloads globalDownloads)
    {
        _globals = globals;
        _logger = logger;
        _globalDownloads = globalDownloads;
    }

    #endregion Ctor

    private readonly object _modelDownloadLock = new();

    #region Methods

    public async Task Transcribe(TranscriptionOptions options, CancellationToken token)
    {
        List<string> whisperArgs = new()
        {
            "-f",
            options.AudioFile,
            "-m",
            options.ModelPath,
            "-l",
            options.Language,
            options.OutputFormat,
            "-t 2"
        };
        if (options.Translate)
            whisperArgs.Add("-tr");

        ProcessStartInfo startInfo = new()
        {
            FileName = _globals.WhisperExecPath,
            Arguments = string.Join(" ", whisperArgs),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(token),
                process.StandardError.ReadToEndAsync(token));
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Whisper was cancelled for {AudioFile}", options.AudioFile);
        }
        catch (Exception)
        {
            _logger.Error("Whisper failed to transcribe {AudioFile}", options.AudioFile);
            throw new FileProcessingException("Whisper failed to transcribe the audio file");
        }
    }

    public void DownloadModelIfNotExists(WhisperModel whisperModel, string modelPath)
    {
        if (File.Exists(modelPath)) return;

        lock (_modelDownloadLock)
        {
            if (File.Exists(modelPath)) return;

            _logger.Information("Model {WhisperModel} doesn't exist, downloading...", whisperModel);
            _globalDownloads.Model(whisperModel).Wait();
        }
    }

    public List<TimeStamp> ConvertToJson(string path)
    {
        List<TimeStamp> jsonLines = new();
        /*
         * The csv format:
         * start,end,text
         * 0,5120," Most conversations about performance are a total waste of time, not because performance"
         * 5120,10600," is an important, but because people feel very, very strongly about performance."
         */
        StreamReader reader;
        try
        {
            reader = new StreamReader(path);
        }
        catch (Exception)
        {
            throw new FileProcessingException("Whisper failed to transcribe the audio file");
        }
        using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<CsvFile>();

        foreach (var record in records)
        {
            var text = record.Text.Trim();
            var start = int.TryParse(record.Start, out var startInt) ? TimeSpan.FromMilliseconds(startInt) : TimeSpan.Zero;
            var end = int.TryParse(record.End, out var endInt) ? TimeSpan.FromMilliseconds(endInt) : TimeSpan.Zero;
            jsonLines.Add(new TimeStamp
            {
                Start = (int)start.TotalSeconds,
                End = (int)end.TotalSeconds,
                Text = text
            });
        }

        return jsonLines;
    }

    #endregion Methods
}