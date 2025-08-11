using Squish.Core.Model;

namespace Squish.Core.Abstractions;

public interface IVideoConverter
{
    Task<ConversionResult> ConvertAsync(VideoFile file, string basePath, TimeSpan duration, ConversionOptions options, IProgress<ConversionProgress> progress);
}