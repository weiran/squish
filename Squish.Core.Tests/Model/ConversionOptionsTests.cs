using FluentAssertions;
using Squish.Core.Model;
using Xunit;

namespace Squish.Core.Tests.Model;

public class ConversionOptionsTests
{
    [Fact]
    public void ConversionOptions_HasCorrectDefaults()
    {
        var options = new ConversionOptions();

        options.UseGpu.Should().BeTrue();
        options.ParallelJobs.Should().Be(Environment.ProcessorCount);
        options.Limit.Should().BeNull();
        options.ListOnly.Should().BeFalse();
    }

    [Fact]
    public void ConversionOptions_CanOverrideDefaults()
    {
        var options = new ConversionOptions
        {
            UseGpu = false,
            ParallelJobs = 4,
            Limit = 10,
            ListOnly = true
        };

        options.UseGpu.Should().BeFalse();
        options.ParallelJobs.Should().Be(4);
        options.Limit.Should().Be(10);
        options.ListOnly.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void ConversionOptions_CanSetValidParallelJobs(int jobCount)
    {
        var options = new ConversionOptions { ParallelJobs = jobCount };
        
        options.ParallelJobs.Should().Be(jobCount);
    }
}