using Squish.Core.Abstractions;
using Squish.Core.Model;

namespace Squish.Core.Services;

public class FileFinder : IFileFinder
{
    private readonly string[] _videoExtensions = { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" };

    public async Task<IEnumerable<VideoFile>> FindFilesAsync(string directoryPath)
    {
        return await Task.Run(() =>
        {
            var videoFiles = new List<VideoFile>();

            foreach (var extension in _videoExtensions)
            {
                var files = Directory.EnumerateFiles(directoryPath, $"*{extension}", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    videoFiles.Add(new VideoFile
                    {
                        FilePath = file,
                        FileSize = fileInfo.Length
                    });
                }
            }

            return videoFiles.OrderByDescending(f => f.FileSize);
        });
    }
}