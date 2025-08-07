using FluentAssertions;
using Moq;
using Squish.Core.Abstractions;
using Squish.Core.Services;
using Xunit;

namespace Squish.Core.Tests.Services;

public class FileFinderTests
{
    private readonly Mock<IFileSystemWrapper> _mockFileSystem;
    private readonly FileFinder _fileFinder;

    public FileFinderTests()
    {
        _mockFileSystem = new Mock<IFileSystemWrapper>();
        _fileFinder = new FileFinder(_mockFileSystem.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFileSystemWrapperIsNull()
    {
        var act = () => new FileFinder(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileSystemWrapper");
    }

    [Fact]
    public async Task FindFilesAsync_ThrowsArgumentException_WhenDirectoryPathIsNull()
    {
        var act = async () => await _fileFinder.FindFilesAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("directoryPath");
    }

    [Fact]
    public async Task FindFilesAsync_ThrowsArgumentException_WhenDirectoryPathIsEmpty()
    {
        var act = async () => await _fileFinder.FindFilesAsync("");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("directoryPath");
    }

    [Fact]
    public async Task FindFilesAsync_ThrowsArgumentException_WhenDirectoryPathIsWhitespace()
    {
        var act = async () => await _fileFinder.FindFilesAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("directoryPath");
    }

    [Fact]
    public async Task FindFilesAsync_ThrowsDirectoryNotFoundException_WhenDirectoryDoesNotExist()
    {
        var directoryPath = "/nonexistent/path";
        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(false);

        var act = async () => await _fileFinder.FindFilesAsync(directoryPath);

        await act.Should().ThrowAsync<DirectoryNotFoundException>()
            .WithMessage($"Directory not found: {directoryPath}");
    }

    [Fact]
    public async Task FindFilesAsync_ReturnsEmptyCollection_WhenNoVideoFilesFound()
    {
        var directoryPath = "/test/path";
        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(true);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, It.IsAny<string>(), SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        var result = await _fileFinder.FindFilesAsync(directoryPath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindFilesAsync_ReturnsVideoFiles_WhenVideoFilesFound()
    {
        var directoryPath = "/test/path";
        var mp4Files = new[] { "/test/path/video1.mp4", "/test/path/video2.mp4" };
        var mkvFiles = new[] { "/test/path/video3.mkv" };

        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(true);
        
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, "*.mp4", SearchOption.AllDirectories))
            .Returns(mp4Files);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, "*.mkv", SearchOption.AllDirectories))
            .Returns(mkvFiles);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, It.IsRegex(@"\*\.(avi|mov|wmv|flv|webm|m4v)"), SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/video1.mp4")).Returns(1000);
        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/video2.mp4")).Returns(2000);
        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/video3.mkv")).Returns(1500);

        var result = await _fileFinder.FindFilesAsync(directoryPath);

        result.Should().HaveCount(3);
        result.Should().Contain(v => v.FilePath == "/test/path/video1.mp4" && v.FileSize == 1000);
        result.Should().Contain(v => v.FilePath == "/test/path/video2.mp4" && v.FileSize == 2000);
        result.Should().Contain(v => v.FilePath == "/test/path/video3.mkv" && v.FileSize == 1500);
    }

    [Fact]
    public async Task FindFilesAsync_ReturnsFilesOrderedBySize_LargestFirst()
    {
        var directoryPath = "/test/path";
        var files = new[] { "/test/path/small.mp4", "/test/path/large.mp4", "/test/path/medium.mp4" };

        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(true);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, "*.mp4", SearchOption.AllDirectories))
            .Returns(files);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, It.IsRegex(@"\*\.(mkv|avi|mov|wmv|flv|webm|m4v)"), SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/small.mp4")).Returns(500);
        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/large.mp4")).Returns(2000);
        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/medium.mp4")).Returns(1000);

        var result = await _fileFinder.FindFilesAsync(directoryPath);

        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList[0].FilePath.Should().Be("/test/path/large.mp4");
        resultList[0].FileSize.Should().Be(2000);
        resultList[1].FilePath.Should().Be("/test/path/medium.mp4");
        resultList[1].FileSize.Should().Be(1000);
        resultList[2].FilePath.Should().Be("/test/path/small.mp4");
        resultList[2].FileSize.Should().Be(500);
    }

    [Fact]
    public async Task FindFilesAsync_SkipsInaccessibleFiles_WhenFileNotFoundExceptionThrown()
    {
        var directoryPath = "/test/path";
        var files = new[] { "/test/path/accessible.mp4", "/test/path/inaccessible.mp4" };

        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(true);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, "*.mp4", SearchOption.AllDirectories))
            .Returns(files);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, It.IsRegex(@"\*\.(mkv|avi|mov|wmv|flv|webm|m4v)"), SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/accessible.mp4")).Returns(1000);
        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/inaccessible.mp4"))
            .Throws<FileNotFoundException>();

        var result = await _fileFinder.FindFilesAsync(directoryPath);

        result.Should().HaveCount(1);
        result.First().FilePath.Should().Be("/test/path/accessible.mp4");
    }

    [Fact]
    public async Task FindFilesAsync_SkipsInaccessibleFiles_WhenUnauthorizedAccessExceptionThrown()
    {
        var directoryPath = "/test/path";
        var files = new[] { "/test/path/accessible.mp4", "/test/path/unauthorized.mp4" };

        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(true);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, "*.mp4", SearchOption.AllDirectories))
            .Returns(files);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, It.IsRegex(@"\*\.(mkv|avi|mov|wmv|flv|webm|m4v)"), SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/accessible.mp4")).Returns(1000);
        _mockFileSystem.Setup(x => x.GetFileSize("/test/path/unauthorized.mp4"))
            .Throws<UnauthorizedAccessException>();

        var result = await _fileFinder.FindFilesAsync(directoryPath);

        result.Should().HaveCount(1);
        result.First().FilePath.Should().Be("/test/path/accessible.mp4");
    }

    [Fact]
    public async Task FindFilesAsync_SearchesAllVideoExtensions()
    {
        var directoryPath = "/test/path";
        var expectedExtensions = new[] { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" };

        _mockFileSystem.Setup(x => x.DirectoryExists(directoryPath)).Returns(true);
        _mockFileSystem.Setup(x => x.EnumerateFiles(directoryPath, It.IsAny<string>(), SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        await _fileFinder.FindFilesAsync(directoryPath);

        foreach (var extension in expectedExtensions)
        {
            _mockFileSystem.Verify(x => x.EnumerateFiles(directoryPath, $"*{extension}", SearchOption.AllDirectories), Times.Once);
        }
    }
}