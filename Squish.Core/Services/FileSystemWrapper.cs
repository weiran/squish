using Squish.Core.Abstractions;

namespace Squish.Core.Services;

public class FileSystemWrapper : IFileSystemWrapper
{
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.EnumerateFiles(path, searchPattern, searchOption);
    }

    public long GetFileSize(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }
}