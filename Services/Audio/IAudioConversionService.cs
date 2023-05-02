namespace WhisperAPI.Services.Audio;

public interface IAudioConversionService
{
    Task ConvertToWavAsync(string input, string output);
}