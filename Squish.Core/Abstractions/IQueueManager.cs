using Squish.Core.Model;

namespace Squish.Core.Abstractions;

public interface IQueueManager
{
    void Enqueue(VideoFile file);
    VideoFile? Dequeue();
    int Count { get; }
}