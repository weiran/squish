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

    public void Dispose()
    {
        var files = Directory.GetFiles(_outputDirectory);
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }
}