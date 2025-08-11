using Squish.Core.Abstractions;

namespace Squish.Core.Services;

public class InMemoryLogger : ILogger
{
    private readonly List<string> _warnings = new();
    private readonly List<string> _errors = new();
    private readonly object _lock = new();

    public void LogWarning(string message)
    {
        lock (_lock)
        {
            _warnings.Add(message);
        }
    }

    public void LogError(string message)
    {
        lock (_lock)
        {
            _errors.Add(message);
        }
    }

    public IReadOnlyList<string> Warnings 
    {
        get
        {
            lock (_lock)
            {
                return _warnings.ToList();
            }
        }
    }

    public IReadOnlyList<string> Errors 
    {
        get
        {
            lock (_lock)
            {
                return _errors.ToList();
            }
        }
    }
}