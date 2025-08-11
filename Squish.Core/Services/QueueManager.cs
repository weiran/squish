using System.Collections.Concurrent;
using Squish.Core.Abstractions;
using Squish.Core.Model;

namespace Squish.Core.Services;

public class QueueManager : IQueueManager
{
    private readonly ConcurrentQueue<VideoFile> _queue = new();
    private int _count = 0;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task EnqueueAsync(VideoFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        await _semaphore.WaitAsync();
        try
        {
            _queue.Enqueue(file);
            _count++;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<VideoFile?> DequeueAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_queue.TryDequeue(out var file))
            {
                _count--;
                return file;
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public int Count => _count;

    public async Task EnqueueRangeAsync(IEnumerable<VideoFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var sortedFiles = files.OrderByDescending(f => f.FileSize);
        foreach (var file in sortedFiles)
        {
            await EnqueueAsync(file);
        }
    }

    public void Enqueue(VideoFile file)
    {
        EnqueueAsync(file).GetAwaiter().GetResult();
    }

    public VideoFile? Dequeue()
    {
        return DequeueAsync().GetAwaiter().GetResult();
    }

    public void EnqueueRange(IEnumerable<VideoFile> files)
    {
        EnqueueRangeAsync(files).GetAwaiter().GetResult();
    }
}