# Squish UI - Cross-Platform Video Compression Tool

A modern, cross-platform desktop application for video compression using H.265/HEVC encoding built with Avalonia UI.

## Features

- **Cross-Platform**: Runs natively on Windows and macOS with platform-specific styling
- **Modern UI**: Clean, intuitive interface with native look and feel
- **Real-time Progress**: Live progress tracking for individual files and overall conversion
- **Folder Management**: Easy input/output folder selection with file browser integration
- **Flexible Settings**: Complete control over all compression options
- **GPU Acceleration**: Automatic detection and use of hardware encoders (NVENC, VideoToolbox)
- **Parallel Processing**: Configurable number of concurrent conversions
- **Results Display**: Detailed results with space savings and error reporting

## Screenshots

The application provides a clean, modern interface with:
- 🎬 **Header**: Clear branding and description
- 📁 **Folder Selection**: Input folder (required) and output folder (optional) selection
- ⚙️ **Settings Panel**: GPU acceleration, parallel jobs, file limits, and other options
- 🚀 **Start Button**: Large, prominent action button
- 📊 **Progress Display**: Overall progress bar and individual file progress indicators
- 📋 **Results Section**: Detailed conversion results and statistics

## Architecture

Built using modern .NET technologies:

- **Framework**: .NET 9.0
- **UI Framework**: Avalonia UI 11.1.3
- **MVVM**: CommunityToolkit.Mvvm for clean separation of concerns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Core Logic**: Shared Squish.Core library with console application

## Project Structure

```
Squish.UI/
├── Views/
│   ├── MainWindow.axaml          # Main UI layout
│   └── MainWindow.axaml.cs       # View code-behind
├── ViewModels/
│   ├── ViewModelBase.cs          # Base view model
│   └── MainWindowViewModel.cs    # Main window logic
├── Services/
│   ├── IFolderPickerService.cs   # Folder picker abstraction
│   └── FolderPickerService.cs    # Platform-specific folder picker
├── Converters/
│   └── ValueConverters.cs        # UI value converters
├── Styles/
│   └── CustomStyles.axaml        # Custom UI styles
├── Assets/                       # Application assets
├── Program.cs                    # Application entry point
├── App.axaml                     # Application resources
└── App.axaml.cs                  # Application startup logic
```

## Usage

### Running the Application

```bash
# Development
dotnet run --project Squish.UI

# Build for release
dotnet build -c Release Squish.UI
```

### Using the Application

1. **Select Input Folder**: Browse and select the folder containing videos to compress
2. **Configure Output** (Optional): Select an output folder to preserve original files
3. **Adjust Settings**:
   - Toggle GPU acceleration
   - Set number of parallel jobs (1-16)
   - Set file limit (optional)
   - Choose to preserve timestamps
   - Enable "List only" mode to see what would be converted
4. **Start Processing**: Click the "Start Processing" button
5. **Monitor Progress**: Watch real-time progress for individual files and overall conversion
6. **View Results**: See detailed results including space saved and any errors

## Configuration Options

### GPU Acceleration
- **Enabled**: Uses NVIDIA NVENC or Apple VideoToolbox hardware encoding
- **Disabled**: Uses software encoding (CPU-only)

### Parallel Jobs
- **Range**: 1-16 concurrent conversions
- **Default**: Number of CPU cores
- **Higher values**: Faster processing but more resource usage

### File Limit
- **Optional**: Limit the number of files to convert
- **Useful for**: Testing or processing subsets of large collections

### Output Folder
- **Not specified**: Replaces original files
- **Specified**: Preserves originals, saves converted files to new location

### Additional Options
- **Preserve Timestamps**: Maintains original file creation/modification dates
- **List Only**: Shows what files need conversion without actually converting

## Platform-Specific Features

### Windows
- Native Windows styling and controls
- Integration with Windows file system dialogs
- Windows-specific progress indicators

### macOS
- Native macOS styling and controls
- Integration with macOS file system dialogs
- macOS-specific progress indicators
- Support for both Intel and Apple Silicon

## Dependencies

The application uses the following key dependencies:

- **Avalonia**: Cross-platform .NET UI framework
- **Avalonia.Themes.Fluent**: Modern Fluent Design styling
- **CommunityToolkit.Mvvm**: MVVM helpers and source generators
- **Microsoft.Extensions.DependencyInjection**: Dependency injection container
- **Squish.Core**: Shared video processing logic

## Building and Distribution

### Development Build
```bash
dotnet build Squish.UI
```

### Release Build
```bash
dotnet build -c Release Squish.UI
```

### Self-Contained Deployment
```bash
# Windows
dotnet publish Squish.UI -c Release -r win-x64 --self-contained true

# macOS Intel
dotnet publish Squish.UI -c Release -r osx-x64 --self-contained true

# macOS Apple Silicon
dotnet publish Squish.UI -c Release -r osx-arm64 --self-contained true
```

## Requirements

### Development
- .NET 9.0 SDK
- Compatible IDE (Visual Studio, VS Code, Rider, etc.)

### Runtime
- .NET 9.0 Runtime (or self-contained deployment)
- ffmpeg and ffprobe in PATH
- Optional: GPU drivers for hardware acceleration

## Error Handling

The application provides comprehensive error handling:

- **Input Validation**: Checks for valid input folders
- **Output Directory Creation**: Automatically creates output directories
- **FFmpeg Dependency**: Clear error messages if ffmpeg is not available
- **File Access**: Handles permission issues and file locks
- **Progress Recovery**: Graceful handling of conversion failures

## Contributing

This UI project is part of the larger Squish video compression utility. See the main README.md for contribution guidelines and project information.