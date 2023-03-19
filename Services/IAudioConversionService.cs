namespace WhisperAPI.Services;

public interface IAudioConversionService
{
    Task ConvertToWavAsync(string inputFilePath, string outputFilePath);
}