using FluentAssertions;
using Moq;
using Squish.Core.Abstractions;
using Squish.Core.Model;
using Squish.Core.Services;
using Xunit;

namespace Squish.Core.Tests.Services;

public class VideoConverterTests
{
    private readonly Mock<IProcessWrapper> _mockProcessWrapper;
    private readonly VideoConverter _videoConverter;

    public VideoConverterTests()
    {
        _mockProcessWrapper = new Mock<IProcessWrapper>();
        _videoConverter = new VideoConverter(_mockProcessWrapper.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenProcessWrapperIsNull()
    {
        var act = () => new VideoConverter(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processWrapper");
    }

    [Fact]
    public async Task ConvertAsync_ThrowsArgumentNullException_WhenVideoFileIsNull()
    {
        var options = new ConversionOptions();
        var progress = new Progress<ConversionProgress>();

        var act = async () => await _videoConverter.ConvertAsync(null!, options, progress);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("file");
    }

    [Fact]
    public async Task ConvertAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        var videoFile = new VideoFile { FilePath = "/test/video.mp4" };
        var progress = new Progress<ConversionProgress>();

        var act = async () => await _videoConverter.ConvertAsync(videoFile, null!, progress);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task ConvertAsync_ThrowsArgumentException_WhenFilePathIsEmpty()
    {
        var videoFile = new VideoFile { FilePath = "" };
        var options = new ConversionOptions();
        var progress = new Progress<ConversionProgress>();

        var act = async () => await _videoConverter.ConvertAsync(videoFile, options, progress);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("file");
    }

    // TODO: VideoConverter needs more extensive refactoring to properly use IProcessWrapper
    // and IFileSystemWrapper for full testability. The current implementation still uses
    // direct Process creation and file system operations internally.

    // Note: Full integration testing of VideoConverter would require complex mocking
    // of file system operations and ffmpeg process behavior. The current tests focus
    // on validation and basic error handling. More comprehensive tests could be added
    // as integration tests or with more sophisticated mocking setup.
}