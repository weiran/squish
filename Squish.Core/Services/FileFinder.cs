using Squish.Core.Abstractions;
using Squish.Core.Model;

namespace Squish.Core.Services;

public class FileFinder : IFileFinder
{
    private readonly IFileSystemWrapper _fileSystemWrapper;
    private readonly string[] _videoExtensions = { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" };

    public FileFinder(IFileSystemWrapper fileSystemWrapper)
    {
        _fileSystemWrapper = fileSystemWrapper ?? throw new ArgumentNullException(nameof(fileSystemWrapper));
    }

    public async Task<IEnumerable<VideoFile>> FindFilesAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        if (!_fileSystemWrapper.DirectoryExists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        return await Task.Run(() =>
        {
            var videoFiles = new List<VideoFile>();

            foreach (var extension in _videoExtensions)
            {
                var files = _fileSystemWrapper.EnumerateFiles(directoryPath, $"*{extension}", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    try
                    {
                        var fileSize = _fileSystemWrapper.GetFileSize(file);
                        videoFiles.Add(new VideoFile
                        {
                            FilePath = file,
                            FileSize = fileSize
                        });
                    }
                    catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException)
                    {
                        // Skip files that can't be accessed
                        continue;
                    }
                }
            }

            return videoFiles.OrderByDescending(f => f.FileSize);
        });
    }
}