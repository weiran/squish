using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Squish.Core.Abstractions;
using Squish.Core.Exceptions;
using Squish.Core.Model;

namespace Squish.Core.Services;

public class VideoConverter : IVideoConverter
{
    private readonly IProcessWrapper _processWrapper;
    private static readonly Regex ProgressRegex = new(@"time=(\d{2}):(\d{2}):(\d{2}\.\d{2})", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new(@"speed=\s*(\d+(?:\.\d+)?)x", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})", RegexOptions.Compiled);
    
    // Encoder constants
    private const string ENCODER_VIDEOTOOLBOX = "hevc_videotoolbox";  // Apple VideoToolbox HEVC encoder
    private const string ENCODER_NVENC = "hevc_nvenc";                // NVIDIA NVENC HEVC encoder
    private const string ENCODER_SOFTWARE_X265 = "libx265";           // Software x265 encoder
    
    // CRF (Constant Rate Factor) constants for different encoders
    private const string CRF_VIDEOTOOLBOX = "50";  // Apple VideoToolbox HEVC encoder
    private const string CRF_NVENC = "40";         // NVIDIA NVENC HEVC encoder
    private const string CRF_SOFTWARE_X265 = "32"; // Software x265 encoder

    public VideoConverter(IProcessWrapper processWrapper)
    {
        _processWrapper = processWrapper ?? throw new ArgumentNullException(nameof(processWrapper));
    }

