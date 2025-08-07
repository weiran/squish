using FluentAssertions;
using Squish.Core.Exceptions;
using Xunit;

namespace Squish.Core.Tests.Exceptions;

public class VideoConversionExceptionTests
{
    [Fact]
    public void VideoConversionException_CanBeCreatedWithDefaultConstructor()
    {
        var exception = new VideoConversionException();

        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VideoConversionException_CanBeCreatedWithMessage()
    {
        var message = "Test error message";
        var exception = new VideoConversionException(message);

        exception.Message.Should().Be(message);
    }

    [Fact]
    public void VideoConversionException_CanBeCreatedWithMessageAndInnerException()
    {
        var message = "Test error message";
        var innerException = new InvalidOperationException("Inner exception");
        var exception = new VideoConversionException(message, innerException);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void VideoConversionException_InheritsFromException()
    {
        var exception = new VideoConversionException();

        exception.Should().BeAssignableTo<Exception>();
    }
}