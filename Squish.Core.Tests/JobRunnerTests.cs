using FluentAssertions;
using Moq;
using Squish.Core.Abstractions;
using Squish.Core.Model;
using Xunit;

namespace Squish.Core.Tests;

public class JobRunnerTests
{
    private readonly Mock<IFileFinder> _mockFileFinder;
    private readonly Mock<IVideoInspector> _mockVideoInspector;
    private readonly Mock<IVideoConverter> _mockVideoConverter;
    private readonly Mock<IQueueManager> _mockQueueManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly JobRunner _jobRunner;

    public JobRunnerTests()
    {
        _mockFileFinder = new Mock<IFileFinder>();
        _mockVideoInspector = new Mock<IVideoInspector>();
        _mockVideoConverter = new Mock<IVideoConverter>();
        _mockQueueManager = new Mock<IQueueManager>();
        _mockLogger = new Mock<ILogger>();
        
        _jobRunner = new JobRunner(
            _mockFileFinder.Object,
            _mockVideoInspector.Object,
            _mockVideoConverter.Object,
            _mockQueueManager.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RunAsync_ReportsDiscoveryPhase()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        var progressReports = new List<ConversionProgress>();
        var progress = new Progress<ConversionProgress>(p => progressReports.Add(p));

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(new List<VideoFile>());

        // Act
        await _jobRunner.RunAsync(directoryPath, options, progress);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.First().CurrentFile.Should().Be("Discovering video files...");
    }

    [Fact]
    public async Task RunAsync_ReportsInspectionPhase()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        var progressReports = new List<ConversionProgress>();
        var progress = new Progress<ConversionProgress>(p => progressReports.Add(p));

        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 },
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync("h264");

        // Act
        await _jobRunner.RunAsync(directoryPath, options, progress);

