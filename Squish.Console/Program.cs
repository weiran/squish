using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Squish.Core;
using Squish.Core.Abstractions;
using Squish.Core.Model;
using Squish.Core.Services;

var directoryArgument = new Argument<string>(
    name: "directory",
    description: "The directory to process video files from");

var listOnlyOption = new Option<bool>(
    aliases: ["-l", "--list-only"],
    description: "List files that need conversion without converting");

var cpuOnlyOption = new Option<bool>(
    aliases: ["-c", "--cpu-only"],
    description: "Force CPU encoding (disable GPU acceleration)");

var jobsOption = new Option<int>(
    aliases: ["-j", "--jobs"],
    description: "Number of parallel encoding jobs",
    getDefaultValue: () => Environment.ProcessorCount);

var limitOption = new Option<int?>(
    aliases: ["-n", "--limit"],
    description: "Limit number of files to convert");

var outputOption = new Option<string?>(
    aliases: ["-o", "--output"],
    description: "Output folder for converted files (preserves originals)");

var rootCommand = new RootCommand("Squish - Video compression utility using H.265/HEVC encoding")
{
    directoryArgument,
    listOnlyOption,
    cpuOnlyOption,
    jobsOption,
    limitOption,
    outputOption
};

rootCommand.SetHandler(async (string directory, bool listOnly, bool cpuOnly, int jobs, int? limit, string? output) =>
{
    if (!Directory.Exists(directory))
    {
        AnsiConsole.MarkupLine("[red]Error: Directory does not exist[/]");
        Environment.Exit(1);
    }

    // Validate output folder if specified
    if (!string.IsNullOrWhiteSpace(output))
    {
        try
        {
            Directory.CreateDirectory(output);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: Cannot create output directory '{output.EscapeMarkup()}': {ex.Message.EscapeMarkup()}[/]");
            Environment.Exit(1);
        }
    }

    var services = new ServiceCollection();
    services.AddTransient<IFileSystemWrapper, FileSystemWrapper>();
    services.AddTransient<IProcessWrapper, ProcessWrapper>();
    services.AddTransient<IFileFinder, FileFinder>();
    services.AddTransient<IVideoInspector, VideoInspector>();
    services.AddTransient<IVideoConverter, VideoConverter>();
    services.AddTransient<IQueueManager, QueueManager>();
    services.AddSingleton<InMemoryLogger>();
    services.AddTransient<ILogger>(provider => provider.GetRequiredService<InMemoryLogger>());
    services.AddTransient<JobRunner>();

    var serviceProvider = services.BuildServiceProvider();
    var jobRunner = serviceProvider.GetRequiredService<JobRunner>();
    var logger = serviceProvider.GetRequiredService<InMemoryLogger>();

    var options = new ConversionOptions
    {
        UseGpu = !cpuOnly,
        ParallelJobs = jobs,
        Limit = limit,
        ListOnly = listOnly,
        OutputFolder = output
    };

    AnsiConsole.MarkupLine($"[green]Squish - Processing directory:[/] [yellow]{directory.EscapeMarkup()}[/]");
    AnsiConsole.MarkupLine($"[cyan]GPU Acceleration:[/] {(options.UseGpu ? "[green]Enabled[/]" : "[red]Disabled[/]")}");
    AnsiConsole.MarkupLine($"[cyan]Parallel Jobs:[/] [yellow]{options.ParallelJobs}[/]");
    if (options.Limit.HasValue)
        AnsiConsole.MarkupLine($"[cyan]File Limit:[/] [yellow]{options.Limit}[/]");
    if (!string.IsNullOrWhiteSpace(options.OutputFolder))
        AnsiConsole.MarkupLine($"[cyan]Output Folder:[/] [yellow]{options.OutputFolder.EscapeMarkup()}[/]");

    var progress = AnsiConsole.Progress()
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new RemainingTimeColumn(),
            new SpinnerColumn());

    await progress.StartAsync(async ctx =>
    {
        var mainTask = ctx.AddTask("[green]Processing files[/]", maxValue: 100);
        var fileTasks = new Dictionary<string, ProgressTask>();

        var currentPhase = "";
        
        var progressReporter = new Progress<ConversionProgress>(p =>
        {
            // Update main task progress with overall percentage
            mainTask.Value = p.OverallPercentage;
            
            if (!string.IsNullOrEmpty(p.CurrentFile))
            {
                // Handle discovery phase
                if (p.CurrentFile.Contains("Discovering"))
                {
                    if (currentPhase != "discovering")
                    {
                        currentPhase = "discovering";
                        mainTask.Description = "[green]Discovering video files...[/]";
                    }
                    return;
                }
                
                // Handle inspection phase
                if (p.CurrentFile.Contains("Inspecting") || p.CurrentFile.StartsWith("Inspected:"))
                {
                    if (currentPhase != "inspecting")
                    {
                        currentPhase = "inspecting";
                        mainTask.Description = $"[green]Inspecting video codecs[/]";
                    }
                    return;
                }
                
                // Handle conversion phase - show individual file progress bars
                if (p.ActiveConversions.Any() || currentPhase == "converting")
                {
                    if (currentPhase != "converting")
                    {
                        currentPhase = "converting";
                        mainTask.Description = $"[green]Converting {p.CompletedFiles}/{p.TotalFiles} files[/]";
                    }
                    else
                    {
                        mainTask.Description = $"[green]Converting {p.CompletedFiles}/{p.TotalFiles} files[/]";
                    }
                    
                    // Update individual file progress bars
                    foreach (var (filePath, fileProgress) in p.ActiveConversions)
                    {
                        if (!fileTasks.ContainsKey(filePath))
                        {
                            var taskDescription = $"[cyan]{fileProgress.FileName.EscapeMarkup()}[/]";
                            fileTasks[filePath] = ctx.AddTask(taskDescription, maxValue: 100);
                        }
                        
                        var task = fileTasks[filePath];
                        task.Value = fileProgress.Progress;
                        
                        // Update description with speed info if available
                        if (!string.IsNullOrEmpty(fileProgress.Speed) && fileProgress.Speed != "0x")
                        {
                            task.Description = $"[cyan]{fileProgress.FileName.EscapeMarkup()}[/] [dim]({fileProgress.Speed.EscapeMarkup()})[/]";
                        }
                    }
                    
                    // Remove completed tasks that are no longer active
                    var completedTasks = fileTasks.Where(kvp => !p.ActiveConversions.ContainsKey(kvp.Key)).ToList();
                    foreach (var (filePath, task) in completedTasks)
                    {
                        task.Value = 100;
                        task.StopTask();
                        fileTasks.Remove(filePath);
                    }
                    
                    return;
                }
            }
        });

        var results = await jobRunner.RunAsync(directory, options, progressReporter);
        mainTask.Value = 100;
        
        // Complete any remaining file tasks
        foreach (var task in fileTasks.Values)
        {
            task.Value = 100;
            task.StopTask();
        }

        var resultsList = results.ToList();
        var successful = resultsList.Count(r => r.Success);
        var failed = resultsList.Count(r => !r.Success);

        AnsiConsole.WriteLine();
        
        // Display any warnings or errors collected during processing
        foreach (var warning in logger.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning.EscapeMarkup()}");
        }
        
        foreach (var error in logger.Errors)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {error.EscapeMarkup()}");
        }
        
        if (logger.Warnings.Any() || logger.Errors.Any())
        {
            AnsiConsole.WriteLine();
        }

        if (listOnly)
        {
            var table = new Table();
            table.AddColumn("File Path");
            table.AddColumn("Size");

            foreach (var result in resultsList)
            {
                table.AddRow(
                    result.FilePath.EscapeMarkup(),
                    FormatFileSize(result.OriginalSize));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[green]{resultsList.Count} files need conversion[/]");
        }
        else
        {
            var totalOriginalSize = resultsList.Where(r => r.Success).Sum(r => r.OriginalSize);
            var totalNewSize = resultsList.Where(r => r.Success).Sum(r => r.NewSize);
            var spaceSaved = totalOriginalSize - totalNewSize;

            AnsiConsole.MarkupLine($"[green]Conversion completed![/]");
            AnsiConsole.MarkupLine($"[cyan]Successful:[/] [green]{successful}[/]");
            AnsiConsole.MarkupLine($"[cyan]Failed:[/] [red]{failed}[/]");
            
            if (spaceSaved > 0)
            {
                var percentage = (double)spaceSaved / totalOriginalSize * 100;
                AnsiConsole.MarkupLine($"[cyan]Space saved:[/] [yellow]{FormatFileSize(spaceSaved)} ({percentage:F1}%)[/]");
            }

            if (!string.IsNullOrWhiteSpace(options.OutputFolder))
            {
                AnsiConsole.MarkupLine($"[cyan]Converted files saved to:[/] [yellow]{options.OutputFolder.EscapeMarkup()}[/]");
            }

            foreach (var result in resultsList.Where(r => !r.Success))
            {
                AnsiConsole.MarkupLine($"[red]Failed: {result.FilePath.EscapeMarkup()} - {result.ErrorMessage?.EscapeMarkup()}[/]");
            }
        }
    });

}, directoryArgument, listOnlyOption, cpuOnlyOption, jobsOption, limitOption, outputOption);

static string FormatFileSize(long bytes)
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

return await rootCommand.InvokeAsync(args);