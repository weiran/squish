using FluentAssertions;
using Squish.Core.Model;
using Xunit;

namespace Squish.Core.Tests.Model;

public class ConversionResultTests
{
    [Fact]
    public void ConversionResult_HasCorrectDefaults()
    {
        var result = new ConversionResult();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.FilePath.Should().Be(string.Empty);
        result.Duration.Should().Be(TimeSpan.Zero);
        result.OriginalSize.Should().Be(0L);
        result.NewSize.Should().Be(0L);
    }

    [Fact]
    public void ConversionResult_CanSetAllProperties()
    {
        var filePath = "/path/to/video.mp4";
        var duration = TimeSpan.FromMinutes(5);
        var originalSize = 1000L;
        var newSize = 800L;
        var errorMessage = "Conversion failed";

        var result = new ConversionResult
        {
            Success = true,
            ErrorMessage = errorMessage,
            FilePath = filePath,
            Duration = duration,
            OriginalSize = originalSize,
            NewSize = newSize
        };

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().Be(errorMessage);
        result.FilePath.Should().Be(filePath);
        result.Duration.Should().Be(duration);
        result.OriginalSize.Should().Be(originalSize);
        result.NewSize.Should().Be(newSize);
    }

    [Fact]
    public void ConversionResult_CanCalculateCompressionRatio()
    {
        var result = new ConversionResult
        {
            OriginalSize = 1000L,
            NewSize = 800L
        };

        var compressionRatio = (double)result.NewSize / result.OriginalSize;
        compressionRatio.Should().Be(0.8);
    }

    [Fact]
    public void ConversionResult_CanCalculateSpaceSaved()
    {
        var result = new ConversionResult
        {
            OriginalSize = 1000L,
            NewSize = 800L
        };

        var spaceSaved = result.OriginalSize - result.NewSize;
        spaceSaved.Should().Be(200L);
    }
}