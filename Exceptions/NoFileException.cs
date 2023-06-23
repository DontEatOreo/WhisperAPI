namespace WhisperAPI.Exceptions;

public class NoFileException : FileNotFoundException
{
    public NoFileException(string message) : base(message) { }
}