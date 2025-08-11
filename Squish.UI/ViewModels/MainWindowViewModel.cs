using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squish.Core;
using Squish.Core.Model;
using Squish.Core.Services;
using Squish.UI.Services;

namespace Squish.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly JobRunner _jobRunner;
    private readonly InMemoryLogger _logger;
    private readonly IFolderPickerService _folderPickerService;

    [ObservableProperty]
    private string? _inputFolder;

    [ObservableProperty]
    private string? _outputFolder;

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private int _parallelJobs = Environment.ProcessorCount;

    [ObservableProperty]
    private int? _fileLimit;

    [ObservableProperty]
    private bool _preserveTimestamps = true;

    [ObservableProperty]
    private bool _listOnly;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _statusText = "Ready to process video files";

    [ObservableProperty]
    private ObservableCollection<FileProgressItem> _fileProgress = new();

    [ObservableProperty]
    private string _resultsText = "";

    [ObservableProperty]
    private bool _showResults;

    public MainWindowViewModel(JobRunner jobRunner, InMemoryLogger logger, IFolderPickerService folderPickerService)
    {
        _jobRunner = jobRunner;
        _logger = logger;
        _folderPickerService = folderPickerService;
    }

    [RelayCommand]
    private async Task SelectInputFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync("Select Input Folder");
        if (!string.IsNullOrEmpty(folder))
        {
            InputFolder = folder;
        }
    }

    [RelayCommand]
    private async Task SelectOutputFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync("Select Output Folder");
        if (!string.IsNullOrEmpty(folder))
        {
            OutputFolder = folder;
        }
    }

    [RelayCommand]
    private void ClearOutputFolder()
    {
        OutputFolder = null;
    }

    [RelayCommand]
    private async Task StartProcessingAsync()
    {
        if (string.IsNullOrEmpty(InputFolder) || !Directory.Exists(InputFolder))
        {
            StatusText = "❌ Please select a valid input folder";
            return;
        }

        if (!string.IsNullOrEmpty(OutputFolder) && !Directory.Exists(OutputFolder))
        {
            try
            {
                Directory.CreateDirectory(OutputFolder);
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Cannot create output directory: {ex.Message}";
                return;
            }
        }

        IsProcessing = true;
        OverallProgress = 0;
        StatusText = "🚀 Starting...";
        FileProgress.Clear();
        ResultsText = "";
        ShowResults = false;

        try
        {
            var options = new ConversionOptions
            {
                UseGpu = UseGpu,
                ParallelJobs = ParallelJobs,
                Limit = FileLimit,
                ListOnly = ListOnly,
                OutputFolder = OutputFolder,
                PreserveTimestamps = PreserveTimestamps
            };

            var progressReporter = new Progress<ConversionProgress>(UpdateProgress);
            var results = await _jobRunner.RunAsync(InputFolder, options, progressReporter);
            
            DisplayResults(results, options);
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Processing failed: {ex.Message}";
            ResultsText = $"❌ Error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
            ShowResults = true;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void UpdateProgress(ConversionProgress progress)
    {
        OverallProgress = progress.OverallPercentage;

        if (!string.IsNullOrEmpty(progress.CurrentFile))
        {
            if (progress.CurrentFile.Contains("Discovering"))
            {
                StatusText = "🔍 Discovering video files...";
            }
            else if (progress.CurrentFile.Contains("Inspecting") || progress.CurrentFile.StartsWith("Inspected:"))
            {
                StatusText = "🔍 Inspecting video codecs...";
            }
            else if (progress.ActiveConversions.Any())
            {
                StatusText = $"🎬 Converting {progress.CompletedFiles}/{progress.TotalFiles} files ({progress.OverallPercentage:F1}%)";
                
                // Update file progress items
                FileProgress.Clear();
                foreach (var (filePath, fileProgress) in progress.ActiveConversions)
                {
                    FileProgress.Add(new FileProgressItem
                    {
                        FileName = fileProgress.FileName,
                        Progress = fileProgress.Progress,
                        Speed = fileProgress.Speed ?? ""
                    });
                }
            }
        }

        if (progress.CompletedFiles > 0 && progress.TotalFiles > 0)
        {
            StatusText = $"🎬 Converting {progress.CompletedFiles}/{progress.TotalFiles} files ({progress.OverallPercentage:F1}%)";
        }
    }

    private void DisplayResults(IEnumerable<ConversionResult> results, ConversionOptions options)
    {
        var resultsList = results.ToList();
        var successful = resultsList.Count(r => r.Success);
        var failed = resultsList.Count(r => !r.Success);

        var resultsBuilder = new System.Text.StringBuilder();

        // Display warnings and errors
        foreach (var warning in _logger.Warnings)
        {
            resultsBuilder.AppendLine($"⚠️ Warning: {warning}");
        }

        foreach (var error in _logger.Errors)
        {
            resultsBuilder.AppendLine($"❌ Error: {error}");
        }

        if (_logger.Warnings.Any() || _logger.Errors.Any())
        {
            resultsBuilder.AppendLine();
        }

        if (options.ListOnly)
        {
            resultsBuilder.AppendLine($"📄 Found {resultsList.Count} files that need conversion:");
            resultsBuilder.AppendLine();
            foreach (var result in resultsList.Take(20)) // Limit to first 20 for display
            {
                resultsBuilder.AppendLine($"• {Path.GetFileName(result.FilePath)} ({FormatFileSize(result.OriginalSize)})");
            }
            if (resultsList.Count > 20)
            {
                resultsBuilder.AppendLine($"... and {resultsList.Count - 20} more files");
            }
            StatusText = $"📄 Found {resultsList.Count} files that need conversion";
        }
        else
        {
            var totalOriginalSize = resultsList.Where(r => r.Success).Sum(r => r.OriginalSize);
            var totalNewSize = resultsList.Where(r => r.Success).Sum(r => r.NewSize);
            var spaceSaved = totalOriginalSize - totalNewSize;

            resultsBuilder.AppendLine("🎉 Conversion completed!");
            resultsBuilder.AppendLine();
            resultsBuilder.AppendLine($"✅ Successful: {successful}");
            
            if (failed > 0)
            {
                resultsBuilder.AppendLine($"❌ Failed: {failed}");
            }
            
            if (spaceSaved > 0)
            {
                var percentage = (double)spaceSaved / totalOriginalSize * 100;
                resultsBuilder.AppendLine($"💾 Space saved: {FormatFileSize(spaceSaved)} ({percentage:F1}%)");
                resultsBuilder.AppendLine($"📊 Total size: {FormatFileSize(totalOriginalSize)} → {FormatFileSize(totalNewSize)}");
            }

            if (!string.IsNullOrWhiteSpace(options.OutputFolder))
            {
                resultsBuilder.AppendLine($"📁 Files saved to: {options.OutputFolder}");
            }

            if (resultsList.Where(r => !r.Success).Any())
            {
                resultsBuilder.AppendLine();
                resultsBuilder.AppendLine("❌ Failed files:");
                foreach (var result in resultsList.Where(r => !r.Success))
                {
                    resultsBuilder.AppendLine($"• {Path.GetFileName(result.FilePath)}: {result.ErrorMessage}");
                }
            }

            if (successful > 0)
            {
                StatusText = $"🎉 Successfully converted {successful} file{(successful != 1 ? "s" : "")}!";
            }
            else if (failed > 0)
            {
                StatusText = $"❌ All {failed} file{(failed != 1 ? "s" : "")} failed to convert";
            }
            else
            {
                StatusText = "ℹ️ No files found that need conversion";
            }
        }

        ResultsText = resultsBuilder.ToString();
        ShowResults = true;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class FileProgressItem
{
    public required string FileName { get; set; }
    public double Progress { get; set; }
    public string Speed { get; set; } = "";
}