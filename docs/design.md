# Squish

Squish is a utility designed to reduce video file sizes through reencoding and compression. It uses ffmpeg to reencode videos into h.265 with an efficient bitrate. Squish provides the file discovery, queue management, and parallelisation of encoding tasks.

## Squish Core

This library processes video files in the specified directory, checking if they are encoded with H.265 (HEVC). Files not using H.265 will be converted using FFMPEG and replace the original file. GPU acceleration (NVENC, Apple etc.) is used automatically when available. Multiple files can be processed in parallel for faster conversion.

### Technical Design

#### Project Structure

```
Squish.Core/
├── Squish.Core.csproj
├── Abstractions/
│   ├── IFileFinder.cs
│   ├── IVideoInspector.cs
│   ├── IVideoConverter.cs
│   └── IQueueManager.cs
├── Services/
│   ├── FileFinder.cs
│   ├── VideoInspector.cs
│   ├── VideoConverter.cs
│   └── QueueManager.cs
├── Model/
│   ├── VideoFile.cs
│   ├── ConversionOptions.cs
│   ├── ConversionProgress.cs
│   └── ConversionResult.cs
├── JobRunner.cs
└── Exceptions/
    └── VideoConversionException.cs
```

#### Classes and Responsibilities

*   **`IFileFinder`**: Defines a method to find video files recursively in a directory.
    *   `Task<IEnumerable<VideoFile>> FindFilesAsync(string directoryPath);`
*   **`FileFinder`**: Implements `IFileFinder`. The `FindFilesAsync` method will be marked `async` and will use `Directory.EnumerateFiles` to find files. It can be configured to look for specific extensions (e.g., ".mkv", ".mp4", ".avi", ".mov").
*   **`IVideoInspector`**: Defines a method to get video codec information.
    *   `Task<string> GetVideoCodecAsync(string filePath);`
*   **`VideoInspector`**: Implements `IVideoInspector`. The `GetVideoCodecAsync` method will be marked `async` and will use `ffprobe` (part of ffmpeg) to get the codec information. It will `await` the `ffprobe` process and parse its output.
*   **`IVideoConverter`**: Defines a method to convert a video file.
    *   `Task<ConversionResult> ConvertAsync(VideoFile file, ConversionOptions options, IProgress<ConversionProgress> progress);`
*   **`VideoConverter`**: Implements `IVideoConverter`. The `ConvertAsync` method will be marked `async` and will use `ffmpeg` to perform the conversion. It will construct the correct `ffmpeg` command-line arguments and `await` the `ffmpeg` process. It will redirect `ffmpeg`'s standard error stream and parse its output in real-time to report progress (percentage and encoding speed) via the `IProgress<ConversionProgress>` parameter. It will also handle GPU acceleration by adding the appropriate flags (e.g., `-c:v hevc_nvenc` for Nvidia, `-c:v hevc_videotoolbox` for Apple).
*   **`IQueueManager`**: Defines methods to manage the conversion queue.
    *   `void Enqueue(VideoFile file);`
    *   `VideoFile Dequeue();`
    *   `int Count { get; }`
*   **`QueueManager`**: Implements `IQueueManager`. It will use a `ConcurrentQueue<VideoFile>` to store the files to be converted. The files will be sorted by size in descending order before being enqueued.
*   **`JobRunner`**: This class will orchestrate the conversion process. It will use the other services to find files, check their codecs, and convert them. It will manage a pool of `Task`s to run the conversions in parallel, creating and passing an `IProgress<ConversionProgress>` instance to each `VideoConverter` task. The number of parallel jobs will be configurable.
*   **`VideoFile`**: A class representing a video file, containing properties like `FilePath`, `FileSize`, and `Codec`.
*   **`ConversionOptions`**: A class to hold the options for the conversion, such as `UseGpu`, `ParallelJobs`, `Limit`, etc.
*   **`ConversionProgress`**: A class to hold the progress of a conversion, including `Percentage` (double) and `Speed` (string, e.g., "1.5x").
*   **`ConversionResult`**: A class to hold the result of a conversion, including success/failure status and any error messages.
*   **`VideoConversionException`**: Custom exception for errors during conversion.

#### Asynchronous Patterns

All I/O-bound operations (finding files, running external processes) will be implemented using `async/await` and `Task`. This will ensure the application remains responsive and can handle many files efficiently. The `JobRunner` will use `Task.WhenAll` to wait for all the parallel conversion jobs to complete.

#### Error Handling

The `VideoConverter` will check the exit code of the `ffmpeg` process to determine if the conversion was successful. Any errors will be wrapped in a `VideoConversionException` and propagated up the call stack. The `JobRunner` will handle these exceptions and log them appropriately.

#### Testing

*   **Framework**: xUnit will be the testing framework.
*   **Mocking**: `Moq` will be used for creating mock objects for interfaces like `IFileFinder`, `IVideoInspector`, and `IVideoConverter`.
*   **Coverage**: Unit tests will cover all the business logic in the services and the `JobRunner`. Integration tests will be created to test the interaction with the actual `ffmpeg` and `ffprobe` command-line tools.

## Squish Console

Console app interface to Squish. This uses `System.CommandLine`.

### Technical Design

#### Project Structure

```
Squish.Console/
├── Squish.Console.csproj
├── Program.cs
└── appsettings.json
```

#### Command Line Parsing

`System.CommandLine` will be used to define and parse the command-line arguments as specified in the usage section. The parsed arguments will be used to create a `ConversionOptions` object.

#### Dependency Injection

`Microsoft.Extensions.DependencyInjection` will be used to set up the dependency injection container. All the services from `Squish.Core` will be registered in the container. The `JobRunner` will be resolved from the container and executed.

#### User Interface

The console will display a list of files to be converted. `Spectre.Console` will be used to create a rich and interactive console UI. It will show a progress bar for each concurrent conversion job, displaying the percentage complete and the current encoding speed (e.g., "1.5x"). A summary of the conversion results will be displayed at the end.

#### Interaction with Squish Core

The `Program.cs` file will be the entry point of the application. It will create a `ServiceCollection`, register the services, and build a `ServiceProvider`. It will then create a `RootCommand` with `System.CommandLine` and set a handler that will resolve the `JobRunner` and the `ConversionOptions` and start the conversion process.

### Usage

```
squish [OPTIONS] <directory>
```

**Options:**

| Short | Long | Description |
|---|---|---|
| `-l` | `--list-only` | List files that need conversion without converting |
| `-c` | `--cpu-only` | Force CPU encoding (disable GPU acceleration) |
| `-j` | `--jobs N` | Number of parallel encoding jobs (default: auto-detect) |
| `-n` | `--limit N` | Limit number of files to convert (default: all) |
| `-h` | `--help` | Show this help message |

**Description:**

This script processes video files in the specified directory, checking if they are encoded with H.265 (HEVC). Files not using H.265 will be converted and replace the original file. Files are processed from largest to smallest. NVIDIA GPU acceleration (NVENC) is used automatically if available. Multiple files can be processed in parallel for faster conversion.

**Examples:**

```bash
# Convert all non-H.265 videos (auto-detect NVENC and jobs)
squish /path/to/videos

# List files that need conversion
squish --list-only /path/to/videos

# Force CPU encoding
squish --cpu-only /path/to/videos

# Use 4 parallel encoding jobs
squish --jobs 4 /path/to/videos

# Convert only the first 10 files (largest first)
squish --limit 10 /path/to/videos

# Convert first 5 files using 2 parallel jobs
squish --limit 5 --jobs 2 /path/to/videos
```