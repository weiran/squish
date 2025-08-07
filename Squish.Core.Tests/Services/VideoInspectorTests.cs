using FluentAssertions;
using Moq;
using Squish.Core.Abstractions;
using Squish.Core.Services;
using Xunit;

namespace Squish.Core.Tests.Services;

public class VideoInspectorTests
{
    private readonly Mock<IProcessWrapper> _mockProcessWrapper;
    private readonly VideoInspector _videoInspector;

    public VideoInspectorTests()
    {
        _mockProcessWrapper = new Mock<IProcessWrapper>();
        _videoInspector = new VideoInspector(_mockProcessWrapper.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenProcessWrapperIsNull()
    {
        var act = () => new VideoInspector(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processWrapper");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ThrowsArgumentException_WhenFilePathIsNull()
    {
        var act = async () => await _videoInspector.GetVideoCodecAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ThrowsArgumentException_WhenFilePathIsEmpty()
    {
        var act = async () => await _videoInspector.GetVideoCodecAsync("");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ThrowsArgumentException_WhenFilePathIsWhitespace()
    {
        var act = async () => await _videoInspector.GetVideoCodecAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ThrowsInvalidOperationException_WhenFfprobeExitCodeIsNonZero()
    {
        var filePath = "/test/video.mp4";
        var processResult = new ProcessResult
        {
            ExitCode = 1,
            StandardError = "ffprobe error"
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var act = async () => await _videoInspector.GetVideoCodecAsync(filePath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ffprobe failed with exit code 1: ffprobe error");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ReturnsCodec_WhenValidJsonWithCodecName()
    {
        var filePath = "/test/video.mp4";
        var jsonOutput = """
        {
            "streams": [
                {
                    "codec_name": "h264",
                    "codec_type": "video"
                }
            ]
        }
        """;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = jsonOutput
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var result = await _videoInspector.GetVideoCodecAsync(filePath);

        result.Should().Be("h264");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ReturnsUnknown_WhenJsonHasNoStreams()
    {
        var filePath = "/test/video.mp4";
        var jsonOutput = """
        {
            "streams": []
        }
        """;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = jsonOutput
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var result = await _videoInspector.GetVideoCodecAsync(filePath);

        result.Should().Be("unknown");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ReturnsUnknown_WhenJsonHasNoStreamsProperty()
    {
        var filePath = "/test/video.mp4";
        var jsonOutput = """
        {
            "format": {}
        }
        """;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = jsonOutput
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var result = await _videoInspector.GetVideoCodecAsync(filePath);

        result.Should().Be("unknown");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ReturnsUnknown_WhenStreamHasNoCodecName()
    {
        var filePath = "/test/video.mp4";
        var jsonOutput = """
        {
            "streams": [
                {
                    "codec_type": "video"
                }
            ]
        }
        """;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = jsonOutput
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var result = await _videoInspector.GetVideoCodecAsync(filePath);

        result.Should().Be("unknown");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ReturnsUnknown_WhenCodecNameIsNull()
    {
        var filePath = "/test/video.mp4";
        var jsonOutput = """
        {
            "streams": [
                {
                    "codec_name": null,
                    "codec_type": "video"
                }
            ]
        }
        """;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = jsonOutput
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var result = await _videoInspector.GetVideoCodecAsync(filePath);

        result.Should().Be("unknown");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ThrowsInvalidOperationException_WhenOutputIsEmpty()
    {
        var filePath = "/test/video.mp4";
        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = ""
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var act = async () => await _videoInspector.GetVideoCodecAsync(filePath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ffprobe output is empty");
    }

    [Fact]
    public async Task GetVideoCodecAsync_ThrowsInvalidOperationException_WhenJsonIsInvalid()
    {
        var filePath = "/test/video.mp4";
        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = "invalid json"
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var act = async () => await _videoInspector.GetVideoCodecAsync(filePath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to parse ffprobe output: *");
    }

    [Fact]
    public async Task GetVideoCodecAsync_CallsProcessWrapperWithCorrectArguments()
    {
        var filePath = "/test/video.mp4";
        var expectedArguments = $"-v quiet -print_format json -show_streams -select_streams v:0 \"{filePath}\"";

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = """
            {
                "streams": [
                    {
                        "codec_name": "h264"
                    }
                ]
            }
            """
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", expectedArguments, default))
            .ReturnsAsync(processResult);

        await _videoInspector.GetVideoCodecAsync(filePath);

        _mockProcessWrapper.Verify(x => x.RunAsync("ffprobe", expectedArguments, default), Times.Once);
    }

    [Theory]
    [InlineData("h264")]
    [InlineData("hevc")]
    [InlineData("av1")]
    [InlineData("vp9")]
    public async Task GetVideoCodecAsync_ReturnsCorrectCodec_ForVariousCodecs(string codecName)
    {
        var filePath = "/test/video.mp4";
        var jsonOutput = $$"""
        {
            "streams": [
                {
                    "codec_name": "{{codecName}}"
                }
            ]
        }
        """;

        var processResult = new ProcessResult
        {
            ExitCode = 0,
            StandardOutput = jsonOutput
        };

        _mockProcessWrapper.Setup(x => x.RunAsync("ffprobe", It.IsAny<string>(), default))
            .ReturnsAsync(processResult);

        var result = await _videoInspector.GetVideoCodecAsync(filePath);

        result.Should().Be(codecName);
    }
}