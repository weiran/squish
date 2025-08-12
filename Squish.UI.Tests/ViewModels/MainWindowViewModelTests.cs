using FluentAssertions;
using Moq;
using Squish.Core.Abstractions;
using Squish.Core.Model;
using Squish.Core.Services;
using Squish.UI.Services;
using Squish.UI.ViewModels;
using System.IO;
using Xunit;

namespace Squish.UI.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly Mock<IJobRunner> _mockJobRunner;
    private readonly InMemoryLogger _logger;
    private readonly Mock<IFolderPickerService> _mockFolderPickerService;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _mockJobRunner = new Mock<IJobRunner>();
        _logger = new InMemoryLogger();
        _mockFolderPickerService = new Mock<IFolderPickerService>();

        _viewModel = new MainWindowViewModel(
            _mockJobRunner.Object,
            _logger,
            _mockFolderPickerService.Object);
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Assert
        _viewModel.UseGpu.Should().BeTrue();
        _viewModel.ParallelJobs.Should().Be(Environment.ProcessorCount);
        _viewModel.PreserveTimestamps.Should().BeTrue();
        _viewModel.ListOnly.Should().BeFalse();
        _viewModel.IsProcessing.Should().BeFalse();
        _viewModel.OverallProgress.Should().Be(0);
        _viewModel.StatusText.Should().Be("Ready to process video files");
        _viewModel.FileProgress.Should().BeEmpty();
        _viewModel.ResultsText.Should().BeEmpty();
        _viewModel.ShowResults.Should().BeFalse();
    }

    [Fact]
    public async Task SelectInputFolderAsync_UpdatesInputFolder_WhenFolderSelected()
    {
        // Arrange
        var selectedFolder = "/test/input";
        _mockFolderPickerService
            .Setup(f => f.PickFolderAsync("Select Input Folder"))
            .ReturnsAsync(selectedFolder);

        // Act
        await _viewModel.SelectInputFolderCommand.ExecuteAsync(null);

        // Assert
        _viewModel.InputFolder.Should().Be(selectedFolder);
    }

    [Fact]
    public async Task SelectInputFolderAsync_DoesNotUpdateInputFolder_WhenNoFolderSelected()
    {
        // Arrange
        var originalFolder = _viewModel.InputFolder;
        _mockFolderPickerService
            .Setup(f => f.PickFolderAsync("Select Input Folder"))
            .ReturnsAsync(string.Empty);

        // Act
        await _viewModel.SelectInputFolderCommand.ExecuteAsync(null);

        // Assert
        _viewModel.InputFolder.Should().Be(originalFolder);
    }

    [Fact]
    public async Task SelectOutputFolderAsync_UpdatesOutputFolder_WhenFolderSelected()
    {
        // Arrange
        var selectedFolder = "/test/output";
        _mockFolderPickerService
            .Setup(f => f.PickFolderAsync("Select Output Folder"))
            .ReturnsAsync(selectedFolder);

        // Act
        await _viewModel.SelectOutputFolderCommand.ExecuteAsync(null);

        // Assert
        _viewModel.OutputFolder.Should().Be(selectedFolder);
    }

    [Fact]
    public void ClearOutputFolder_SetsOutputFolderToNull()
    {
        // Arrange
        _viewModel.OutputFolder = "/test/output";

        // Act
        _viewModel.ClearOutputFolderCommand.Execute(null);

        // Assert
        _viewModel.OutputFolder.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Test all settable properties
        _viewModel.UseGpu = false;
        _viewModel.UseGpu.Should().BeFalse();

        _viewModel.ParallelJobs = 4;
        _viewModel.ParallelJobs.Should().Be(4);

        _viewModel.FileLimit = 10;
        _viewModel.FileLimit.Should().Be(10);

        _viewModel.ListOnly = true;
        _viewModel.ListOnly.Should().BeTrue();

        _viewModel.PreserveTimestamps = false;
        _viewModel.PreserveTimestamps.Should().BeFalse();

        _viewModel.InputFolder = "/test/input";
        _viewModel.InputFolder.Should().Be("/test/input");

        _viewModel.OutputFolder = "/test/output";
        _viewModel.OutputFolder.Should().Be("/test/output");
    }

    [Fact]
    public async Task StartProcessingAsync_ShowsError_WhenInputFolderIsNull()
    {
        // Arrange
        _viewModel.InputFolder = null;

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.StatusText.Should().Be("❌ Please select a valid input folder");
        _viewModel.IsProcessing.Should().BeFalse();
        
        _mockJobRunner.Verify(j => j.RunAsync(
            It.IsAny<string>(),
            It.IsAny<ConversionOptions>(),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartProcessingAsync_ShowsError_WhenInputFolderIsEmpty()
    {
        // Arrange
        _viewModel.InputFolder = "";

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.StatusText.Should().Be("❌ Please select a valid input folder");
        _viewModel.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public async Task StartProcessingAsync_SetsProcessingState_WhenStarting()
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        _viewModel.InputFolder = inputFolder;

        var tcs = new TaskCompletionSource<IEnumerable<ConversionResult>>();
        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act
        var task = _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert - During processing
        _viewModel.IsProcessing.Should().BeTrue();
        _viewModel.StatusText.Should().Be("🚀 Starting...");
        _viewModel.OverallProgress.Should().Be(0);
        _viewModel.FileProgress.Should().BeEmpty();
        _viewModel.ResultsText.Should().BeEmpty();
        _viewModel.ShowResults.Should().BeFalse();

        // Complete the task
        tcs.SetResult(new List<ConversionResult>());
        await task;

        // Assert - After processing
        _viewModel.IsProcessing.Should().BeFalse();
        
        CleanupTempDirectory(inputFolder);
    }

    [Fact]
    public async Task StartProcessingAsync_PassesCorrectOptions_ToJobRunner()
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        var outputFolder = CreateTempDirectory();
        
        _viewModel.InputFolder = inputFolder;
        _viewModel.OutputFolder = outputFolder;
        _viewModel.UseGpu = false;
        _viewModel.ParallelJobs = 8;
        _viewModel.FileLimit = 10;
        _viewModel.ListOnly = true;
        _viewModel.PreserveTimestamps = false;

        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConversionResult>());

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _mockJobRunner.Verify(j => j.RunAsync(
            inputFolder,
            It.Is<ConversionOptions>(opts =>
                opts.UseGpu == false &&
                opts.ParallelJobs == 8 &&
                opts.Limit == 10 &&
                opts.ListOnly == true &&
                opts.OutputFolder == outputFolder &&
                opts.PreserveTimestamps == false),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        CleanupTempDirectory(inputFolder);
        CleanupTempDirectory(outputFolder);
    }

    [Fact]
    public async Task StartProcessingAsync_HandlesJobRunnerException()
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        _viewModel.InputFolder = inputFolder;

        var exception = new InvalidOperationException("Test error");
        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.IsProcessing.Should().BeFalse();
        _viewModel.StatusText.Should().Be("❌ Processing failed: Test error");
        _viewModel.ShowResults.Should().BeTrue();
        _viewModel.ResultsText.Should().Contain("❌ Error: Test error");
        _viewModel.ResultsText.Should().Contain("Stack trace:");

        CleanupTempDirectory(inputFolder);
    }

    [Theory]
    [InlineData(0, 0, "ℹ️ No files found that need conversion")]
    [InlineData(1, 0, "🎉 Successfully converted 1 file!")]
    [InlineData(2, 0, "🎉 Successfully converted 2 files!")]
    [InlineData(0, 1, "❌ All 1 file failed to convert")]
    [InlineData(0, 2, "❌ All 2 files failed to convert")]
    [InlineData(2, 1, "🎉 Successfully converted 2 files!")]
    public async Task StartProcessingAsync_DisplaysCorrectStatusMessage(int successCount, int failCount, string expectedStatus)
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        _viewModel.InputFolder = inputFolder;

        var results = new List<ConversionResult>();
        
        for (int i = 0; i < successCount; i++)
        {
            results.Add(new ConversionResult
            {
                FilePath = $"/test/success{i}.mp4",
                Success = true,
                OriginalSize = 1000,
                NewSize = 800
            });
        }
        
        for (int i = 0; i < failCount; i++)
        {
            results.Add(new ConversionResult
            {
                FilePath = $"/test/fail{i}.mp4",
                Success = false,
                ErrorMessage = "Test error"
            });
        }

        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.StatusText.Should().Be(expectedStatus);

        CleanupTempDirectory(inputFolder);
    }

    [Fact]
    public async Task StartProcessingAsync_DisplaysListOnlyResults()
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        _viewModel.InputFolder = inputFolder;
        _viewModel.ListOnly = true;

        var results = new List<ConversionResult>
        {
            new() { FilePath = "/test/video1.mp4", Success = true, OriginalSize = 1000000 },
            new() { FilePath = "/test/video2.mp4", Success = true, OriginalSize = 2000000 }
        };

        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.StatusText.Should().Be("📄 Found 2 files that need conversion");
        _viewModel.ShowResults.Should().BeTrue();
        _viewModel.ResultsText.Should().Contain("📄 Found 2 files that need conversion:");
        _viewModel.ResultsText.Should().Contain("video1.mp4");
        _viewModel.ResultsText.Should().Contain("video2.mp4");

        CleanupTempDirectory(inputFolder);
    }

    [Fact]
    public async Task StartProcessingAsync_DisplaysSpaceSavingsForSuccessfulConversions()
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        _viewModel.InputFolder = inputFolder;

        var results = new List<ConversionResult>
        {
            new()
            {
                FilePath = "/test/video1.mp4",
                Success = true,
                OriginalSize = 1000000,
                NewSize = 800000
            },
            new()
            {
                FilePath = "/test/video2.mp4",
                Success = true,
                OriginalSize = 2000000,
                NewSize = 1600000
            }
        };

        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ResultsText.Should().Contain("✅ Successful: 2");
        _viewModel.ResultsText.Should().Contain("💾 Space saved:");
        _viewModel.ResultsText.Should().MatchRegex(@"\d+\.\d+ KB"); // Check for space saved in KB format
        _viewModel.ResultsText.Should().Contain("20.0%"); // 20% savings
        _viewModel.ResultsText.Should().Contain("📊 Total size:");

        CleanupTempDirectory(inputFolder);
    }

    [Fact]
    public async Task StartProcessingAsync_DisplaysLoggerWarningsAndErrors()
    {
        // Arrange
        var inputFolder = CreateTempDirectory();
        _viewModel.InputFolder = inputFolder;

        _logger.LogWarning("Test warning");
        _logger.LogError("Test error");

        _mockJobRunner
            .Setup(j => j.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConversionResult>());

        // Act
        await _viewModel.StartProcessingCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ResultsText.Should().Contain("⚠️ Warning: Test warning");
        _viewModel.ResultsText.Should().Contain("❌ Error: Test error");

        CleanupTempDirectory(inputFolder);
    }

    private string CreateTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    private void CleanupTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}