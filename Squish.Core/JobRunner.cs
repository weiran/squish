using Squish.Core.Abstractions;
using Squish.Core.Model;
using Squish.Core.Services;

namespace Squish.Core;

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

        ((QueueManager)_queueManager).EnqueueRange(filesToProcess);

        var conversionResults = new List<ConversionResult>();
        var conversionSemaphore = new SemaphoreSlim(options.ParallelJobs, options.ParallelJobs);
        var conversionTasks = new List<Task<ConversionResult>>();
        var totalFiles = filesToProcess.Count;
        
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
                var file = _queueManager.Dequeue();
                if (file == null) break;

                var task = ProcessFileAsync(file, options, progress, conversionSemaphore, cancellationToken);
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
                        
                        // Report overall progress after each file completion
                        progress?.Report(new ConversionProgress
                        {
                            TotalFiles = totalFiles,
                            CompletedFiles = conversionResults.Count,
                            CurrentFile = $"Completed: {Path.GetFileName(result.FilePath)}"
                        });
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
                await Task.Delay(100, cancellationToken);
            }
        }

        await Task.WhenAll(conversionTasks);
        foreach (var task in conversionTasks)
        {
            try
            {
                var result = await task;
                conversionResults.Add(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Task failed: {ex.Message}");
            }
        }

        return conversionResults;
    }

    private async Task<ConversionResult> ProcessFileAsync(
        VideoFile file,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await _videoConverter.ConvertAsync(file, options, progress ?? new Progress<ConversionProgress>());
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