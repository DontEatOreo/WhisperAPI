namespace WhisperAPI.Exceptions;

public class NoFileException(string message) : FileNotFoundException(message);