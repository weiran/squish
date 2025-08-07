namespace Squish.Core.Exceptions;

public class VideoConversionException : Exception
{
    public VideoConversionException() { }
    
    public VideoConversionException(string message) : base(message) { }
    
    public VideoConversionException(string message, Exception innerException) : base(message, innerException) { }
}