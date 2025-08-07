using FluentAssertions;
using Squish.Core.Model;
using Squish.Core.Services;
using Xunit;

namespace Squish.Core.Tests.Services;

public class QueueManagerTests
{
    [Fact]
    public void Constructor_InitializesEmptyQueue()
    {
        var queueManager = new QueueManager();

        queueManager.Count.Should().Be(0);
    }

    [Fact]
    public void Enqueue_AddsFileToQueue_AndIncrementsCount()
    {
        var queueManager = new QueueManager();
        var videoFile = new VideoFile { FilePath = "/test/video.mp4", FileSize = 1000 };

        queueManager.Enqueue(videoFile);

        queueManager.Count.Should().Be(1);
    }

    [Fact]
    public void Enqueue_MultipleFiles_IncrementsCountCorrectly()
    {
        var queueManager = new QueueManager();
        var file1 = new VideoFile { FilePath = "/test/video1.mp4", FileSize = 1000 };
        var file2 = new VideoFile { FilePath = "/test/video2.mp4", FileSize = 2000 };

        queueManager.Enqueue(file1);
        queueManager.Enqueue(file2);

        queueManager.Count.Should().Be(2);
    }

    [Fact]
    public void Dequeue_ReturnsNull_WhenQueueIsEmpty()
    {
        var queueManager = new QueueManager();

        var result = queueManager.Dequeue();

        result.Should().BeNull();
        queueManager.Count.Should().Be(0);
    }

    [Fact]
    public void Dequeue_ReturnsFile_WhenQueueHasItems()
    {
        var queueManager = new QueueManager();
        var videoFile = new VideoFile { FilePath = "/test/video.mp4", FileSize = 1000 };
        queueManager.Enqueue(videoFile);

        var result = queueManager.Dequeue();

        result.Should().NotBeNull();
        result!.FilePath.Should().Be("/test/video.mp4");
        result.FileSize.Should().Be(1000);
        queueManager.Count.Should().Be(0);
    }

    [Fact]
    public void Dequeue_ReturnsFilesInFifoOrder()
    {
        var queueManager = new QueueManager();
        var file1 = new VideoFile { FilePath = "/test/video1.mp4", FileSize = 1000 };
        var file2 = new VideoFile { FilePath = "/test/video2.mp4", FileSize = 2000 };

        queueManager.Enqueue(file1);
        queueManager.Enqueue(file2);

        var result1 = queueManager.Dequeue();
        var result2 = queueManager.Dequeue();

        result1!.FilePath.Should().Be("/test/video1.mp4");
        result2!.FilePath.Should().Be("/test/video2.mp4");
        queueManager.Count.Should().Be(0);
    }

    [Fact]
    public void EnqueueRange_AddsMultipleFiles_OrderedBySize()
    {
        var queueManager = new QueueManager();
        var files = new[]
        {
            new VideoFile { FilePath = "/test/small.mp4", FileSize = 500 },
            new VideoFile { FilePath = "/test/large.mp4", FileSize = 2000 },
            new VideoFile { FilePath = "/test/medium.mp4", FileSize = 1000 }
        };

        queueManager.EnqueueRange(files);

        queueManager.Count.Should().Be(3);
        
        // Should be dequeued in the order they were added (largest first due to sorting)
        var first = queueManager.Dequeue();
        var second = queueManager.Dequeue();
        var third = queueManager.Dequeue();

        first!.FilePath.Should().Be("/test/large.mp4");
        second!.FilePath.Should().Be("/test/medium.mp4");
        third!.FilePath.Should().Be("/test/small.mp4");
    }

    [Fact]
    public void EnqueueRange_WithEmptyCollection_DoesNotChangeCount()
    {
        var queueManager = new QueueManager();
        var files = Array.Empty<VideoFile>();

        queueManager.EnqueueRange(files);

        queueManager.Count.Should().Be(0);
    }

    [Fact]
    public void Count_IsThreadSafe_WhenAccessedConcurrently()
    {
        var queueManager = new QueueManager();
        var tasks = new List<Task>();

        // Add files concurrently
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var file = new VideoFile { FilePath = $"/test/video{index}.mp4", FileSize = index };
                queueManager.Enqueue(file);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        queueManager.Count.Should().Be(100);
    }

    [Fact]
    public void EnqueueDequeue_IsThreadSafe_WhenAccessedConcurrently()
    {
        var queueManager = new QueueManager();
        var enqueueTasks = new List<Task>();
        var dequeueTasks = new List<Task>();
        var dequeuedItems = new List<VideoFile?>();
        var lockObject = new object();

        // Enqueue 50 files
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            enqueueTasks.Add(Task.Run(() =>
            {
                var file = new VideoFile { FilePath = $"/test/video{index}.mp4", FileSize = index };
                queueManager.Enqueue(file);
            }));
        }

        // Dequeue 25 files
        for (int i = 0; i < 25; i++)
        {
            dequeueTasks.Add(Task.Run(() =>
            {
                var item = queueManager.Dequeue();
                lock (lockObject)
                {
                    dequeuedItems.Add(item);
                }
            }));
        }

        Task.WaitAll(enqueueTasks.ToArray());
        Task.WaitAll(dequeueTasks.ToArray());

        queueManager.Count.Should().Be(25); // 50 enqueued - 25 dequeued
        dequeuedItems.Count(x => x != null).Should().BeGreaterOrEqualTo(0);
        dequeuedItems.Count(x => x != null).Should().BeLessOrEqualTo(25);
    }

    [Fact]
    public void Enqueue_ThrowsArgumentNullException_WhenFileIsNull()
    {
        var queueManager = new QueueManager();

        var act = () => queueManager.Enqueue(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}