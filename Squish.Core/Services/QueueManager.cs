using System.Collections.Concurrent;
using Squish.Core.Abstractions;
using Squish.Core.Model;

namespace Squish.Core.Services;

public class QueueManager : IQueueManager
{
    private readonly ConcurrentQueue<VideoFile> _queue = new();
    private int _count = 0;

    public void Enqueue(VideoFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _queue.Enqueue(file);
        Interlocked.Increment(ref _count);
    }

    public VideoFile? Dequeue()
    {
        if (_queue.TryDequeue(out var file))
        {
            Interlocked.Decrement(ref _count);
            return file;
        }
        return null;
    }

    public int Count => _count;

    public void EnqueueRange(IEnumerable<VideoFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var sortedFiles = files.OrderByDescending(f => f.FileSize);
        foreach (var file in sortedFiles)
        {
            Enqueue(file);
        }
    }
}