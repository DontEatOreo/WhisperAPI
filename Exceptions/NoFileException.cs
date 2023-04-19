using JetBrains.Annotations;

namespace WhisperAPI.Exceptions;

[UsedImplicitly]
public class NoFileException : FileNotFoundException
{
    public NoFileException(string message) : base (message) {}
}