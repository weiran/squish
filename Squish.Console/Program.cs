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

var rootCommand = new RootCommand("Squish - Video compression utility using H.265/HEVC encoding")
{
    directoryArgument,
    listOnlyOption,
    cpuOnlyOption,
    jobsOption,
    limitOption
};

rootCommand.SetHandler(async (string directory, bool listOnly, bool cpuOnly, int jobs, int? limit) =>
{
    if (!Directory.Exists(directory))
    {
        AnsiConsole.MarkupLine("[red]Error: Directory does not exist[/]");
        Environment.Exit(1);
    }

    var services = new ServiceCollection();
    services.AddTransient<IFileSystemWrapper, FileSystemWrapper>();
    services.AddTransient<IProcessWrapper, ProcessWrapper>();
    services.AddTransient<IFileFinder, FileFinder>();
    services.AddTransient<IVideoInspector, VideoInspector>();
    services.AddTransient<IVideoConverter, VideoConverter>();
    services.AddTransient<IQueueManager, QueueManager>();
    services.AddTransient<JobRunner>();

    var serviceProvider = services.BuildServiceProvider();
    var jobRunner = serviceProvider.GetRequiredService<JobRunner>();

    var options = new ConversionOptions
    {
        UseGpu = !cpuOnly,
        ParallelJobs = jobs,
        Limit = limit,
        ListOnly = listOnly
    };

    AnsiConsole.MarkupLine($"[green]Squish - Processing directory:[/] [yellow]{directory}[/]");
    AnsiConsole.MarkupLine($"[cyan]GPU Acceleration:[/] {(options.UseGpu ? "[green]Enabled[/]" : "[red]Disabled[/]")}");
    AnsiConsole.MarkupLine($"[cyan]Parallel Jobs:[/] [yellow]{options.ParallelJobs}[/]");
    if (options.Limit.HasValue)
        AnsiConsole.MarkupLine($"[cyan]File Limit:[/] [yellow]{options.Limit}[/]");

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
        var fileProgressTasks = new Dictionary<string, ProgressTask>();

        var progressReporter = new Progress<ConversionProgress>(p =>
        {
            // Update main task progress with overall percentage
            mainTask.Value = p.OverallPercentage;
            
            if (!string.IsNullOrEmpty(p.CurrentFile))
            {
                if (!fileProgressTasks.ContainsKey(p.CurrentFile))
                {
                    fileProgressTasks[p.CurrentFile] = ctx.AddTask($"[blue]{p.CurrentFile.EscapeMarkup()}[/]", maxValue: 100);
                }
                
                var task = fileProgressTasks[p.CurrentFile];
                task.Value = p.Percentage;
                task.Description = $"[blue]{p.CurrentFile.EscapeMarkup()}[/] [dim]({p.Speed})[/]";
            }
        });

        var results = await jobRunner.RunAsync(directory, options, progressReporter);
        mainTask.Value = 100;

        var resultsList = results.ToList();
        var successful = resultsList.Count(r => r.Success);
        var failed = resultsList.Count(r => !r.Success);

        AnsiConsole.WriteLine();

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

            foreach (var result in resultsList.Where(r => !r.Success))
            {
                AnsiConsole.MarkupLine($"[red]Failed: {result.FilePath} - {result.ErrorMessage}[/]");
            }
        }
    });

}, directoryArgument, listOnlyOption, cpuOnlyOption, jobsOption, limitOption);

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