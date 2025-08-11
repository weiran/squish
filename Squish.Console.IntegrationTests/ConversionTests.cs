using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Squish.Console.IntegrationTests;

public class ConversionTests : IDisposable
{
    private readonly string _outputDirectory;

    public ConversionTests()
    {
        _outputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/output");
        Directory.CreateDirectory(_outputDirectory);
    }

    private static string GetSolutionDirectory()
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var solutionDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, "../../../../"));
        return solutionDirectory;
    }

    private static string GetExecutablePath()
    {
        var solutionDirectory = GetSolutionDirectory();
        return Path.Combine(solutionDirectory, "Squish.Console/bin/Debug/net9.0/squish");
    }

    [Fact]
    public void DefaultConversion_CreatesOutputFiles()
    {
        // Arrange
        var inputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/originals");
        var expectedOutputFile1 = Path.Combine(_outputDirectory, "h264-sample.mp4");
        var expectedOutputFile2 = Path.Combine(_outputDirectory, "h264-sample-2.mp4");

        // Act
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(inputDirectory);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(_outputDirectory);
        process.Start();
        process.WaitForExit();

        // Assert
        Assert.True(File.Exists(expectedOutputFile1));
        Assert.True(File.Exists(expectedOutputFile2));
    }

    [Fact]
    public void CpuOnly_CreatesOutputFiles()
    {
        // Arrange
        var inputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/originals");
        var expectedOutputFile1 = Path.Combine(_outputDirectory, "h264-sample.mp4");
        var expectedOutputFile2 = Path.Combine(_outputDirectory, "h264-sample-2.mp4");

        // Act
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(inputDirectory);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(_outputDirectory);
        process.StartInfo.ArgumentList.Add("--cpu-only");
        process.Start();
        process.WaitForExit();

        // Assert
        Assert.True(File.Exists(expectedOutputFile1));
        Assert.True(File.Exists(expectedOutputFile2));
    }

    [Fact]
    public void Limit_CreatesOneOutputFile()
    {
        // Arrange
        var inputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/originals");

        // Act
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(inputDirectory);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(_outputDirectory);
        process.StartInfo.ArgumentList.Add("--limit");
        process.StartInfo.ArgumentList.Add("1");
        process.Start();
        process.WaitForExit();

        // Assert
        var files = Directory.GetFiles(_outputDirectory);
        Assert.Single(files);
    }

    [Fact]
    public void ListOnly_DoesNotCreateOutputFiles()
    {
        // Arrange
        var inputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/originals");

        // Act
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(inputDirectory);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(_outputDirectory);
        process.StartInfo.ArgumentList.Add("--list-only");
        process.Start();
        process.WaitForExit();

        // Assert
        var files = Directory.GetFiles(_outputDirectory);
        Assert.Empty(files);
    }

    [Fact]
    public void PreservesTimestamps_ByDefault()
    {
        // Arrange
        var inputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/originals");
        var originalFile = Path.Combine(inputDirectory, "h264-sample.mp4");
        var originalFileInfo = new FileInfo(originalFile);
        
        // Modify original file timestamps to test values
        var testCreationTime = new DateTime(2022, 1, 1, 12, 0, 0);
        var testWriteTime = new DateTime(2022, 1, 2, 12, 0, 0);
        File.SetCreationTime(originalFile, testCreationTime);
        File.SetLastWriteTime(originalFile, testWriteTime);

        // Act
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(_outputDirectory);
        process.StartInfo.ArgumentList.Add(inputDirectory);

        process.Start();
        process.WaitForExit();

        // Assert
        Assert.Equal(0, process.ExitCode);
        var outputFile = Path.Combine(_outputDirectory, "h264-sample.mp4");
        Assert.True(File.Exists(outputFile));
        
        var outputFileInfo = new FileInfo(outputFile);
        Assert.Equal(testCreationTime, outputFileInfo.CreationTime);
        Assert.Equal(testWriteTime, outputFileInfo.LastWriteTime);
    }

    [Fact]
    public void UsesCurrentTimestamps_WhenRequested()
    {
        // Arrange
        var inputDirectory = Path.Combine(GetSolutionDirectory(), "Squish.Console.IntegrationTests/TestAssets/originals");
        var originalFile = Path.Combine(inputDirectory, "h264-sample.mp4");
        
        // Modify original file timestamps to old values
        var oldCreationTime = new DateTime(2020, 1, 1, 12, 0, 0);
        var oldWriteTime = new DateTime(2020, 1, 2, 12, 0, 0);
        File.SetCreationTime(originalFile, oldCreationTime);
        File.SetLastWriteTime(originalFile, oldWriteTime);
        
        var beforeConversion = DateTime.Now;

        // Act
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(_outputDirectory);
        process.StartInfo.ArgumentList.Add("--use-current-timestamps");
        process.StartInfo.ArgumentList.Add(inputDirectory);

        process.Start();
        process.WaitForExit();
        
        var afterConversion = DateTime.Now;

        // Assert
        Assert.Equal(0, process.ExitCode);
        var outputFile = Path.Combine(_outputDirectory, "h264-sample.mp4");
        Assert.True(File.Exists(outputFile));
        
        var outputFileInfo = new FileInfo(outputFile);
        
        // The timestamps should be current (between before and after conversion)
        Assert.True(outputFileInfo.CreationTime >= beforeConversion && outputFileInfo.CreationTime <= afterConversion);
        Assert.True(outputFileInfo.LastWriteTime >= beforeConversion && outputFileInfo.LastWriteTime <= afterConversion);
        
        // Should NOT match the old original timestamps
        Assert.NotEqual(oldCreationTime, outputFileInfo.CreationTime);
        Assert.NotEqual(oldWriteTime, outputFileInfo.LastWriteTime);
    }

    public void Dispose()
    {
        var files = Directory.GetFiles(_outputDirectory);
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }
}