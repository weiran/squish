using Squish.Core.Model;

namespace Squish.Core.Abstractions;

public interface IFileFinder
{
    Task<IEnumerable<VideoFile>> FindFilesAsync(string directoryPath);
}