using Squish.Core.Model;

namespace Squish.Core.Abstractions;

public interface IJobRunner
{
    Task<IEnumerable<ConversionResult>> RunAsync(
        string directoryPath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}