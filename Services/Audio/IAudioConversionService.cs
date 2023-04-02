namespace WhisperAPI.Services.Audio;

public interface IAudioConversionService
{
    Task ConvertToWavAsync(string inputFilePath, string outputFilePath);
}