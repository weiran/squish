namespace Squish.Core.Abstractions;

public interface IVideoInspector
{
    Task<string> GetVideoCodecAsync(string filePath);
    Task<TimeSpan> GetVideoDurationAsync(string filePath);
}