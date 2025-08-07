using System.Diagnostics;
using Squish.Core.Abstractions;

namespace Squish.Core.Services;

public class ProcessWrapper : IProcessWrapper
{
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
            throw new InvalidOperationException($"Failed to start {fileName} process");

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError
        };
    }
}