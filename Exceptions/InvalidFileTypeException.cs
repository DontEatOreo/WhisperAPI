using JetBrains.Annotations;

namespace WhisperAPI.Exceptions;

[UsedImplicitly]
public class InvalidFileTypeException : Exception
{
    public InvalidFileTypeException(string message) : base (message) { }
}