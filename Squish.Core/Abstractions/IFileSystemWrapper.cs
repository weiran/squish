namespace Squish.Core.Abstractions;

public interface IFileSystemWrapper
{
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    long GetFileSize(string filePath);
    bool DirectoryExists(string path);
}