    public async Task<ConversionResult> ConvertAsync(VideoFile file, ConversionOptions options, IProgress<ConversionProgress> progress)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);
        
        if (string.IsNullOrWhiteSpace(file.FilePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(file));

        var result = new ConversionResult
        {
            FilePath = file.FilePath,
            OriginalSize = file.FileSize
        };

        var startTime = DateTime.UtcNow;
        var fileExtension = Path.GetExtension(file.FilePath);
        var fileWithoutExtension = Path.GetFileNameWithoutExtension(file.FilePath);
        var directory = Path.GetDirectoryName(file.FilePath);
        
        // Determine final output path based on options
        string finalOutputPath;
        if (!string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            // Ensure output directory exists
            Directory.CreateDirectory(options.OutputFolder);
            
            // Preserve relative directory structure if the input is nested
            var relativePath = Path.GetRelativePath(Path.GetDirectoryName(file.FilePath) ?? "", file.FilePath);
            var outputFileName = Path.GetFileName(file.FilePath);
            finalOutputPath = Path.Combine(options.OutputFolder, outputFileName);
        }
        else
        {
            // Default behavior: replace original file
            finalOutputPath = file.FilePath;
        }
        
        var tempOutputPath = Path.Combine(directory!, $"{fileWithoutExtension}.tmp{fileExtension}");

        Process? process = null;
        try
        {
            var ffmpegArgs = BuildFfmpegArguments(file.FilePath, tempOutputPath, options);
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = Process.Start(processStartInfo);
            if (process == null)
                throw new VideoConversionException("Failed to start ffmpeg process");

            var duration = TimeSpan.Zero;
            var errorOutput = new List<string>();
            var hasEncounteredError = false;
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                
                errorOutput.Add(e.Data);
                
                // Check for common ffmpeg error patterns early
                if (e.Data.Contains("Invalid data found when processing input") ||
                    e.Data.Contains("No such file or directory") ||
                    e.Data.Contains("Permission denied") ||
                    e.Data.Contains("Unable to choose an output format") ||
                    e.Data.Contains("Conversion failed"))
                {
                    hasEncounteredError = true;
                }
                
                if (duration == TimeSpan.Zero)
                {
                    var durationMatch = DurationRegex.Match(e.Data);
                    if (durationMatch.Success)
                    {
                        duration = ParseTimeSpan(durationMatch.Groups[1].Value, durationMatch.Groups[2].Value, durationMatch.Groups[3].Value);
                    }
                }

                var progressMatch = ProgressRegex.Match(e.Data);
                if (progressMatch.Success && duration > TimeSpan.Zero && !hasEncounteredError)
                {
                    var currentTime = ParseTimeSpan(progressMatch.Groups[1].Value, progressMatch.Groups[2].Value, progressMatch.Groups[3].Value);
                    var percentage = (currentTime.TotalSeconds / duration.TotalSeconds) * 100;
                    
                    var speedMatch = SpeedRegex.Match(e.Data);
                    var speed = speedMatch.Success ? $"{speedMatch.Groups[1].Value}x" : "0x";

                    progress?.Report(new ConversionProgress
                    {
                        Percentage = Math.Min(percentage, 100),
                        Speed = speed,
                        CurrentFile = Path.GetFileName(file.FilePath)
                    });
                }
            };

            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || hasEncounteredError)
            {
                var errorMessage = string.Join(Environment.NewLine, errorOutput.TakeLast(5));
                throw new VideoConversionException($"ffmpeg failed with exit code {process.ExitCode}: {errorMessage}");
            }

            // Verify temporary file was created and has content
            if (!File.Exists(tempOutputPath))
                throw new VideoConversionException("ffmpeg did not create output file");

            var tempFileInfo = new FileInfo(tempOutputPath);
            if (tempFileInfo.Length == 0)
                throw new VideoConversionException("ffmpeg created empty output file");

            File.Move(tempOutputPath, finalOutputPath, true);

            var newFileInfo = new FileInfo(finalOutputPath);
            result.NewSize = newFileInfo.Length;
            result.OutputPath = finalOutputPath;
            result.Success = true;
        }
        catch (Exception ex)
        {
            // Clean up temporary file if it exists
            if (File.Exists(tempOutputPath))
            {
                try
                {
                    File.Delete(tempOutputPath);
                }
                catch (Exception cleanupEx)
                {
                    // Log cleanup failure but don't override the original exception
                    Console.WriteLine($"Warning: Failed to delete temporary file {tempOutputPath}: {cleanupEx.Message}");
                }
            }

            result.Success = false;
            result.ErrorMessage = ex.Message;

            if (ex is not VideoConversionException)
                throw new VideoConversionException($"Conversion failed for {file.FilePath}", ex);
            
            throw;
        }
        finally
        {
            // Ensure process is properly disposed
            try
            {
                process?.Dispose();
            }
            catch (Exception disposeEx)
            {
                Console.WriteLine($"Warning: Failed to dispose ffmpeg process: {disposeEx.Message}");
            }

            result.Duration = DateTime.UtcNow - startTime;
            progress?.Report(new ConversionProgress
            {
                Percentage = result.Success ? 100 : 0,
                Speed = "0x",
                CurrentFile = Path.GetFileName(file.FilePath)
            });
        }

        return result;
    }

    private static string BuildFfmpegArguments(string inputPath, string outputPath, ConversionOptions options)
    {
        var args = new List<string>
        {
            "-i", $"\"{inputPath}\"",
            "-c:a", "copy"
        };

        string encoder;
        string crfValue;
        
        if (options.UseGpu)
        {
            var gpuEncoder = GetGpuEncoder();
            if (!string.IsNullOrEmpty(gpuEncoder))
            {
                encoder = gpuEncoder;
                // Hardware encoders typically need higher CRF values for same compression
                crfValue = gpuEncoder.Contains("videotoolbox") ? CRF_VIDEOTOOLBOX : CRF_NVENC;
            }
            else
            {
                encoder = ENCODER_SOFTWARE_X265;
                crfValue = CRF_SOFTWARE_X265;
            }
        }
        else
        {
            encoder = ENCODER_SOFTWARE_X265;
            crfValue = CRF_SOFTWARE_X265;
        }

        args.AddRange(["-c:v", encoder]);
        args.AddRange(["-crf", crfValue]);
        
        // Add preset if not using hardware encoder
        if (!encoder.Contains("videotoolbox") && !encoder.Contains("nvenc"))
        {
            args.Add("-preset");
            args.Add("medium");
        }
        
        args.AddRange(["-y", $"\"{outputPath}\""]);

        return string.Join(" ", args);
    }

    private static string? GetGpuEncoder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ENCODER_VIDEOTOOLBOX;
        }

        return HasNvidiaGpu() ? ENCODER_NVENC : null;
    }

    private static bool HasNvidiaGpu()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            return process?.WaitForExit(5000) == true && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static TimeSpan ParseTimeSpan(string hours, string minutes, string seconds)
    {
        return new TimeSpan(
            int.Parse(hours),
            int.Parse(minutes),
            (int)double.Parse(seconds, CultureInfo.InvariantCulture),
            (int)((double.Parse(seconds, CultureInfo.InvariantCulture) % 1) * 1000)
        );
    }
}