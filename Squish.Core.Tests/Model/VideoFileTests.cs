using FluentAssertions;
using Squish.Core.Model;
using Xunit;

namespace Squish.Core.Tests.Model;

public class VideoFileTests
{
    [Fact]
    public void VideoFile_CanBeCreated_WithRequiredProperties()
    {
        var filePath = "/path/to/video.mp4";
        var fileSize = 1024L;
        var codec = "h264";

        var videoFile = new VideoFile
        {
            FilePath = filePath,
            FileSize = fileSize,
            Codec = codec
        };

        videoFile.FilePath.Should().Be(filePath);
        videoFile.FileSize.Should().Be(fileSize);
        videoFile.Codec.Should().Be(codec);
    }

    [Fact]
    public void VideoFile_CodecCanBeNull()
    {
        var videoFile = new VideoFile
        {
            FilePath = "/path/to/video.mp4",
            FileSize = 1024L,
            Codec = null
        };

        videoFile.Codec.Should().BeNull();
    }

    [Fact]
    public void VideoFile_CanBeCreated_WithMinimalProperties()
    {
        var filePath = "/path/to/video.mp4";

        var videoFile = new VideoFile
        {
            FilePath = filePath
        };

        videoFile.FilePath.Should().Be(filePath);
        videoFile.FileSize.Should().Be(0L);
        videoFile.Codec.Should().BeNull();
    }
}