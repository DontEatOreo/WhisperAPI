using JetBrains.Annotations;

namespace WhisperAPI.Exceptions;

[UsedImplicitly]
public class InvalidLanguageException : Exception
{
    public InvalidLanguageException(string message) : base(message) { }
}