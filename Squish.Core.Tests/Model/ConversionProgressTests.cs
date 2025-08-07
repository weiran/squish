using FluentAssertions;
using Squish.Core.Model;
using Xunit;

namespace Squish.Core.Tests.Model;

public class ConversionProgressTests
{
    [Fact]
    public void ConversionProgress_HasCorrectDefaults()
    {
        var progress = new ConversionProgress();

        progress.Percentage.Should().Be(0.0);
        progress.Speed.Should().Be(string.Empty);
        progress.CurrentFile.Should().Be(string.Empty);
    }

    [Fact]
    public void ConversionProgress_CanSetAllProperties()
    {
        var progress = new ConversionProgress
        {
            Percentage = 75.5,
            Speed = "1.2x",
            CurrentFile = "video.mp4"
        };

        progress.Percentage.Should().Be(75.5);
        progress.Speed.Should().Be("1.2x");
        progress.CurrentFile.Should().Be("video.mp4");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    public void ConversionProgress_CanSetValidPercentages(double percentage)
    {
        var progress = new ConversionProgress { Percentage = percentage };
        
        progress.Percentage.Should().Be(percentage);
    }
}