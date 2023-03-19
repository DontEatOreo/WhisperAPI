using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;

namespace WhisperAPI.Services;

public class FileService
{
    public async Task SaveFileAsync(byte[] fileBytes, string filePath)
    {
        await using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
        await fileStream.WriteAsync(fileBytes);
    }

    public (byte[]?, string?) GetFileData(string fileBase64)
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

    public void CleanUp(params string[] filePaths)
    {
        foreach (var filePath in filePaths)
            File.Delete(filePath);
    }
}