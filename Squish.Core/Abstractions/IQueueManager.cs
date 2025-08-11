using Squish.Core.Model;

namespace Squish.Core.Abstractions;

public interface IQueueManager
{
    [Obsolete("Use EnqueueAsync instead")]
    void Enqueue(VideoFile file);
    [Obsolete("Use DequeueAsync instead")]
    VideoFile? Dequeue();
    int Count { get; }
    Task EnqueueAsync(VideoFile file);
    Task<VideoFile?> DequeueAsync();
    void EnqueueRange(IEnumerable<VideoFile> files);
}