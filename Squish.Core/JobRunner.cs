using Squish.Core.Abstractions;
using Squish.Core.Model;
using Squish.Core.Services;
using System.Collections.Concurrent;

namespace Squish.Core;

// Helper class to track individual file conversion progress
internal class FileProgressTracker
{
    public string FilePath { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string Speed { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class JobRunner
{
    private readonly IFileFinder _fileFinder;
    private readonly IVideoInspector _videoInspector;
    private readonly IVideoConverter _videoConverter;
    private readonly IQueueManager _queueManager;

    public JobRunner(
        IFileFinder fileFinder,
        IVideoInspector videoInspector,
        IVideoConverter videoConverter,
        IQueueManager queueManager)
    {
        _fileFinder = fileFinder;
        _videoInspector = videoInspector;
        _videoConverter = videoConverter;
        _queueManager = queueManager;
    }

    public async Task<IEnumerable<ConversionResult>> RunAsync(
        string directoryPath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Report discovery phase
        progress?.Report(new ConversionProgress
        {
            CurrentFile = "Discovering video files..."
        });

        var allFiles = await _fileFinder.FindFilesAsync(directoryPath);
        var allFilesList = allFiles.ToList();
        
        // Report inspection phase  
        progress?.Report(new ConversionProgress
        {
            TotalFiles = allFilesList.Count,
            CompletedFiles = 0,
            CurrentFile = "Inspecting video codecs..."
        });

        var filesToProcess = new List<VideoFile>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
        var processedCount = 0;

        // Process files in parallel for codec inspection
        var tasks = allFilesList.Select(async file =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var codec = await _videoInspector.GetVideoCodecAsync(file.FilePath);
                file.Codec = codec;

                var completed = Interlocked.Increment(ref processedCount);
                progress?.Report(new ConversionProgress
                {
                    TotalFiles = allFilesList.Count,
                    CompletedFiles = completed,
                    CurrentFile = $"Inspected: {Path.GetFileName(file.FilePath)}"
                });

                return !IsHevcCodec(codec) ? file : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not inspect {file.FilePath}: {ex.Message}");
                Interlocked.Increment(ref processedCount);
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        filesToProcess.AddRange(results.Where(f => f != null)!);

        if (options.Limit.HasValue && options.Limit.Value > 0)
        {
            filesToProcess = filesToProcess.Take(options.Limit.Value).ToList();
        }

        if (options.ListOnly)
        {
            return filesToProcess.Select(f => new ConversionResult
            {
                FilePath = f.FilePath,
                Success = true,
                OriginalSize = f.FileSize
            });
        }

        _queueManager.EnqueueRange(filesToProcess);

        var conversionResults = new List<ConversionResult>();
        var conversionSemaphore = new SemaphoreSlim(options.ParallelJobs, options.ParallelJobs);
        var conversionTasks = new List<Task<ConversionResult>>();
        var totalFiles = filesToProcess.Count;
        
        // Track individual file progress
        var fileProgressTrackers = new ConcurrentDictionary<string, FileProgressTracker>();
        var progressUpdateTimer = new Timer(_ => UpdateOverallProgress(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        
        void UpdateOverallProgress()
        {
            var trackers = fileProgressTrackers.Values.ToList();
            var completedCount = trackers.Count(t => t.IsCompleted);
            var partialProgress = trackers.Where(t => !t.IsCompleted).Sum(t => t.Progress / 100.0);
            
            // Build active conversions dictionary for individual progress bars
            var activeConversions = trackers.Where(t => !t.IsCompleted && t.Progress > 0)
                                          .ToDictionary(t => t.FilePath, t => new FileConversionProgress
                                          {
                                              FilePath = t.FilePath,
                                              FileName = Path.GetFileName(t.FilePath),
                                              Progress = t.Progress,
                                              Speed = t.Speed,
                                              IsActive = true
                                          });
            
            var currentFileDisplay = activeConversions.Any() 
                ? string.Join(", ", activeConversions.Values.Take(2).Select(f => $"{f.FileName} ({f.Progress:F0}%)")) + (activeConversions.Count > 2 ? "..." : "")
                : "Processing...";

            progress?.Report(new ConversionProgress
            {
                TotalFiles = totalFiles,
                CompletedFiles = completedCount,
                PartialProgress = partialProgress,
                CurrentFile = currentFileDisplay,
                ActiveConversions = activeConversions
            });
        }
        
        // Report initial conversion progress
        progress?.Report(new ConversionProgress
        {
            TotalFiles = totalFiles,
            CompletedFiles = 0,
            CurrentFile = "Starting conversion..."
        });

        while (_queueManager.Count > 0 || conversionTasks.Any(t => !t.IsCompleted))
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (_queueManager.Count > 0 && conversionTasks.Count(t => !t.IsCompleted) < options.ParallelJobs)
            {
                var file = await _queueManager.DequeueAsync();
                if (file == null) break;

                var task = ProcessFileAsync(file, directoryPath, options, fileProgressTrackers, conversionSemaphore, cancellationToken);
                conversionTasks.Add(task);
            }

            if (conversionTasks.Any(t => t.IsCompleted))
            {
                var completedTasks = conversionTasks.Where(t => t.IsCompleted).ToList();
                foreach (var completedTask in completedTasks)
                {
                    try
                    {
                        var result = await completedTask;
                        conversionResults.Add(result);
                        
                        // Mark file as completed in tracker
                        if (fileProgressTrackers.TryGetValue(result.FilePath, out var tracker))
                        {
                            tracker.IsCompleted = true;
                            tracker.Progress = 100;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Task failed: {ex.Message}");
                    }
                    conversionTasks.Remove(completedTask);
                }
            }

            if (conversionTasks.Any(t => !t.IsCompleted))
            {
                await Task.Delay(200, cancellationToken);
            }
        }

        await Task.WhenAll(conversionTasks);
        foreach (var task in conversionTasks)
        {
            try
            {
                var result = await task;
                conversionResults.Add(result);
                
                // Mark file as completed in tracker
                if (fileProgressTrackers.TryGetValue(result.FilePath, out var tracker))
                {
                    tracker.IsCompleted = true;
                    tracker.Progress = 100;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Task failed: {ex.Message}");
            }
        }
        
        // Dispose timer and report final progress
        progressUpdateTimer?.Dispose();
        UpdateOverallProgress();

        return conversionResults;
    }

    private async Task<ConversionResult> ProcessFileAsync(
        VideoFile file,
        string basePath,
        ConversionOptions options,
        ConcurrentDictionary<string, FileProgressTracker> progressTrackers,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Initialize progress tracker for this file
            var tracker = new FileProgressTracker
            {
                FilePath = file.FilePath,
                Progress = 0,
                IsCompleted = false
            };
            progressTrackers[file.FilePath] = tracker;
            
            // Create a progress reporter that updates the tracker
            var fileProgressReporter = new Progress<ConversionProgress>(p =>
            {
                tracker.Progress = p.Percentage;
                tracker.Speed = p.Speed;
            });

            var duration = await _videoInspector.GetVideoDurationAsync(file.FilePath);
            return await _videoConverter.ConvertAsync(file, basePath, duration, options, fileProgressReporter);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static bool IsHevcCodec(string codec)
    {
        return codec.Equals("hevc", StringComparison.OrdinalIgnoreCase) ||
               codec.Equals("h265", StringComparison.OrdinalIgnoreCase);
    }
}