        // Assert
        var inspectionReport = progressReports.FirstOrDefault(p => p.CurrentFile == "Inspecting video codecs...");
        inspectionReport.Should().NotBeNull();
        inspectionReport!.TotalFiles.Should().Be(2);
        inspectionReport.CompletedFiles.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_FiltersHevcCodecs()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 }, // h264 - should be converted
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 }, // hevc - should be skipped
            new() { FilePath = "/test/video3.mp4", FileSize = 3000 }  // h265 - should be skipped
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync("/test/video1.mp4"))
            .ReturnsAsync("h264");
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync("/test/video2.mp4"))
            .ReturnsAsync("hevc");
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync("/test/video3.mp4"))
            .ReturnsAsync("h265");

        _mockQueueManager.Setup(q => q.Count).Returns(0);

        // Act
        var results = await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        _mockQueueManager.Verify(q => q.EnqueueRange(It.Is<IEnumerable<VideoFile>>(
            files => files.Count() == 1 && files.First().FilePath == "/test/video1.mp4")), Times.Once);
    }

    [Fact]
    public async Task RunAsync_RespectsLimitOption()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions { Limit = 2 };
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 },
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 },
            new() { FilePath = "/test/video3.mp4", FileSize = 3000 },
            new() { FilePath = "/test/video4.mp4", FileSize = 4000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync("h264");
        
        _mockQueueManager.Setup(q => q.Count).Returns(0);

        // Act
        await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        _mockQueueManager.Verify(q => q.EnqueueRange(It.Is<IEnumerable<VideoFile>>(
            files => files.Count() == 2)), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ListOnlyMode_ReturnsResultsWithoutConversion()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions { ListOnly = true };
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 },
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync("h264");

        // Act
        var results = await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();
        results.Select(r => r.FilePath).Should().BeEquivalentTo(new[] { "/test/video1.mp4", "/test/video2.mp4" });
        
        // Should not enqueue for conversion
        _mockQueueManager.Verify(q => q.EnqueueRange(It.IsAny<IEnumerable<VideoFile>>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_HandlesVideoInspectorErrors_AndLogsWarnings()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 },
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync("/test/video1.mp4"))
            .ReturnsAsync("h264");
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync("/test/video2.mp4"))
            .ThrowsAsync(new InvalidOperationException("ffprobe failed"));

        _mockQueueManager.Setup(q => q.Count).Returns(0);

        // Act
        await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        _mockLogger.Verify(l => l.LogWarning(It.Is<string>(msg => 
            msg.Contains("/test/video2.mp4") && msg.Contains("ffprobe failed"))), Times.Once);
        
        // Should only enqueue the successfully inspected file
        _mockQueueManager.Verify(q => q.EnqueueRange(It.Is<IEnumerable<VideoFile>>(
            files => files.Count() == 1 && files.First().FilePath == "/test/video1.mp4")), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ProcessesFilesFromQueue()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions { ParallelJobs = 1 };
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 }
        };

        var conversionResult = new ConversionResult
        {
            FilePath = "/test/video1.mp4",
            Success = true,
            OriginalSize = 1000,
            NewSize = 800
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync("h264");
        _mockVideoInspector.Setup(v => v.GetVideoDurationAsync(It.IsAny<string>()))
            .ReturnsAsync(TimeSpan.FromMinutes(10));
        
        // Setup queue manager to properly simulate queue behavior
        // The queue starts with 1 item, then becomes empty after dequeue
        var queueCallCount = 0;
        _mockQueueManager.Setup(q => q.Count).Returns(() => 
        {
            // First several calls during processing loop return 1, then 0 
            return queueCallCount++ < 3 ? 1 : 0;
        });
        
        // Setup dequeue to return the file once, then null for subsequent calls
        var dequeueCallCount = 0;
        _mockQueueManager.Setup(q => q.DequeueAsync()).ReturnsAsync(() => 
        {
            return dequeueCallCount++ == 0 ? files[0] : null;
        });
        
        _mockVideoConverter.Setup(v => v.ConvertAsync(
            It.IsAny<VideoFile>(), 
            It.IsAny<string>(), 
            It.IsAny<TimeSpan>(), 
            It.IsAny<ConversionOptions>(),
            It.IsAny<IProgress<ConversionProgress>>()))
            .ReturnsAsync(conversionResult);

        // Act
        var results = await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        results.Should().HaveCount(1);
        results.First().Should().BeEquivalentTo(conversionResult);
        
        _mockVideoConverter.Verify(v => v.ConvertAsync(
            It.Is<VideoFile>(f => f.FilePath == "/test/video1.mp4"),
            directoryPath,
            TimeSpan.FromMinutes(10),
            options,
            It.IsAny<IProgress<ConversionProgress>>()), Times.Once);
        
        // Verify the file was properly enqueued
        _mockQueueManager.Verify(q => q.EnqueueRange(It.Is<IEnumerable<VideoFile>>(
            f => f.Count() == 1 && f.First().FilePath == "/test/video1.mp4")), Times.Once);
    }

    [Fact]
    public async Task RunAsync_HandlesCancellation()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        var cancellationTokenSource = new CancellationTokenSource();
        
        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(new List<VideoFile>());

        // Cancel immediately
        cancellationTokenSource.Cancel();

        // Act & Assert
        var act = async () => await _jobRunner.RunAsync(directoryPath, options, null, cancellationTokenSource.Token);
        
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("hevc", true)]
    [InlineData("HEVC", true)]
    [InlineData("h265", true)]
    [InlineData("H265", true)]
    [InlineData("h264", false)]
    [InlineData("av1", false)]
    [InlineData("", false)]
    public async Task RunAsync_HevcCodecDetection_WorksCorrectly(string codec, bool shouldSkip)
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync(codec);
        
        _mockQueueManager.Setup(q => q.Count).Returns(0);

        // Act
        await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        var expectedCount = shouldSkip ? 0 : 1;
        _mockQueueManager.Verify(q => q.EnqueueRange(It.Is<IEnumerable<VideoFile>>(
            files => files.Count() == expectedCount)), Times.Once);
    }

    [Fact]
    public async Task RunAsync_RespectsParallelJobsLimit()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions { ParallelJobs = 2 };
        
        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 },
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 },
            new() { FilePath = "/test/video3.mp4", FileSize = 3000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync("h264");
        _mockVideoInspector.Setup(v => v.GetVideoDurationAsync(It.IsAny<string>()))
            .ReturnsAsync(TimeSpan.FromMinutes(10));

        // Setup queue to return files one by one, then empty
        var queueSetupSequence = _mockQueueManager.SetupSequence(q => q.Count);
        for (int i = files.Count; i >= 0; i--)
        {
            queueSetupSequence.Returns(i);
        }

        _mockQueueManager.SetupSequence(q => q.DequeueAsync())
            .ReturnsAsync(files[0])
            .ReturnsAsync(files[1])
            .ReturnsAsync(files[2]);

        var conversionDelay = new TaskCompletionSource<ConversionResult>();
        _mockVideoConverter.Setup(v => v.ConvertAsync(
            It.IsAny<VideoFile>(), 
            It.IsAny<string>(), 
            It.IsAny<TimeSpan>(), 
            It.IsAny<ConversionOptions>(),
            It.IsAny<IProgress<ConversionProgress>>()))
            .Returns(conversionDelay.Task);

        // Act
        var runTask = _jobRunner.RunAsync(directoryPath, options);
        
        // Allow some processing time
        await Task.Delay(100);
        
        // Complete conversions
        conversionDelay.SetResult(new ConversionResult { Success = true });
        
        // Assert - This test primarily ensures no deadlocks occur with parallel job limits
        // Complete the run
        await runTask;
        
        _mockVideoConverter.Verify(v => v.ConvertAsync(
            It.IsAny<VideoFile>(),
            It.IsAny<string>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<ConversionOptions>(),
            It.IsAny<IProgress<ConversionProgress>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_EmptyDirectory_ReturnsEmptyResults()
    {
        // Arrange
        var directoryPath = "/test/empty";
        var options = new ConversionOptions();
        
        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(new List<VideoFile>());

        // Act
        var results = await _jobRunner.RunAsync(directoryPath, options);

        // Assert
        results.Should().BeEmpty();
        _mockQueueManager.Verify(q => q.EnqueueRange(It.Is<IEnumerable<VideoFile>>(
            files => !files.Any())), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ProgressReporting_IncludesFileCount()
    {
        // Arrange
        var directoryPath = "/test/directory";
        var options = new ConversionOptions();
        var progressReports = new List<ConversionProgress>();
        var progress = new Progress<ConversionProgress>(p => progressReports.Add(p));

        var files = new List<VideoFile>
        {
            new() { FilePath = "/test/video1.mp4", FileSize = 1000 },
            new() { FilePath = "/test/video2.mp4", FileSize = 2000 }
        };

        _mockFileFinder.Setup(f => f.FindFilesAsync(directoryPath))
            .ReturnsAsync(files);
        _mockVideoInspector.Setup(v => v.GetVideoCodecAsync(It.IsAny<string>()))
            .ReturnsAsync("h264");

        _mockQueueManager.Setup(q => q.Count).Returns(0);

        // Act
        await _jobRunner.RunAsync(directoryPath, options, progress);

        // Assert
        var inspectionProgress = progressReports.Where(p => p.TotalFiles > 0).ToList();
        inspectionProgress.Should().NotBeEmpty();
        inspectionProgress.All(p => p.TotalFiles == 2).Should().BeTrue();
    }
}