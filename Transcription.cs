using System.Globalization;
using System.Text.Json;
using AsyncKeyedLock;
using CliWrap;
using CsvHelper;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;
using Serilog;
using WhisperAPI.Models;
using static WhisperAPI.Globals;

namespace WhisperAPI;

public sealed class Transcription
{
    public Transcription(AsyncKeyedLocker<string> asyncKeyedLocker)
    {
        MaxCount = asyncKeyedLocker.MaxCount;
    }

    private static int MaxCount { get; set; }

    public static async Task<(string? transcription, string? errorCode, string? errorMessage)> TranscribeAudio(string fileBase64,
        string lang,
        bool translate,
        WhisperModel whisperModel,
        bool timeStamp)
    {
        var fileId = Guid.NewGuid().ToString();
        var selectedModelPath = ModelFilePaths[whisperModel];
        var selectedOutputFormat = OutputFormatMapping[timeStamp];

        await DownloadModelIfNotExists(whisperModel, selectedModelPath);

        var (fileBytes, fileExtension) = GetFileData(fileBase64);
        if (fileBytes is null || fileExtension is null)
            return (null, ErrorCodesAndMessages.InvalidFileType, ErrorCodesAndMessages.InvalidFileTypeMessage);

        var fileSize = fileBytes.Length;
        const int sizeLimit = 52428800; // 52428800 is 50 mib
        if (fileSize > sizeLimit)
            return (null, ErrorCodesAndMessages.FileSizeExceeded, ErrorCodesAndMessages.FileSizeExceededMessage);

        var fileName = Path.Combine(WhisperFolder, $"{fileId}.{fileExtension}");
        var audioFile = Path.Combine(WhisperFolder, $"{fileId}.wav"); // the output file (It needs to be wav)
        await using FileStream fileStream = new(fileName, FileMode.Create, FileAccess.Write);
        await fileStream.WriteAsync(fileBytes);

        await ConvertToWav(fileName, audioFile);

        // The CLI Arguments in Whisper.cpp for the output file format are `-o<extension>` so we just parse the extension after the `-o`
        var transcribedFilePath = Path.Combine(WhisperFolder, $"{audioFile}.{selectedOutputFormat[2..]}");
        await Transcribe(audioFile, lang, translate, selectedModelPath, selectedOutputFormat);

        if (timeStamp)
        {
            var jsonLines = ConvertToJson(transcribedFilePath);
            CleanUp(fileName, audioFile, transcribedFilePath);
            var serialized = JsonSerializer.Serialize(jsonLines, new JsonSerializerOptions { WriteIndented = true }).Trim();
            return (serialized, null, null);
        }

        var transcribedText = await File.ReadAllTextAsync(transcribedFilePath);
        CleanUp(fileName, audioFile, transcribedFilePath);
        return (transcribedText.Trim(), null, null);
    }

    private static async Task DownloadModelIfNotExists(WhisperModel whisperModel, string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            Log.Information("Model {WhisperModel} doesn't exist, downloading...", whisperModel);
            await GlobalDownloads.DownloadModels(whisperModel);
        }
    }

    private static (byte[]?, string?) GetFileData(string fileBase64)
    {
        try
        {
            var fileBytes = Convert.FromBase64String(fileBase64);
            /*
             * We don't know ahead of time what extension the file will have.
             * So we use MimeDetective to get the extension from the file bytes.
             * That way if it's not a video or audio file, we can just return null.
             */
            var inspector = new ContentInspectorBuilder
            {
                Definitions = new CondensedBuilder
                    { UsageType = UsageType.PersonalNonCommercial }.Build()
            }.Build();
            var definition = inspector.Inspect(fileBytes).FirstOrDefault();
            // https://github.com/MediatedCommunications/Mime-Detective#mime-detectivedefinitionscondensed
            // Supported Video Extensions: 3g2 3gp avi flv h264 m4v mkv mov mp4 mpg mpeg rm swf vob wmv
            // Supported Audio Extensions: aif cda mid midi mp3 mpa ogg wav wma wpl
            var mimeType = definition?.Definition.File.MimeType;
            if (mimeType is null || (!mimeType.Contains("video") && !mimeType.Contains("audio")))
                return (null, null);
            var fileExtension = definition?.Definition.File.Extensions.FirstOrDefault();
            return (fileBytes, fileExtension);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static async Task ConvertToWav(string fileName, string audioFile)
    {
        string[] ffmpegArgs =
        {
            "-i",
            fileName,
            "-ar",
            "16000",
            "-ac",
            "1",
            "-c:a",
            "pcm_s16le",
            audioFile
        };

        try
        {
            await Cli.Wrap("ffmpeg")
                .WithArguments(arg =>
                {
                    foreach (var ffmpegArg in ffmpegArgs)
                        arg.Add(ffmpegArg);
                })
                .ExecuteAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "[{Message}] Could not convert file to wav", e.Message);
        }
    }

    private static async Task Transcribe(string audioFile,
        string lang,
        bool translate,
        string modelPath,
        string outputFormat)
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
            $"{Math.Max(MaxCount / 2, 1)}"
        };
        if (translate)
            whisperArgs.Add("-tr");

        await Cli.Wrap(WhisperExecPath)
            .WithArguments(arg =>
            {
                foreach (var whisperArg in whisperArgs)
                    arg.Add(whisperArg);
            })
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
    }

    private static List<TimeStamp> ConvertToJson(string path)
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

    private static void CleanUp(params string[] filePaths)
    {
        foreach (var filePath in filePaths)
            File.Delete(filePath);
    }
}