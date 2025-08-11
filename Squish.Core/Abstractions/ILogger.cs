namespace Squish.Core.Abstractions;

public interface ILogger
{
    void LogWarning(string message);
    void LogError(string message);
}