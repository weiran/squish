# Squish - Video Compression Utility

A C# utility designed to reduce video file sizes through reencoding and compression using H.265/HEVC encoding with ffmpeg.

## Features

- Automatic discovery of video files in directories
- H.265 codec detection to avoid re-encoding already compressed files
- GPU acceleration support (NVIDIA NVENC, Apple VideoToolbox)
- Parallel processing for faster conversion
- Rich console UI with progress bars
- Comprehensive error handling

## Project Structure

```
Squish/
├── Squish.Core/           # Core library with business logic
│   ├── Abstractions/      # Service interfaces
│   ├── Services/          # Service implementations
│   ├── Model/             # Data models
│   ├── Exceptions/        # Custom exceptions
│   └── JobRunner.cs       # Main orchestrator
├── Squish.Console/        # Console application
│   ├── Program.cs         # Entry point with CLI parsing
│   └── appsettings.json   # Configuration
└── docs/
    └── design.md          # Technical design document
```

## Usage

```bash
squish [OPTIONS] <directory>
```

### Options

| Short | Long | Description |
|-------|------|-------------|
| `-l` | `--list-only` | List files that need conversion without converting |
| `-c` | `--cpu-only` | Force CPU encoding (disable GPU acceleration) |
| `-j` | `--jobs N` | Number of parallel encoding jobs (default: auto-detect) |
| `-n` | `--limit N` | Limit number of files to convert (default: all) |
| `-h` | `--help` | Show help message |

### Examples

```bash
# Convert all non-H.265 videos
squish /path/to/videos

# List files that need conversion
squish --list-only /path/to/videos

# Force CPU encoding with 4 parallel jobs
squish --cpu-only --jobs 4 /path/to/videos

# Convert only the first 10 files (largest first)
squish --limit 10 /path/to/videos
```

## Prerequisites

- ffmpeg and ffprobe installed and available in PATH
- Optional: NVIDIA GPU drivers for NVENC acceleration

## Quick Start

### Option 1: Download Pre-built Executables (Recommended)

Self-contained executables with embedded .NET runtime are available in the `publish/` directory after running the build script:

- **Windows x64**: `publish/win-x64/Squish.Console.exe`
- **macOS x64**: `publish/osx-x64/Squish.Console`  
- **macOS ARM64**: `publish/osx-arm64/Squish.Console`
- **Linux x64**: `publish/linux-x64/Squish.Console`

These executables don't require .NET to be installed on the target system.

### Option 2: Build from Source

Requirements:
- .NET 9.0 SDK

```bash
# Build for development
dotnet build

# Create self-contained executables for all platforms
./build-all.sh          # On macOS/Linux
# OR
./build-all.ps1         # On Windows (PowerShell)
```

### Option 3: Run with .NET

```bash
dotnet run --project Squish.Console -- [options] <directory>
```

## Architecture

The application follows a clean architecture with dependency injection:

- **IFileFinder**: Discovers video files recursively
- **IVideoInspector**: Uses ffprobe to detect video codecs
- **IVideoConverter**: Uses ffmpeg to perform H.265 conversion with progress reporting
- **IQueueManager**: Manages conversion queue, prioritizing larger files
- **JobRunner**: Orchestrates the entire conversion process with parallel execution

## Key Features Implemented

1. **Async/Await Pattern**: All I/O operations are asynchronous
2. **GPU Acceleration**: Automatically detects and uses hardware encoders
3. **Progress Reporting**: Real-time progress updates with encoding speed
4. **Error Handling**: Comprehensive exception handling with custom exceptions
5. **Parallel Processing**: Configurable number of concurrent conversions
6. **File Sorting**: Processes largest files first for optimal resource usage
7. **Rich Console UI**: Uses Spectre.Console for beautiful progress displays

## Dependencies

- **System.CommandLine**: Command-line argument parsing
- **Spectre.Console**: Rich console UI components
- **Microsoft.Extensions.DependencyInjection**: Dependency injection container