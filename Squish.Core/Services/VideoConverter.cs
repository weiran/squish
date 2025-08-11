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
    private readonly ILogger _logger;
    private static readonly Regex ProgressRegex = new(@"time=(\d{2}):(\d{2}):(\d{2}\.\d{2})", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new(@"speed=\s*(\d+(?:\.\d+)?)x", RegexOptions.Compiled);
    
    // Encoder constants
    private const string ENCODER_VIDEOTOOLBOX = "hevc_videotoolbox";  // Apple VideoToolbox HEVC encoder
    private const string ENCODER_NVENC = "hevc_nvenc";                // NVIDIA NVENC HEVC encoder
    private const string ENCODER_SOFTWARE_X265 = "libx265";           // Software x265 encoder
    
    // CRF (Constant Rate Factor) constants for different encoders
    private const string CRF_VIDEOTOOLBOX = "50";  // Apple VideoToolbox HEVC encoder
    private const string CRF_NVENC = "40";         // NVIDIA NVENC HEVC encoder
    private const string CRF_SOFTWARE_X265 = "32"; // Software x265 encoder

    public VideoConverter(IProcessWrapper processWrapper, ILogger logger)
    {
        _processWrapper = processWrapper ?? throw new ArgumentNullException(nameof(processWrapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConversionResult> ConvertAsync(VideoFile file, string basePath, TimeSpan duration, ConversionOptions options, IProgress<ConversionProgress> progress)
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

        string finalOutputPath;
        string conversionOutputPath;
        string? tempOutputPath = null; // Can be null if outputting directly

        if (!string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            var relativePath = Path.GetRelativePath(basePath, file.FilePath);
            finalOutputPath = Path.Combine(options.OutputFolder, relativePath);
            
            var finalDirectory = Path.GetDirectoryName(finalOutputPath);
            if (!string.IsNullOrEmpty(finalDirectory)){
                Directory.CreateDirectory(finalDirectory);
            }
            
            conversionOutputPath = finalOutputPath; // Output directly to the final destination
        }
        else
        {
            // Use a temporary file in the original directory
            finalOutputPath = file.FilePath;
            tempOutputPath = Path.Combine(directory!, $"{fileWithoutExtension}.tmp{fileExtension}");
            conversionOutputPath = tempOutputPath;
        }

        Process? process = null;
        try
        {
            var ffmpegArgs = BuildFfmpegArguments(file.FilePath, conversionOutputPath, options);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            foreach (var arg in ffmpegArgs)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            process = Process.Start(processStartInfo);
            if (process == null)
                throw new VideoConversionException("Failed to start ffmpeg process");

            var errorOutput = new List<string>();
            var hasEncounteredError = false;

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                errorOutput.Add(e.Data);

                if (e.Data.Contains("Invalid data found when processing input") ||
                    e.Data.Contains("No such file or directory") ||
                    e.Data.Contains("Permission denied") ||
                    e.Data.Contains("Unable to choose an output format") ||
                    e.Data.Contains("Conversion failed"))
                {
                    hasEncounteredError = true;
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

            // Verify output file was created and has content
            if (!File.Exists(conversionOutputPath))
                throw new VideoConversionException("ffmpeg did not create output file");

            var outputFileInfo = new FileInfo(conversionOutputPath);
            if (outputFileInfo.Length == 0)
                throw new VideoConversionException("ffmpeg created empty output file");

            // If a temporary file was used, move it to the final destination
            if (tempOutputPath != null)
            {
                File.Move(tempOutputPath, finalOutputPath, true);
            }

            var newFileInfo = new FileInfo(finalOutputPath);
            result.NewSize = newFileInfo.Length;
            result.OutputPath = finalOutputPath;
            result.Success = true;

            progress?.Report(new ConversionProgress
            {
                Percentage = 100,
                Speed = "0x",
                CurrentFile = Path.GetFileName(file.FilePath)
            });
        }
        catch (Exception ex)
        {
            // Clean up the output file if an error occurred
            if (File.Exists(conversionOutputPath))
            {
                try
                {
                    File.Delete(conversionOutputPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning($"Failed to delete output file {conversionOutputPath}: {cleanupEx.Message}");
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
                _logger.LogWarning($"Failed to dispose ffmpeg process: {disposeEx.Message}");
            }

            result.Duration = DateTime.UtcNow - startTime;
        }

        return result;
    }

    private static List<string> BuildFfmpegArguments(string inputPath, string outputPath, ConversionOptions options)
    {
        var args = new List<string>
        {
            "-i", inputPath,
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
        
        args.AddRange(["-y", outputPath]);

        return args;
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
        var h = int.Parse(hours);
        var m = int.Parse(minutes);
        var secDouble = double.Parse(seconds, CultureInfo.InvariantCulture);
        var s = (int)secDouble;
        var ms = (int)((secDouble % 1) * 1000);
        
        // Use the correct constructor: TimeSpan(days, hours, minutes, seconds, milliseconds)
        return new TimeSpan(0, h, m, s, ms);
    }
}