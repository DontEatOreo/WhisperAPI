using JetBrains.Annotations;

namespace WhisperAPI.Exceptions;

[UsedImplicitly]
public class InvalidModelException : Exception
{
    public InvalidModelException(string message) : base(message) { }
}