namespace Squish.Core.Abstractions;

public interface IProcessWrapper
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
}