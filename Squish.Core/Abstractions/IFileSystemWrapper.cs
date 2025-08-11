namespace Squish.Core.Abstractions;

public interface IFileSystemWrapper
{
    // File operations
    bool FileExists(string path);
    void SetFileAttributes(string path, FileAttributes attributes);
    DateTime GetCreationTime(string path);
    DateTime GetLastWriteTime(string path);
    DateTime GetLastAccessTime(string path);
    void SetCreationTime(string path, DateTime creationTime);
    void SetLastWriteTime(string path, DateTime lastWriteTime);
    void SetLastAccessTime(string path, DateTime lastAccessTime);
    void MoveFile(string sourceFileName, string destFileName, bool overwrite);
    void DeleteFile(string path);
    FileInfo GetFileInfo(string path);
    
    // Directory operations
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    long GetFileSize(string filePath);
    bool DirectoryExists(string path);
}

