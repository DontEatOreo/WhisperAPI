using System.Globalization;
using AsyncKeyedLock;
using CliWrap;
using CsvHelper;
using Serilog;
using WhisperAPI.Models;

namespace WhisperAPI.Services.Transcription;

public class TranscriptionHelper
{
    #region Constructor

    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly Globals _globals;
    private readonly IGlobalDownloads _globalDownloads;

    public TranscriptionHelper(AsyncKeyedLocker<string> asyncKeyedLocker, IGlobalDownloads globalDownloads, Globals globals)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _globalDownloads = globalDownloads;
        _globals = globals;
    }

    #endregion

    private const string DownloadKey = "download";

    #region Methods

    public async Task Transcribe(
        string audioFile,
        string lang,
        bool translate,
        string modelPath,
        string outputFormat,
        CancellationToken token)
    {
        List<string> whisperArgs = new()
        {
            "-f",
            audioFile,
            "-m",
            modelPath,
            "-l",
            lang,
            outputFormat,
            "-t",
            $"{Math.Max(_asyncKeyedLocker.MaxCount / 2, 1)}"
        };
        if (translate)
            whisperArgs.Add("-tr");

        try
        {
            await Cli.Wrap(_globals.WhisperExecPath)
                .WithArguments(arg =>
                {
                    foreach (var whisperArg in whisperArgs)
                        arg.Add(whisperArg);
                })
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .ExecuteAsync(token);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception)
        {
            Log.Error("Whisper failed to transcribe {AudioFile}", audioFile);
        }
    }

    public async Task DownloadModelIfNotExists(WhisperModel whisperModel, string modelPath)
    {
        await _asyncKeyedLocker.LockAsync(DownloadKey).ConfigureAwait(false);
        if (!File.Exists(modelPath))
        {
            Log.Information("Model {WhisperModel} doesn't exist, downloading...", whisperModel);
            await _globalDownloads.DownloadModels(whisperModel);
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
        using StreamReader reader = new(path);
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

    #endregion
}