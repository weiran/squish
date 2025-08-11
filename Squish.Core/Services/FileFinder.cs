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
            var extensionSet = new HashSet<string>(_videoExtensions, StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Single enumeration of all files, filter by extension
                var allFiles = _fileSystemWrapper.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories);
                
                foreach (var file in allFiles)
                {
                    try
                    {
                        var extension = Path.GetExtension(file);
                        if (!string.IsNullOrEmpty(extension) && extensionSet.Contains(extension))
                        {
                            var fileSize = _fileSystemWrapper.GetFileSize(file);
                            videoFiles.Add(new VideoFile
                            {
                                FilePath = file,
                                FileSize = fileSize
                            });
                        }
                    }
                    catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
                    {
                        // Skip files that can't be accessed
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // If we can't access the directory at all, return empty list
                // This handles cases like system directories with restricted access
            }

            return videoFiles.OrderByDescending(f => f.FileSize);
        });
    }
}