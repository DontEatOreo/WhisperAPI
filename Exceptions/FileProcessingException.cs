namespace WhisperAPI.Exceptions;

public class FileProcessingException : Exception
{
    public FileProcessingException(string message) : base(message) { }
}