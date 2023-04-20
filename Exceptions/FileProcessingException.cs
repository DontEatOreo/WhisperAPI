using JetBrains.Annotations;

namespace WhisperAPI.Exceptions;

[UsedImplicitly]
public class FileProcessingException : Exception
{
    public FileProcessingException(string message) : base(message) { }
}