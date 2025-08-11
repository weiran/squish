using Squish.Core.Abstractions;

namespace Squish.Core.Services;

public class FileSystemWrapper : IFileSystemWrapper
{
    // File operations
    public bool FileExists(string path) => File.Exists(path);

    public void SetFileAttributes(string path, FileAttributes attributes) 
        => File.SetAttributes(path, attributes);

    public DateTime GetCreationTime(string path) 
        => File.GetCreationTime(path);

    public DateTime GetLastWriteTime(string path) 
        => File.GetLastWriteTime(path);

    public DateTime GetLastAccessTime(string path) 
        => File.GetLastAccessTime(path);

    public void SetCreationTime(string path, DateTime creationTime) 
        => File.SetCreationTime(path, creationTime);

    public void SetLastWriteTime(string path, DateTime lastWriteTime) 
        => File.SetLastWriteTime(path, lastWriteTime);

    public void SetLastAccessTime(string path, DateTime lastAccessTime) 
        => File.SetLastAccessTime(path, lastAccessTime);

    public void MoveFile(string sourceFileName, string destFileName, bool overwrite) 
        => File.Move(sourceFileName, destFileName, overwrite);

    public void DeleteFile(string path) 
        => File.Delete(path);

    public FileInfo GetFileInfo(string path) 
        => new FileInfo(path);

    // Directory operations
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