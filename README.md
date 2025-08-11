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

- **Windows x64**: `publish/win-x64/squish.exe`
- **Windows ARM64**: `publish/win-arm64/squish.exe`
- **macOS x64 (Intel)**: `publish/osx-x64/squish`  
- **macOS ARM64 (Apple Silicon)**: `publish/osx-arm64/squish`
- **Linux x64**: `publish/linux-x64/squish`
- **Linux ARM64**: `publish/linux-arm64/squish`

These executables don't require .NET to be installed on the target system.

### Option 2: Build from Source

Requirements:
- .NET 9.0 SDK

#### Unified Build System

The project uses a single build script that automatically detects your platform and builds accordingly:

```bash
# Default: Build for current platform only (fast)
./build.sh              # On macOS/Linux
.\build.ps1             # On Windows (PowerShell)

# Development builds (fastest - no self-contained publish)
./build.sh --dev        # On macOS/Linux
.\build.ps1 -Dev        # On Windows (PowerShell)

# Build for all supported platforms
./build.sh --all        # On macOS/Linux
.\build.ps1 -All        # On Windows (PowerShell)

# Build for platform families
./build.sh win          # Windows (x64 + ARM64)
./build.sh macos        # macOS (x64 + ARM64)
./build.sh linux        # Linux (x64 + ARM64)
./build.sh win linux    # Multiple families

# Build specific architectures
./build.sh win-x64      # Windows x64 only
./build.sh win-arm64    # Windows ARM64 only
./build.sh osx-arm64    # macOS Apple Silicon only
./build.sh linux-arm64  # Linux ARM64 only

# PowerShell examples
.\build.ps1 win,linux             # Multiple platform families
.\build.ps1 win-arm64,linux-arm64 # Multiple specific architectures
```

#### Supported Platforms (64-bit and ARM only)

| Platform Family | Specific Targets | Description |
|------------------|------------------|-------------|
| `win` | `win-x64`, `win-arm64` | Windows x64 and ARM64 |
| `macos` | `osx-x64`, `osx-arm64` | macOS Intel and Apple Silicon |
| `linux` | `linux-x64`, `linux-arm64` | Linux x64 and ARM64 |

#### Manual Build Commands

```bash
# Build for development (current platform)
dotnet build

# Build for release (current platform)  
dotnet build -c Release

# Manual cross-platform publish
dotnet publish Squish.Console/Squish.Console.csproj -c Release --self-contained true -r win-x64 -o publish/win-x64
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