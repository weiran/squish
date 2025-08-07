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
        var allFiles = await _fileFinder.FindFilesAsync(directoryPath);
        var filesToProcess = new List<VideoFile>();

        foreach (var file in allFiles)
        {
            try
            {
                var codec = await _videoInspector.GetVideoCodecAsync(file.FilePath);
                file.Codec = codec;

                if (!IsHevcCodec(codec))
                {
                    filesToProcess.Add(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not inspect {file.FilePath}: {ex.Message}");
            }
        }

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

        var results = new List<ConversionResult>();
        var semaphore = new SemaphoreSlim(options.ParallelJobs, options.ParallelJobs);
        var tasks = new List<Task>();

        while (_queueManager.Count > 0 || tasks.Any(t => !t.IsCompleted))
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (_queueManager.Count > 0 && tasks.Count(t => !t.IsCompleted) < options.ParallelJobs)
            {
                var file = _queueManager.Dequeue();
                if (file == null) break;

                var task = ProcessFileAsync(file, options, progress, semaphore, cancellationToken);
                tasks.Add(task);
            }

            if (tasks.Any(t => t.IsCompleted))
            {
                var completedTasks = tasks.Where(t => t.IsCompleted).ToList();
                foreach (var completedTask in completedTasks)
                {
                    try
                    {
                        var result = await completedTask;
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Task failed: {ex.Message}");
                    }
                    tasks.Remove(completedTask);
                }
            }

            if (tasks.Any(t => !t.IsCompleted))
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            try
            {
                var result = await task;
                results.Add(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Task failed: {ex.Message}");
            }
        }

        return results;
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